using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// ゲートSet 1つ。対応を持たない空のSetで、重みをFXのレイヤーで切り替え、
    /// Parameter BehaviorのweightBlendSetNameに指定して効き方を変える（仕様書5.4）
    /// </summary>
    internal sealed class GateSlot
    {
        public GateSlot(string key, GroupCondition condition)
        {
            Key = key;
            Condition = condition;
        }

        /// <summary>Set名から接頭辞を除いた部分（例: Gate/Emulation/Lip）</summary>
        public string Key { get; }

        public string SetName => NativeFTManagerParameterNames.SetPrefix + Key;

        public GroupCondition Condition { get; }
    }

    /// <summary>
    /// Descriptorへ足すParameter Behavior 1つ
    /// </summary>
    internal sealed class CorrectionBehaviorPlan
    {
        public CorrectionBehaviorPlan(
            string ruleId,
            CorrectionMode mode,
            string targetBlendShape,
            string expression,
            string gateSetName)
        {
            RuleId = ruleId;
            Mode = mode;
            TargetBlendShape = targetBlendShape;
            Expression = expression;
            GateSetName = gateSetName;
        }

        public string RuleId { get; }

        public CorrectionMode Mode { get; }

        public string TargetBlendShape { get; }

        public string Expression { get; }

        /// <summary>weightBlendSetNameに書くゲートSetの名前。常に有効ならnull</summary>
        public string GateSetName { get; }
    }

    internal sealed class CorrectionPlan
    {
        public CorrectionPlan(
            IReadOnlyList<CorrectionBehaviorPlan> behaviors,
            IReadOnlyList<GateSlot> gates,
            IReadOnlyList<string> missingTargets,
            IReadOnlyList<string> droppedRules,
            int instructions,
            IReadOnlyList<string> missingShapes = null)
        {
            Behaviors = behaviors;
            Gates = gates;
            MissingTargets = missingTargets;
            DroppedRules = droppedRules;
            Instructions = instructions;
            MissingShapes = missingShapes ?? new string[0];
        }

        public IReadOnlyList<CorrectionBehaviorPlan> Behaviors { get; }

        /// <summary>Behaviorが1つ以上使うゲート</summary>
        public IReadOnlyList<GateSlot> Gates { get; }

        /// <summary>対象キーのシェイプがSetに無く、生成しなかった規則のid（I-01）</summary>
        public IReadOnlyList<string> MissingTargets { get; }

        /// <summary>対象のシェイプがFace Tracking Meshに無く、生成しなかったカスタム補正のid（W-22）</summary>
        public IReadOnlyList<string> MissingShapes { get; }

        /// <summary>Custom式の命令数の上限を超えるので落とした規則のid（W-12）</summary>
        public IReadOnlyList<string> DroppedRules { get; }

        /// <summary>生成したCustom式の命令数の合計（定数の簡約後）</summary>
        public int Instructions { get; }

        /// <summary>
        /// Eye SyncのBehaviorを生成したか。生成しなかったときは、EyeClosedのSetを止めるとまばたきの出力が無くなる
        /// </summary>
        public bool ReplacesEyeClosed => Behaviors.Any(behavior => behavior.RuleId == CorrectionRuleTable.EyeSyncRuleId);

        /// <summary>
        /// グループのSetを切り替えるレイヤーのグループ。Eye SyncのBehaviorが無ければ、
        /// EyeClosedはEye SyncでOFFにせず、Eyeと同じ条件（FT ∧ Eye）で切り替える
        /// </summary>
        public FaceTrackingGroup GetLayerGroup(FaceTrackingGroup group)
        {
            return group == FaceTrackingGroup.EyeClosed && !ReplacesEyeClosed ? FaceTrackingGroup.Eye : group;
        }

        public static CorrectionPlan Empty =>
            new CorrectionPlan(new CorrectionBehaviorPlan[0], new GateSlot[0], new string[0], new string[0], 0);
    }

    /// <summary>
    /// 規則表とSetの中身から、Parameter BehaviorsとゲートSetを決める（仕様書5.4）
    ///
    /// ゲートの条件は FT ∧ 機能のトグル に、規則が読み書きするキーの側のトグルを足したものにする。
    /// 式の入力はSetの重みの影響を受けないので、例えば口から眉を動かす規則は、
    /// 目と口のどちらが無効でも止めないと、無効にした側の表情が残るためである。
    ///
    /// カスタム補正は機能のトグルを持たないので、条件は FT ∧ 式が読むキーの側のトグル になる。
    /// 命令数の予算が足りないときに組み込みの規則から先に落ちるよう、カスタム補正を規則表より前に並べる
    /// </summary>
    internal static class CorrectionPlanner
    {
        /// <summary>
        /// 生成する可能性のあるゲート。Setの中身が決まる前（Generating）にFXのレイヤーを作るのに使う。
        /// 常に偽または常に真になるゲートは含めない
        /// </summary>
        public static IReadOnlyList<GateSlot> GetPossibleGates(
            ToggleSet toggles,
            ICollection<string> excludedRules,
            IReadOnlyList<CorrectionRule> customRules = null)
        {
            var gates = new List<GateSlot>();
            foreach (var rule in SelectRules(toggles, excludedRules, customRules))
            {
                var gate = GetGate(rule, toggles);
                if (gate != null && gates.All(existing => existing.Key != gate.Key))
                {
                    gates.Add(gate);
                }
            }

            var visemeGate = VisemeSuppression.GetGate(toggles);
            if (visemeGate != null)
            {
                gates.Add(visemeGate);
            }

            return gates;
        }

        /// <param name="sets">分割後にDescriptorへ書くSet（ゲートを除く）。対象キーのシェイプ名をここから引く</param>
        /// <param name="toggles">トグル</param>
        /// <param name="excludedRules">個別に外した規則のid</param>
        /// <param name="existingInstructions">Descriptorに元からあるCustom式の命令数</param>
        /// <param name="lipSyncShapes">DescriptorのLip Sync（Viseme Blend Shape）のシェイプ。Visemesのトグルが口パクを止める対象に加える</param>
        /// <param name="allSets">
        /// Descriptorへ書くSetの全体（分割しなかったSetを含む）。Visemesのトグルは、ここのビゼーム対応を対象にし、
        /// ここのFTのキーに割り当てたシェイプを対象から外す。nullならsetsを使う
        /// </param>
        /// <param name="customRules">カスタム補正の規則。規則表より前に並べる</param>
        /// <param name="knownShapes">
        /// Face Tracking Meshにあるシェイプ名。カスタム補正の対象がここに無ければ生成しない。nullなら確かめない
        /// </param>
        public static CorrectionPlan Plan(
            IReadOnlyList<FaceTrackingSetData> sets,
            ToggleSet toggles,
            ICollection<string> excludedRules,
            int existingInstructions,
            IEnumerable<string> lipSyncShapes = null,
            IReadOnlyList<FaceTrackingSetData> allSets = null,
            IReadOnlyList<CorrectionRule> customRules = null,
            ICollection<string> knownShapes = null)
        {
            var behaviors = new List<CorrectionBehaviorPlan>();
            var gates = new List<GateSlot>();
            var missing = new List<string>();
            var missingShapes = new List<string>();
            var dropped = new List<string>();
            int budget = CustomExpression.AvatarInstructionLimit - Math.Max(0, existingInstructions);
            int instructions = 0;

            foreach (var rule in SelectRules(toggles, excludedRules, customRules))
            {
                var ruleBehaviors = new List<CorrectionBehaviorPlan>();
                int ruleInstructions = 0;
                var gate = GetGate(rule, toggles);

                foreach (var target in rule.Targets)
                {
                    foreach (var (shape, multiplier) in GetShapes(sets, target, knownShapes))
                    {
                        var expression = GetExpression(rule, target, multiplier);
                        var info = CustomExpression.Analyze(expression);
                        if (!info.IsValid)
                        {
                            throw new InvalidOperationException($"rule {rule.Id}: {info.Error}");
                        }

                        // 左右が同じシェイプ（両目で1つのまばたきなど）で式も同じなら、1つで足りる
                        if (ruleBehaviors.Any(existing => existing.TargetBlendShape == shape
                                                          && existing.Expression == expression))
                        {
                            continue;
                        }

                        ruleBehaviors.Add(new CorrectionBehaviorPlan(
                            rule.Id, rule.Mode, shape, expression, gate?.SetName));
                        ruleInstructions += info.Instructions;
                    }
                }

                if (ruleBehaviors.Count == 0)
                {
                    (rule.Feature == CorrectionFeature.Custom ? missingShapes : missing).Add(rule.Id);
                    continue;
                }

                if (instructions + ruleInstructions > budget)
                {
                    dropped.Add(rule.Id);
                    continue;
                }

                instructions += ruleInstructions;
                behaviors.AddRange(ruleBehaviors);
                if (gate != null && gates.All(existing => existing.Key != gate.Key))
                {
                    gates.Add(gate);
                }
            }

            PlanVisemeSuppression(allSets ?? sets, toggles, lipSyncShapes, budget, behaviors, gates, dropped, ref instructions);

            return new CorrectionPlan(behaviors, gates, missing, dropped, instructions, missingShapes);
        }

        /// <summary>
        /// Visemesのトグルで口パクを止めるLimitBlendshapeMax（仕様書5.5）。表情補正の規則と同じ命令数の予算に入れ、
        /// 超えるなら規則と同じく落としてW-12で知らせる。止めるシェイプが無ければ何も作らない
        /// </summary>
        private static void PlanVisemeSuppression(
            IReadOnlyList<FaceTrackingSetData> sets,
            ToggleSet toggles,
            IEnumerable<string> lipSyncShapes,
            int budget,
            List<CorrectionBehaviorPlan> behaviors,
            List<GateSlot> gates,
            List<string> dropped,
            ref int instructions)
        {
            if (!VisemeSuppression.IsPossible(toggles))
            {
                return;
            }

            var shapes = VisemeSuppression.GetTargetShapes(sets, lipSyncShapes);
            if (shapes.Count == 0)
            {
                return;
            }

            var gate = VisemeSuppression.GetGate(toggles);
            int cost = CustomExpression.Analyze(VisemeSuppression.Expression).Instructions * shapes.Count;
            if (instructions + cost > budget)
            {
                dropped.Add(VisemeSuppression.RuleId);
                return;
            }

            instructions += cost;
            behaviors.AddRange(shapes.Select(shape => new CorrectionBehaviorPlan(
                VisemeSuppression.RuleId,
                CorrectionMode.LimitMax,
                shape,
                VisemeSuppression.Expression,
                gate?.SetName)));
            if (gate != null)
            {
                gates.Add(gate);
            }
        }

        /// <summary>
        /// 機能が有効で、個別に外されておらず、ゲートの条件が常に偽でない規則。カスタム補正、規則表の順に並べる
        /// </summary>
        public static IEnumerable<CorrectionRule> SelectRules(
            ToggleSet toggles,
            ICollection<string> excludedRules,
            IReadOnlyList<CorrectionRule> customRules = null)
        {
            foreach (var rule in (customRules ?? new CorrectionRule[0]).Concat(CorrectionRuleTable.All))
            {
                if (!IsFeatureEnabled(toggles.Features, rule.Feature))
                {
                    continue;
                }

                if (excludedRules != null && excludedRules.Contains(rule.Id))
                {
                    continue;
                }

                if (GetCondition(rule).Simplify(toggles) == null)
                {
                    continue;
                }

                yield return rule;
            }
        }

        /// <summary>
        /// 規則の効く条件。FT ∧ 機能のトグル ∧ 規則が読み書きするキーの側のトグル。
        /// カスタム補正には機能のトグルが無い
        /// </summary>
        public static GroupCondition GetCondition(CorrectionRule rule)
        {
            var toggles = new List<NativeFTToggle> { NativeFTToggle.FaceTracking };
            if (rule.Feature != CorrectionFeature.Custom)
            {
                toggles.Add(GetFeatureToggle(rule.Feature));
            }

            foreach (var requirement in GetRequirements(rule))
            {
                toggles.Add(requirement);
            }

            return GroupCondition.Of(toggles.ToArray());
        }

        /// <summary>
        /// 規則のゲート。条件がメニューのトグルで変わらず常に真なら、ゲートは要らないのでnull
        /// </summary>
        private static GateSlot GetGate(CorrectionRule rule, ToggleSet toggles)
        {
            var condition = GetCondition(rule).Simplify(toggles);
            if (condition == null || condition.Literals.Count == 0)
            {
                return null;
            }

            var suffix = string.Concat(GetRequirements(rule).Select(GetRequirementName));
            var key = "Gate/" + rule.Feature + (suffix.Length > 0 ? "/" + suffix : string.Empty);
            return new GateSlot(key, GetCondition(rule));
        }

        /// <summary>
        /// 規則の対象と入力のキーが属する側のトグル（Eye、Pupil、Lipの順）
        /// </summary>
        private static IReadOnlyList<NativeFTToggle> GetRequirements(CorrectionRule rule)
        {
            var keys = new HashSet<int>();
            foreach (var target in rule.Targets)
            {
                if (target.Key != CorrectionTarget.NoKey)
                {
                    keys.Add(target.Key);
                }

                foreach (var input in CustomExpression.Analyze(target.Expression).Inputs)
                {
                    int index = IndexOfKey(input);
                    if (index >= 0)
                    {
                        keys.Add(index);
                    }
                }
            }

            bool eye = false, pupil = false, lip = false;
            foreach (var key in keys)
            {
                switch (FaceTrackingGroupTable.GetGroup(key))
                {
                    case FaceTrackingGroup.Eye:
                    case FaceTrackingGroup.EyeClosed:
                    case FaceTrackingGroup.Brow:
                        eye = true;
                        break;
                    case FaceTrackingGroup.Pupil:
                        eye = true;
                        pupil = true;
                        break;
                    case FaceTrackingGroup.Lip:
                        lip = true;
                        break;
                }
            }

            var result = new List<NativeFTToggle>();
            if (eye)
            {
                result.Add(NativeFTToggle.EyeTracking);
            }

            if (pupil)
            {
                result.Add(NativeFTToggle.Pupil);
            }

            if (lip)
            {
                result.Add(NativeFTToggle.LipTracking);
            }

            return result;
        }

        private static string GetRequirementName(NativeFTToggle toggle)
        {
            switch (toggle)
            {
                case NativeFTToggle.EyeTracking:
                    return "Eye";
                case NativeFTToggle.LipTracking:
                    return "Lip";
                default:
                    return toggle.ToString();
            }
        }

        private static NativeFTToggle GetFeatureToggle(CorrectionFeature feature)
        {
            switch (feature)
            {
                case CorrectionFeature.Emulation:
                    return NativeFTToggle.Emulation;
                case CorrectionFeature.Limits:
                    return NativeFTToggle.Limits;
                case CorrectionFeature.EyeSync:
                    return NativeFTToggle.EyeSync;
                default:
                    throw new ArgumentOutOfRangeException(nameof(feature), feature, null);
            }
        }

        private static bool IsFeatureEnabled(CorrectionFeatures features, CorrectionFeature feature)
        {
            // カスタム補正は、コンポーネントの一覧で1件ずつ有効・無効を持つ
            return feature == CorrectionFeature.Custom || features.IsEnabled(GetFeatureToggle(feature));
        }

        /// <summary>
        /// 書き込む式。ApplyToBlendshapeはSetの出力の代わりにシェイプへ書くので、
        /// Setの倍率を掛けて、Setが出していた値と同じ尺度にそろえる。LimitはSetの出力を含む最終値を抑えるので掛けない
        /// </summary>
        private static string GetExpression(CorrectionRule rule, CorrectionTarget target, float multiplier)
        {
            if (rule.Mode != CorrectionMode.ApplyToBlendshape || Math.Abs(multiplier - 1f) < 1e-6f)
            {
                return target.Expression;
            }

            return "(" + target.Expression + ") * "
                   + multiplier.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 対象のシェイプと、式に掛ける倍率。シェイプ名で指す対象は、利用者が書いた式をそのまま使うので等倍にする
        /// </summary>
        private static IEnumerable<(string Shape, float Multiplier)> GetShapes(
            IReadOnlyList<FaceTrackingSetData> sets,
            CorrectionTarget target,
            ICollection<string> knownShapes)
        {
            if (target.Shape == null)
            {
                return GetShapes(sets, target.Key);
            }

            return knownShapes == null || knownShapes.Contains(target.Shape)
                ? new[] { (target.Shape, 1f) }
                : new (string, float)[0];
        }

        /// <summary>
        /// キーに割り当てられたシェイプと、その対応の倍率（同じシェイプが複数のSetにあれば最初のもの）
        /// </summary>
        private static IEnumerable<(string Shape, float Multiplier)> GetShapes(IReadOnlyList<FaceTrackingSetData> sets, int key)
        {
            var seen = new HashSet<string>();
            foreach (var set in sets ?? new FaceTrackingSetData[0])
            {
                var name = set?.GetBlendShapeName(key);
                if (name == null || !seen.Add(name))
                {
                    continue;
                }

                var multipliers = set.BlendShapeMultipliers;
                float multiplier = multipliers != null && key < multipliers.Length ? multipliers[key] : 1f;
                yield return (name, multiplier);
            }
        }

        private static int IndexOfKey(string name)
        {
            var names = FaceTrackingGroupTable.GetKeyNames();
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
