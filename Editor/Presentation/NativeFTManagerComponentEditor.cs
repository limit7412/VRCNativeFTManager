using System.Linq;
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
    /// 並びは仕様書6.4に沿い、状態表示、警告、プリセット、トグル、表情補正、Descriptorの設定、見積もり、詳細の順に出す
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
        private SerializedProperty _enableEmulation;
        private SerializedProperty _enableLimits;
        private SerializedProperty _enableEyeSync;

        private bool _showDetails;
        private bool _showRules;
        private InspectUseCase.Report _report;

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
            _enableEmulation = serializedObject.FindProperty(nameof(NativeFTManagerComponent.enableEmulation));
            _enableLimits = serializedObject.FindProperty(nameof(NativeFTManagerComponent.enableLimits));
            _enableEyeSync = serializedObject.FindProperty(nameof(NativeFTManagerComponent.enableEyeSync));
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

            _report = serializedObject.isEditingMultipleObjects
                ? null
                : InspectUseCase.Inspect((NativeFTManagerComponent)target);
            if (_report != null)
            {
                DrawStatus(_report);
            }

            // 何か操作されたときだけプリセットとの一致を見直す。開いただけでCustomへ書き換えると、
            // 古い版で保存したコンポーネントのシーンが、見ただけで変更扱いになるため
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_preset, G("inspector.preset"));
            bool presetChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.PropertyField(_generatorIntegration, G("inspector.generator_integration"));

            EditorGUILayout.Space();
            DrawToggles();

            EditorGUILayout.Space();
            DrawCorrections();

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

            if (_report != null && _report.Estimate != null)
            {
                EditorGUILayout.Space();
                DrawEstimate(_report);
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

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                SyncPreset(presetChanged);
            }
        }

        /// <summary>
        /// プリセットを選び直したら中身を書き込み、中身をプリセットから変えたらCustomにする（仕様書6.3）
        /// </summary>
        private void SyncPreset(bool presetChanged)
        {
            foreach (var component in targets.OfType<NativeFTManagerComponent>())
            {
                if (presetChanged && component.preset != NativeFTPreset.Custom)
                {
                    Undo.RecordObject(component, S("inspector.undo.preset"));
                    NativeFTPresets.Apply(component, component.preset);
                    MarkModified(component);
                }
                else if (!NativeFTPresets.Matches(component, component.preset))
                {
                    Undo.RecordObject(component, S("inspector.undo.preset"));
                    component.preset = NativeFTPreset.Custom;
                    MarkModified(component);
                }
            }

            serializedObject.Update();
        }

        /// <summary>
        /// SerializedObjectを通さずに書き換えた後に呼ぶ。プレハブのインスタンスでは、
        /// 変更をオーバーライドとして記録しないと、シーンを開き直したときにプレハブの値へ戻る
        /// </summary>
        private static void MarkModified(NativeFTManagerComponent component)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(component);
        }

        private void DrawCorrections()
        {
            EditorGUILayout.LabelField(S("inspector.section.corrections"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableEmulation, G("inspector.enable_emulation"));
            EditorGUILayout.PropertyField(_enableLimits, G("inspector.enable_limits"));
            EditorGUILayout.PropertyField(_enableEyeSync, G("inspector.enable_eye_sync"));

            _showRules = EditorGUILayout.Foldout(_showRules, S("inspector.corrections.rules"), true);
            if (!_showRules)
            {
                return;
            }

            var components = targets.OfType<NativeFTManagerComponent>().ToArray();
            var missing = _report != null ? _report.Corrections.MissingTargets : new string[0];
            EditorGUI.indentLevel++;
            foreach (var rule in CorrectionRuleTable.All)
            {
                bool featureEnabled = components.Any(component => IsFeatureEnabled(component, rule.Feature));
                using (new EditorGUI.DisabledScope(!featureEnabled))
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool excludedAll = components.All(component => IsExcluded(component, rule.Id));
                    bool excludedAny = components.Any(component => IsExcluded(component, rule.Id));
                    EditorGUI.showMixedValue = excludedAll != excludedAny;
                    EditorGUI.BeginChangeCheck();
                    var label = new GUIContent($"{rule.Id} ({rule.Feature})", rule.Source);
                    bool enabled = EditorGUILayout.ToggleLeft(label, !excludedAny);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SetExcluded(components, rule.Id, !enabled);
                    }

                    EditorGUI.showMixedValue = false;
                    if (missing.Contains(rule.Id))
                    {
                        EditorGUILayout.LabelField(S("inspector.corrections.no_shape"), GUILayout.Width(120f));
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        private static bool IsFeatureEnabled(NativeFTManagerComponent component, CorrectionFeature feature)
        {
            switch (feature)
            {
                case CorrectionFeature.Emulation:
                    return component.enableEmulation;
                case CorrectionFeature.Limits:
                    return component.enableLimits;
                default:
                    return component.enableEyeSync;
            }
        }

        private static bool IsExcluded(NativeFTManagerComponent component, string id)
        {
            return component.excludedRules != null && component.excludedRules.Contains(id);
        }

        private void SetExcluded(NativeFTManagerComponent[] components, string id, bool excluded)
        {
            // 書き換えはSerializedObjectを通さずに行うので、先に保留中の変更を書き戻し、後で読み直す
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObjects(components, S("inspector.undo.rules"));
            foreach (var component in components)
            {
                if (component.excludedRules == null)
                {
                    component.excludedRules = new System.Collections.Generic.List<string>();
                }

                component.excludedRules.Remove(id);
                if (excluded)
                {
                    component.excludedRules.Add(id);
                }

                MarkModified(component);
            }

            serializedObject.Update();
        }

        private static void DrawEstimate(InspectUseCase.Report report)
        {
            var estimate = report.Estimate;
            EditorGUILayout.LabelField(S("inspector.section.estimate"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                S("inspector.estimate.sets"),
                S("inspector.estimate.sets_value", estimate.SplitSetCount, estimate.GateSetCount));
            EditorGUILayout.LabelField(
                S("inspector.estimate.behaviors"),
                S("inspector.estimate.behaviors_value", estimate.BehaviorCount, estimate.ExistingBehaviorCount));
            EditorGUILayout.LabelField(
                S("inspector.estimate.instructions"),
                S("inspector.estimate.instructions_value",
                    estimate.TotalInstructions,
                    CustomExpression.AvatarInstructionLimit,
                    estimate.Instructions));
            EditorGUILayout.LabelField(S("inspector.estimate.operations"), estimate.Operations.ToString());
            EditorGUILayout.LabelField(
                S("inspector.estimate.synced_bits"),
                S("inspector.estimate.synced_bits_value",
                    estimate.TotalSyncedBits,
                    BuildEstimate.SyncedBitLimit,
                    estimate.SyncedBits));
            EditorGUILayout.LabelField(S("inspector.estimate.fx_layers"), estimate.FxLayerCount.ToString());

            if (report.EstimateExcludesGenerator)
            {
                EditorGUILayout.HelpBox(S("inspector.estimate.generator_note"), MessageType.Info);
            }
        }

        private static void DrawStatus(InspectUseCase.Report report)
        {
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

                var toggle = (NativeFTToggle)kind.intValue;
                using (new EditorGUI.DisabledScope(!IsToggleAvailable(toggle)))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(MenuPlan.GetLabel(toggle), GUILayout.MinWidth(100f));
                    DrawToggleCell(showInMenu);
                    DrawToggleCell(defaultValue);
                    using (new EditorGUI.DisabledScope(!showInMenu.hasMultipleDifferentValues && !showInMenu.boolValue))
                    {
                        DrawToggleCell(saved);
                    }
                }
            }

            DrawMissingToggles();
        }

        /// <summary>
        /// 表情補正のトグルは、その機能を生成しないあいだ効かないので操作できなくする
        /// </summary>
        private bool IsToggleAvailable(NativeFTToggle toggle)
        {
            switch (toggle)
            {
                case NativeFTToggle.Emulation:
                    return _enableEmulation.hasMultipleDifferentValues || _enableEmulation.boolValue;
                case NativeFTToggle.Limits:
                    return _enableLimits.hasMultipleDifferentValues || _enableLimits.boolValue;
                case NativeFTToggle.EyeSync:
                    return _enableEyeSync.hasMultipleDifferentValues || _enableEyeSync.boolValue;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 古い版で保存したコンポーネントは、後から足したトグルを持たない。
        /// 持たないトグルはメニューに出さない扱いになるので、その旨を出し、表に加えるボタンを置く
        /// </summary>
        private void DrawMissingToggles()
        {
            var components = targets.OfType<NativeFTManagerComponent>().ToArray();
            var missing = System.Enum.GetValues(typeof(NativeFTToggle))
                .Cast<NativeFTToggle>()
                .Where(toggle => components.Any(component =>
                    component.toggles == null || component.toggles.All(settings => settings == null || settings.toggle != toggle)))
                .ToArray();
            if (missing.Length == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                S("inspector.toggle.missing", string.Join(", ", missing.Select(MenuPlan.GetLabel))),
                MessageType.Info);
            if (!GUILayout.Button(S("inspector.toggle.add_missing")))
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
            Undo.RecordObjects(components, S("inspector.undo.toggles"));
            foreach (var component in components)
            {
                if (component.toggles == null)
                {
                    component.toggles = new System.Collections.Generic.List<ToggleSettings>();
                }

                foreach (var toggle in missing)
                {
                    if (component.toggles.All(settings => settings == null || settings.toggle != toggle))
                    {
                        // 足しただけで挙動が変わらないよう、持たないときと同じ値で加える
                        component.toggles.Add(ToggleSettings.CreateMissing(toggle));
                    }
                }

                MarkModified(component);
            }

            serializedObject.Update();
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
