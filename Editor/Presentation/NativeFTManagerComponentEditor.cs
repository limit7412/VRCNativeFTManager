using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
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
        /// <summary>並びはFaceEmoSuppressionTriggerの宣言順に一致させる (enumValueIndexで引くため)</summary>
        internal static readonly IReadOnlyList<string> TriggerLabelKeys = new[]
        {
            "prop.face_emo_trigger.eye_or_lip",
            "prop.face_emo_trigger.lip_only",
        };

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
        private SerializedProperty _faceEmoSuppression;
        private SerializedProperty _sensitivity;
        private SerializedProperty _menuMode;

        /// <summary>追加パラメーターの一覧。追加の操作を差し替えるため、標準の配列表示の代わりに使う</summary>
        private ReorderableList _extraParameterList;

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
            _faceEmoSuppression = serializedObject.FindProperty(nameof(NativeFTManagerComponent.faceEmoSuppression));
            _sensitivity = serializedObject.FindProperty(nameof(NativeFTManagerComponent.sensitivity));
            _menuMode = serializedObject.FindProperty(nameof(NativeFTManagerComponent.menuMode));
        }

        private void OnDisable()
        {
            UpdateCheck.ResultChanged -= Repaint;
            _extraParameterList = null;
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
            DrawSensitivity();

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
            DrawFaceEmoSuppression();

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

                    // ヒエラルキーのメニューでは、プリセットのトグルの構成をMenu Itemの増減で反映する（仕様書6.3）
                    if (ComponentSettings.UsesHierarchyMenu(component))
                    {
                        MenuHierarchyBuilder.ApplyPreset(component, component.preset, S("inspector.undo.preset"));
                    }
                }
                else if (!MatchesPreset(component))
                {
                    Undo.RecordObject(component, S("inspector.undo.preset"));
                    component.preset = NativeFTPreset.Custom;
                    MarkModified(component);
                }
            }

            serializedObject.Update();
        }

        /// <summary>
        /// プリセットとの一致。ヒエラルキーのメニューでは、トグルの構成をMenu Itemから読む
        /// </summary>
        private static bool MatchesPreset(NativeFTManagerComponent component)
        {
            if (!ComponentSettings.UsesHierarchyMenu(component))
            {
                return NativeFTPresets.Matches(component, component.preset);
            }

            var menu = MenuHierarchyScanner.Scan(component);
            return NativeFTPresets.Matches(
                component, component.preset, ComponentSettings.GetToggleSource(component, menu), true);
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

        /// <summary>
        /// 感度（仕様書5.6）。プリセットには含めないので、変えてもCustomにならない。
        /// どの系統も無効のあいだは上限の倍率を出さない
        /// </summary>
        private void DrawSensitivity()
        {
            EditorGUILayout.LabelField(S("inspector.section.sensitivity"), EditorStyles.boldLabel);

            if (IsHierarchyMenu())
            {
                DrawHierarchySensitivity();
                return;
            }

            var eye = _sensitivity.FindPropertyRelative(nameof(SensitivitySettings.eye));
            var lip = _sensitivity.FindPropertyRelative(nameof(SensitivitySettings.lip));
            EditorGUILayout.PropertyField(eye, G("inspector.sensitivity.eye"));
            EditorGUILayout.PropertyField(lip, G("inspector.sensitivity.lip"));

            bool anyEnabled = eye.hasMultipleDifferentValues || lip.hasMultipleDifferentValues
                              || eye.boolValue || lip.boolValue;
            if (!anyEnabled)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                _sensitivity.FindPropertyRelative(nameof(SensitivitySettings.maxMultiplier)),
                G("inspector.sensitivity.max_multiplier"));
            EditorGUILayout.PropertyField(
                _sensitivity.FindPropertyRelative(nameof(SensitivitySettings.saved)),
                G("inspector.sensitivity.saved"));
            EditorGUILayout.HelpBox(S("inspector.sensitivity.description"), MessageType.Info);
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// FaceEmoの抑制（仕様書10.3）。無効のあいだは詳細を出さない
        /// </summary>
        private void DrawFaceEmoSuppression()
        {
            EditorGUILayout.LabelField(S("inspector.section.face_emo"), EditorStyles.boldLabel);

            var enabled = _faceEmoSuppression.FindPropertyRelative(nameof(FaceEmoSuppressionSettings.enabled));
            EditorGUILayout.PropertyField(enabled, G("inspector.face_emo.enabled"));
            if (!enabled.hasMultipleDifferentValues && !enabled.boolValue)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(S("inspector.face_emo.description"), MessageType.Info);

            EditorGUILayout.HelpBox(S("inspector.face_emo.expression_control"), MessageType.Info);
            EditorGUILayout.PropertyField(
                _faceEmoSuppression.FindPropertyRelative(nameof(FaceEmoSuppressionSettings.faceEmoteIndex)),
                G("prop.face_emote_index"));

            var trigger = _faceEmoSuppression.FindPropertyRelative(nameof(FaceEmoSuppressionSettings.trigger));
            DrawLocalizedEnumPopup(trigger, "prop.face_emo_trigger", TriggerLabelKeys);
            if (!trigger.hasMultipleDifferentValues && trigger.enumValueIndex == (int)FaceEmoSuppressionTrigger.LipOnly)
            {
                EditorGUILayout.HelpBox(S("inspector.face_emo.lip_only"), MessageType.Warning);
            }

            EditorGUILayout.Space();
            DrawExtraParameters();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(
                _faceEmoSuppression.FindPropertyRelative(nameof(FaceEmoSuppressionSettings.removeFxLayers)),
                G("prop.remove_fx_layers"),
                true);
            EditorGUILayout.HelpBox(S("inspector.remove_fx_layers"), MessageType.Info);
            EditorGUI.indentLevel--;
        }

        private void DrawExtraParameters()
        {
            // ReorderableListは複数選択中の配列を扱えない。標準の配列表示へ戻すと、
            // 「+」が末尾の項目を複製して既定値の項目を作れないため、複数選択中は編集させない。
            // 一覧の中身はアバターごとに違うので、まとめて編集できても得るものは少ない
            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.LabelField(G("prop.extra_parameters"));
                EditorGUILayout.HelpBox(S("inspector.extra_parameters.multi_edit"), MessageType.Info);
            }
            else
            {
                // ReorderableListはインデントを考慮しないので、字下げを外して描く
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                GetExtraParameterList().DoLayoutList();
                EditorGUI.indentLevel = indent;
            }

            EditorGUILayout.HelpBox(S("inspector.extra_parameters"), MessageType.Info);
        }

        /// <summary>
        /// 追加パラメーターの一覧を作る
        ///
        /// 標準の配列表示の「+」は末尾の項目を複製する。複製では、解除時の扱いなどの既定値が
        /// 新しい項目へ入らず、同じメニューアイテムを指す項目もできてしまう。
        /// 追加のときは既定値の項目を作るよう差し替える
        /// </summary>
        private ReorderableList GetExtraParameterList()
        {
            // serializedObjectはエディタの生存中変わらないため、一度作った一覧を使い回す
            if (_extraParameterList != null)
            {
                return _extraParameterList;
            }

            var property = _faceEmoSuppression.FindPropertyRelative(nameof(FaceEmoSuppressionSettings.extraParameters));
            var list = new ReorderableList(serializedObject, property, true, true, true, true);

            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, G("prop.extra_parameters"));

            list.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(property.GetArrayElementAtIndex(index), true)
                + EditorGUIUtility.standardVerticalSpacing;

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                // 折りたたみの三角が一覧のつまみと重ならないよう、字下げ分だけずらす
                const float foldoutIndent = 12f;
                rect.x += foldoutIndent;
                rect.width -= foldoutIndent;
                rect.y += EditorGUIUtility.standardVerticalSpacing / 2f;
                rect.height -= EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(rect, property.GetArrayElementAtIndex(index), true);
            };

            list.onAddCallback = reorderable =>
            {
                var index = property.arraySize;
                property.arraySize++;

                var element = property.GetArrayElementAtIndex(index);
                ExtraParameterEntryDrawer.ApplyDefaults(element);
                element.isExpanded = true;

                reorderable.index = index;
            };

            _extraParameterList = list;
            return list;
        }

        /// <summary>
        /// 訳語を当てたenumのポップアップを描く
        /// </summary>
        /// <remarks>
        /// PropertyFieldではC#の識別子がそのまま出るため、ポップアップを自前で描く。
        /// BeginPropertyで囲むと、プレハブインスタンス上での上書き表示と
        /// 右クリックのRevertが、通常のフィールドと同じように働く
        /// </remarks>
        private static void DrawLocalizedEnumPopup(
            SerializedProperty property, string labelKey, IReadOnlyList<string> optionKeys)
        {
            var options = new GUIContent[optionKeys.Count];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = G(optionKeys[i]);
            }

            var rect = EditorGUILayout.GetControlRect();
            var label = EditorGUI.BeginProperty(rect, G(labelKey), property);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            // enumValueIndexは宣言順の添字なので、選択肢の並びと対応する
            var selected = EditorGUI.Popup(rect, label, property.enumValueIndex, options);

            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = selected;
            }

            EditorGUI.EndProperty();
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
                case GeneratorPresence.Present:
                    return report.GeneratorSetWriteEnabled
                        ? "inspector.status.generator_set_write"
                        : "inspector.status.generator_present";
                default:
                    return "inspector.status.generator_none";
            }
        }

        /// <summary>
        /// 選んでいるコンポーネントが、どれもメニューをヒエラルキーで持つか
        /// </summary>
        private bool IsHierarchyMenu()
        {
            return !_menuMode.hasMultipleDifferentValues && _menuMode.enumValueIndex == (int)NativeFTMenuMode.Hierarchy;
        }

        private void DrawToggles()
        {
            EditorGUILayout.LabelField(S("inspector.section.toggles"), EditorStyles.boldLabel);

            if (_menuMode.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox(S("inspector.menu.mixed_modes"), MessageType.Info);
                return;
            }

            if (IsHierarchyMenu())
            {
                DrawHierarchyToggles();
                return;
            }

            EditorGUILayout.HelpBox(S("inspector.menu.generated"), MessageType.Info);
            if (GUILayout.Button(S("inspector.menu.expand")))
            {
                ExpandMenus();
                return;
            }

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
        /// ビルド時にメニューを作るコンポーネントの設定を、そのままMenu Itemの階層にする（仕様書6.1）。
        /// 0.1.0-test8までに作ったコンポーネントの移行に使う
        /// </summary>
        private void ExpandMenus()
        {
            serializedObject.ApplyModifiedProperties();
            var undoName = S("inspector.undo.expand_menu");
            foreach (var component in targets.OfType<NativeFTManagerComponent>())
            {
                if (ComponentSettings.UsesHierarchyMenu(component))
                {
                    continue;
                }

                var toggles = ComponentSettings.ResolveToggles(component);
                Undo.RecordObject(component, undoName);
                component.menuMode = NativeFTMenuMode.Hierarchy;

                // Menu Itemにしないトグルの固定の値はtogglesに残るので、古い版で保存して表に無いトグルを、
                // 持たないときと同じ値で加えておく。加えないと、展開後の表で固定の値を編集できない
                if (component.toggles == null)
                {
                    component.toggles = new System.Collections.Generic.List<ToggleSettings>();
                }

                foreach (NativeFTToggle toggle in System.Enum.GetValues(typeof(NativeFTToggle)))
                {
                    if (component.toggles.All(settings => settings == null || settings.toggle != toggle))
                    {
                        component.toggles.Add(ToggleSettings.CreateMissing(toggle));
                    }
                }

                MarkModified(component);
                MenuHierarchyBuilder.Expand(component, toggles, undoName);
            }

            serializedObject.Update();
        }

        /// <summary>
        /// ヒエラルキーのメニューのトグル。値はMenu Itemの側で編集するので、ここでは読んだ結果を表示し、
        /// 無いトグルを足すボタンだけを置く（仕様書6.4）
        /// </summary>
        private void DrawHierarchyToggles()
        {
            EditorGUILayout.HelpBox(S("inspector.menu.hierarchy"), MessageType.Info);
            if (serializedObject.isEditingMultipleObjects || _report == null || _report.Menu == null)
            {
                return;
            }

            var component = (NativeFTManagerComponent)target;
            var menu = _report.Menu;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(GUIContent.none, GUILayout.MinWidth(100f));
                EditorGUILayout.LabelField(G("inspector.toggle.in_menu"), GUILayout.Width(80f));
                EditorGUILayout.LabelField(G("inspector.toggle.default_value"), GUILayout.Width(80f));
                EditorGUILayout.LabelField(G("inspector.toggle.saved"), GUILayout.Width(80f));
            }

            var features = ComponentSettings.GetFeatures(component);
            foreach (NativeFTToggle toggle in System.Enum.GetValues(typeof(NativeFTToggle)))
            {
                var settings = menu.GetToggle(toggle, component.GetToggle);
                bool available = features.IsEnabled(toggle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(MenuPlan.GetLabel(toggle), GUILayout.MinWidth(100f));
                    if (!menu.Contains(toggle))
                    {
                        using (new EditorGUI.DisabledScope(!available))
                        {
                            if (GUILayout.Button(S("inspector.menu.add"), GUILayout.Width(80f)))
                            {
                                MenuHierarchyBuilder.AddToggle(
                                    component, toggle, settings.defaultValue, true, S("inspector.undo.menu_item"));
                                // プリセットとの一致を見直させる
                                GUI.changed = true;
                            }

                            // Menu Itemの無いトグルの固定の値は、コンポーネントのtogglesに置く
                            var fixedValue = FindToggleProperty(toggle);
                            if (fixedValue != null)
                            {
                                DrawToggleCell(fixedValue.FindPropertyRelative(nameof(ToggleSettings.defaultValue)));
                            }
                            else
                            {
                                using (new EditorGUI.DisabledScope(true))
                                {
                                    EditorGUILayout.Toggle(settings.defaultValue, GUILayout.Width(80f));
                                }
                            }
                        }

                        EditorGUILayout.LabelField(GUIContent.none, GUILayout.Width(80f));
                        continue;
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.Toggle(true, GUILayout.Width(80f));
                        EditorGUILayout.Toggle(settings.defaultValue, GUILayout.Width(80f));
                        EditorGUILayout.Toggle(settings.saved, GUILayout.Width(80f));
                    }
                }

                if (menu.Contains(toggle) && !available)
                {
                    EditorGUILayout.HelpBox(S("inspector.menu.feature_disabled", MenuPlan.GetLabel(toggle)), MessageType.Info);
                }
            }
        }

        /// <summary>
        /// togglesの中で、指定したトグルの要素。古い版で保存したコンポーネントには無いことがある
        /// </summary>
        private SerializedProperty FindToggleProperty(NativeFTToggle toggle)
        {
            for (int i = 0; i < _toggles.arraySize; i++)
            {
                var element = _toggles.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative(nameof(ToggleSettings.toggle)).intValue == (int)toggle)
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// ヒエラルキーのメニューの感度。系統はRadialのMenu Itemの有無で決まるので、足す・外すボタンを置く。
        /// 保存はMenu Itemの設定に従う
        /// </summary>
        private void DrawHierarchySensitivity()
        {
            if (serializedObject.isEditingMultipleObjects || _report == null || _report.Menu == null)
            {
                EditorGUILayout.HelpBox(S("inspector.menu.hierarchy"), MessageType.Info);
                return;
            }

            var component = (NativeFTManagerComponent)target;
            var menu = _report.Menu;
            foreach (SensitivityChannel channel in System.Enum.GetValues(typeof(SensitivityChannel)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var labelKey = channel == SensitivityChannel.Eye ? "inspector.sensitivity.eye" : "inspector.sensitivity.lip";
                    EditorGUILayout.LabelField(G(labelKey), GUILayout.MinWidth(100f));
                    bool present = menu.Contains(channel);
                    if (GUILayout.Button(S(present ? "inspector.menu.remove_radial" : "inspector.menu.add_radial"), GUILayout.Width(160f)))
                    {
                        var undoName = S("inspector.undo.menu_item");
                        if (present)
                        {
                            MenuHierarchyBuilder.RemoveRadial(component, channel, undoName);
                        }
                        else
                        {
                            MenuHierarchyBuilder.AddRadial(component, channel, true, undoName);
                        }

                        GUI.changed = true;
                    }
                }
            }

            if (menu.Radials.Count == 0)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                _sensitivity.FindPropertyRelative(nameof(SensitivitySettings.maxMultiplier)),
                G("inspector.sensitivity.max_multiplier"));
            EditorGUILayout.HelpBox(S("inspector.sensitivity.description"), MessageType.Info);
            EditorGUI.indentLevel--;
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
