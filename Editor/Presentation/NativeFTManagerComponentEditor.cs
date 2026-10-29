using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf.ui;
using NativeFTManager.Domain;
using NativeFTManager.Infra;
using NativeFTManager.UseCase;
using static NativeFTManager.Localization;

namespace NativeFTManager.Presentation
{
    /// <summary>
    /// NativeFTManagerComponentのインスペクタ
    ///
    /// 表示文言はNDMFのローカライズ機構から引く。
    /// 並びは仕様書6.4に沿い、M1では状態表示、警告、プリセット、トグル、Descriptorの設定、詳細を出す。
    /// 表情補正と見積もりはM2で足す
    /// </summary>
    [CustomEditor(typeof(NativeFTManagerComponent))]
    [CanEditMultipleObjects]
    public class NativeFTManagerComponentEditor : Editor
    {
        private SerializedProperty _preset;
        private SerializedProperty _toggles;
        private SerializedProperty _lerpSpeed;
        private SerializedProperty _excludedSetNames;
        private SerializedProperty _setUseAllTrackingKeys;
        private SerializedProperty _visemeDampenOverride;
        private SerializedProperty _visemeDampenAmount;
        private SerializedProperty _generatorIntegration;

        private bool _showDetails;

        private void OnEnable()
        {
            // 更新の確認は応答が返った時点で結果が変わる。
            // インスペクタが操作されるまで古い表示のままにしない
            UpdateCheck.ResultChanged += Repaint;

            _preset = serializedObject.FindProperty(nameof(NativeFTManagerComponent.preset));
            _toggles = serializedObject.FindProperty(nameof(NativeFTManagerComponent.toggles));
            _lerpSpeed = serializedObject.FindProperty(nameof(NativeFTManagerComponent.lerpSpeed));
            _excludedSetNames = serializedObject.FindProperty(nameof(NativeFTManagerComponent.excludedSetNames));
            _setUseAllTrackingKeys = serializedObject.FindProperty(nameof(NativeFTManagerComponent.setUseAllTrackingKeys));
            _visemeDampenOverride = serializedObject.FindProperty(nameof(NativeFTManagerComponent.visemeDampenOverride));
            _visemeDampenAmount = serializedObject.FindProperty(nameof(NativeFTManagerComponent.visemeDampenAmount));
            _generatorIntegration = serializedObject.FindProperty(nameof(NativeFTManagerComponent.generatorIntegration));
        }

        private void OnDisable()
        {
            UpdateCheck.ResultChanged -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            LanguageSwitcher.DrawImmediate();
            EditorGUILayout.LabelField("Kx VRC NativeFT Manager", EditorStyles.boldLabel);

            // 更新の案内はヘッダーの直下へ置く。読み飛ばされない位置で、設定の並びは崩さない
            UpdateNotice.Draw();

            EditorGUILayout.HelpBox(S("inspector.description"), MessageType.Info);

            if (!serializedObject.isEditingMultipleObjects)
            {
                DrawStatus((NativeFTManagerComponent)target);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_preset, G("inspector.preset"));
            EditorGUILayout.PropertyField(_generatorIntegration, G("inspector.generator_integration"));

            EditorGUILayout.Space();
            DrawToggles();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(S("inspector.section.descriptor"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_setUseAllTrackingKeys, G("inspector.set_use_all_tracking_keys"));
            EditorGUILayout.PropertyField(_visemeDampenOverride, G("inspector.viseme_dampen"));
            var dampen = (VisemeDampenOverride)_visemeDampenOverride.enumValueIndex;
            if (dampen == VisemeDampenOverride.DampenTracking || dampen == VisemeDampenOverride.DampenVisemes)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_visemeDampenAmount, G("inspector.viseme_dampen_amount"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            _showDetails = EditorGUILayout.Foldout(_showDetails, S("inspector.section.details"), true);
            if (_showDetails)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_lerpSpeed, G("inspector.lerp_speed"));
                EditorGUILayout.PropertyField(_excludedSetNames, G("inspector.excluded_set_names"), true);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawStatus(NativeFTManagerComponent component)
        {
            var report = InspectUseCase.Inspect(component);

            if (report.HasAvatar)
            {
                EditorGUILayout.LabelField(S("inspector.section.status"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    S("inspector.status.sets"),
                    S("inspector.status.sets_value", report.SetCount, report.AssignedKeyCount));
                EditorGUILayout.LabelField(
                    S("inspector.status.use_all_tracking_keys"),
                    S(report.UseAllTrackingKeys ? "common.on" : "common.off"));
                EditorGUILayout.LabelField(S("inspector.status.generator"), S(GetGeneratorStatusKey(report)));
            }

            foreach (var diagnostic in report.Diagnostics)
            {
                EditorGUILayout.HelpBox(
                    S(diagnostic.LocalizationKey, diagnostic.Args) + "\n"
                    + S(diagnostic.LocalizationKey + ":description", diagnostic.Args),
                    ToMessageType(diagnostic.Severity));
            }
        }

        private static string GetGeneratorStatusKey(InspectUseCase.Report report)
        {
            switch (report.Generator)
            {
                case GeneratorPresence.Supported:
                    return report.GeneratorSetWriteEnabled
                        ? "inspector.status.generator_set_write"
                        : "inspector.status.generator_present";
                case GeneratorPresence.Unsupported:
                    return "inspector.status.generator_unsupported";
                default:
                    return "inspector.status.generator_none";
            }
        }

        private void DrawToggles()
        {
            EditorGUILayout.LabelField(S("inspector.section.toggles"), EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(GUIContent.none, GUILayout.MinWidth(100f));
                EditorGUILayout.LabelField(G("inspector.toggle.show_in_menu"), GUILayout.Width(80f));
                EditorGUILayout.LabelField(G("inspector.toggle.default_value"), GUILayout.Width(80f));
                EditorGUILayout.LabelField(G("inspector.toggle.saved"), GUILayout.Width(80f));
            }

            for (int i = 0; i < _toggles.arraySize; i++)
            {
                var element = _toggles.GetArrayElementAtIndex(i);
                var kind = element.FindPropertyRelative(nameof(ToggleSettings.toggle));
                var showInMenu = element.FindPropertyRelative(nameof(ToggleSettings.showInMenu));
                var defaultValue = element.FindPropertyRelative(nameof(ToggleSettings.defaultValue));
                var saved = element.FindPropertyRelative(nameof(ToggleSettings.saved));

                using (new EditorGUILayout.HorizontalScope())
                {
                    var toggle = (NativeFTToggle)kind.enumValueIndex;
                    EditorGUILayout.LabelField(MenuPlan.GetLabel(toggle), GUILayout.MinWidth(100f));
                    DrawToggleCell(showInMenu);
                    DrawToggleCell(defaultValue);
                    using (new EditorGUI.DisabledScope(!showInMenu.hasMultipleDifferentValues && !showInMenu.boolValue))
                    {
                        DrawToggleCell(saved);
                    }
                }
            }
        }

        /// <summary>
        /// 複数選択で値が揃っていなければ混在表示にし、操作されたときだけ書き戻す。
        /// 無条件に代入すると、操作していないコンポーネントまで同じ値で上書きされるため
        /// </summary>
        private static void DrawToggleCell(SerializedProperty property)
        {
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(80f));
            if (EditorGUI.EndChangeCheck())
            {
                property.boolValue = value;
            }

            EditorGUI.showMixedValue = false;
        }

        private static MessageType ToMessageType(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Error:
                case DiagnosticSeverity.NonFatal:
                    return MessageType.Error;
                case DiagnosticSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }
    }
}
