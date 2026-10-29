using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// トグルのFXレイヤーの生成計画（仕様書5.3）
    ///
    /// 条件の組み立てはすべてToggleControllerPlannerが行い、Infra側はこの計画を機械的に実体化する。
    /// Setの名前はGeneratingの時点では決まらない（Generatorが後からSetを書く）ので、計画はグループで持ち、
    /// State BehaviorはTransformingで分割した後に、実際のSetに合わせて付ける
    /// </summary>
    internal sealed class ToggleControllerPlan
    {
        public ToggleControllerPlan(
            IReadOnlyList<ToggleParameterPlan> parameters,
            IReadOnlyList<ToggleLayerPlan> layers,
            IReadOnlyDictionary<FaceTrackingGroup, float> defaultWeights,
            IReadOnlyDictionary<string, float> gateWeights = null,
            VisemeLayerPlan visemeLayer = null,
            IReadOnlyList<FloatParameterPlan> floatParameters = null)
        {
            Parameters = parameters;
            FloatParameters = floatParameters ?? new FloatParameterPlan[0];
            Layers = layers;
            DefaultWeights = defaultWeights;
            GateWeights = gateWeights ?? new Dictionary<string, float>();
            VisemeLayer = visemeLayer;
        }

        /// <summary>コントローラに宣言するBoolパラメーター</summary>
        public IReadOnlyList<ToggleParameterPlan> Parameters { get; }

        /// <summary>コントローラに宣言するFloatパラメーター（感度のRadial、仕様書5.6）</summary>
        public IReadOnlyList<FloatParameterPlan> FloatParameters { get; }

        /// <summary>Setの重みを切り替えるレイヤー（グループとゲート）</summary>
        public IReadOnlyList<ToggleLayerPlan> Layers { get; }

        /// <summary>
        /// グループごとのSetの既定の重み（setWeight）。トグルの既定値で条件を評価した値
        /// </summary>
        public IReadOnlyDictionary<FaceTrackingGroup, float> DefaultWeights { get; }

        /// <summary>ゲートSetの既定の重み。キーはGateSlot.Key</summary>
        public IReadOnlyDictionary<string, float> GateWeights { get; }

        /// <summary>ビゼームを切り替えるレイヤー（仕様書5.5）。切り替えないならnull</summary>
        public VisemeLayerPlan VisemeLayer { get; }

        /// <summary>グループを受け持つレイヤー。トグルで変わらないグループはnull</summary>
        public ToggleLayerPlan FindLayer(FaceTrackingGroup group)
        {
            return Layers.FirstOrDefault(layer => layer.Groups.Contains(group));
        }

        /// <summary>ゲートを受け持つレイヤー</summary>
        public ToggleLayerPlan FindGateLayer(string gateKey)
        {
            return Layers.FirstOrDefault(layer => layer.Gates.Any(gate => gate.Key == gateKey));
        }
    }

    /// <summary>
    /// ビゼームのレイヤー。On / OffステートのVRC Animator Tracking ControlでMouth &amp; Jawを
    /// Tracking（ビゼーム有効）とAnimation（ビゼーム無効）に切り替える。
    /// トグルをメニューに出さず既定で無効にしたときは、Offだけの固定のレイヤーにする
    /// </summary>
    internal sealed class VisemeLayerPlan
    {
        public const string LayerName = NativeFTManagerParameterNames.LayerPrefix + "Visemes";

        public VisemeLayerPlan(ToggleLayerPlan layer, bool alwaysOff)
        {
            Layer = layer;
            AlwaysOff = alwaysOff;
        }

        /// <summary>遷移の計画。AlwaysOffのときは遷移を持たない</summary>
        public ToggleLayerPlan Layer { get; }

        public bool AlwaysOff { get; }
    }

    internal sealed class FloatParameterPlan
    {
        public FloatParameterPlan(string name, float defaultValue, bool saved)
        {
            Name = name;
            DefaultValue = defaultValue;
            Saved = saved;
        }

        public string Name { get; }

        public float DefaultValue { get; }

        public bool Saved { get; }
    }

    internal sealed class ToggleParameterPlan
    {
        public ToggleParameterPlan(string name, bool defaultValue)
        {
            Name = name;
            DefaultValue = defaultValue;
        }

        public string Name { get; }

        public bool DefaultValue { get; }
    }

    /// <summary>
    /// Setの重みを1より小さい値で書くステート（感度の段階）と、その重み
    /// </summary>
    internal sealed class ToggleOnState
    {
        public ToggleOnState(string name, float weight)
        {
            Name = name;
            Weight = weight;
        }

        public string Name { get; }

        /// <summary>このステートで書くSetの重み</summary>
        public float Weight { get; }
    }

    /// <summary>
    /// レイヤー1つ。条件が同じグループは1つのレイヤーにまとめ、ステートにはグループのSetの数だけBehaviorを付ける
    /// </summary>
    internal sealed class ToggleLayerPlan
    {
        public const string InitState = "Init";
        public const string OnState = "On";
        public const string OffState = "Off";

        public ToggleLayerPlan(
            string name,
            IReadOnlyList<FaceTrackingGroup> groups,
            IReadOnlyList<ToggleTransitionPlan> transitions,
            IReadOnlyList<GateSlot> gates = null,
            IReadOnlyList<ToggleOnState> onStates = null,
            SensitivityChannel? sensitivity = null)
        {
            Name = name;
            Groups = groups;
            Transitions = transitions;
            Gates = gates ?? new GateSlot[0];
            OnStates = onStates ?? new[] { new ToggleOnState(OnState, 1f) };
            Sensitivity = sensitivity;
        }

        /// <summary>
        /// 条件が成り立つ間にいるステート。感度の無いレイヤーはOn（重み1）だけで、
        /// 感度のあるレイヤーは段階ごとのステート（On 0〜On 7）になる（仕様書5.6）
        /// </summary>
        public IReadOnlyList<ToggleOnState> OnStates { get; }

        /// <summary>このレイヤーの感度の系統。感度を掛けないならnull</summary>
        public SensitivityChannel? Sensitivity { get; }

        public string Name { get; }

        public IReadOnlyList<FaceTrackingGroup> Groups { get; }

        /// <summary>このレイヤーが重みを切り替えるゲートSet</summary>
        public IReadOnlyList<GateSlot> Gates { get; }

        /// <summary>
        /// レイヤー内の全遷移。同じ遷移元を持つものは記載順がそのまま優先順位になる
        /// </summary>
        public IReadOnlyList<ToggleTransitionPlan> Transitions { get; }

        public IReadOnlyList<ToggleTransitionPlan> TransitionsFrom(string state)
        {
            return Transitions.Where(transition => transition.From == state).ToArray();
        }
    }

    /// <summary>
    /// 遷移1本。条件は論理積で、Has Exit Timeなしで作る。長さは既定で0（感度の段階どうしだけ短い長さを取る）
    /// </summary>
    internal sealed class ToggleTransitionPlan
    {
        public ToggleTransitionPlan(
            string from,
            string to,
            IReadOnlyList<ToggleConditionPlan> conditions,
            float duration = 0f)
        {
            From = from;
            To = to;
            Conditions = conditions;
            Duration = duration;
        }

        /// <summary>遷移の長さ（秒）</summary>
        public float Duration { get; }

        public string From { get; }

        public string To { get; }

        public IReadOnlyList<ToggleConditionPlan> Conditions { get; }
    }

    internal enum ToggleConditionMode
    {
        /// <summary>BoolがExpectedのとき（If / IfNot）</summary>
        Bool,

        /// <summary>FloatがThresholdより大きいとき</summary>
        Greater,

        /// <summary>FloatがThresholdより小さいとき</summary>
        Less,
    }

    /// <summary>遷移の条件1つ。Boolパラメーターがexpectedのとき（If / IfNot）か、Floatの比較</summary>
    internal sealed class ToggleConditionPlan
    {
        public ToggleConditionPlan(string parameter, bool expected)
        {
            Parameter = parameter;
            Expected = expected;
            Mode = ToggleConditionMode.Bool;
        }

        private ToggleConditionPlan(string parameter, ToggleConditionMode mode, float threshold)
        {
            Parameter = parameter;
            Mode = mode;
            Threshold = threshold;
            Expected = true;
        }

        public static ToggleConditionPlan Greater(string parameter, float threshold)
        {
            return new ToggleConditionPlan(parameter, ToggleConditionMode.Greater, threshold);
        }

        public static ToggleConditionPlan Less(string parameter, float threshold)
        {
            return new ToggleConditionPlan(parameter, ToggleConditionMode.Less, threshold);
        }

        public string Parameter { get; }

        /// <summary>Boolの条件で期待する値。Floatの比較では常にtrue</summary>
        public bool Expected { get; }

        public ToggleConditionMode Mode { get; }

        /// <summary>Floatの比較の閾値</summary>
        public float Threshold { get; }
    }

    /// <summary>
    /// トグルの設定からFXレイヤーの計画を組み立てる
    /// </summary>
    internal static class ToggleControllerPlanner
    {
        /// <param name="toggles">トグル</param>
        /// <param name="gates">生成する可能性のあるゲート（CorrectionPlanner.GetPossibleGates）。使わなかったものはApplyで取り除く</param>
        public static ToggleControllerPlan Build(ToggleSet toggles, IEnumerable<GateSlot> gates = null)
        {
            var parameters = toggles.Dynamic
                .Select(toggle => new ToggleParameterPlan(toggle.ParameterName, toggle.DefaultValue))
                .ToArray();
            var sensitivity = toggles.Sensitivity;
            var floatParameters = sensitivity.Channels
                .Select(channel => new FloatParameterPlan(
                    SensitivityConfig.GetParameterName(channel), SensitivityConfig.DefaultValue, sensitivity.Saved))
                .ToArray();

            var defaultWeights = new Dictionary<FaceTrackingGroup, float>();
            var gateWeights = new Dictionary<string, float>();
            var layerSlots = new List<(
                GroupCondition condition,
                SensitivityChannel? channel,
                List<FaceTrackingGroup> groups,
                List<GateSlot> gates)>();

            void AddSlot(GroupCondition condition, FaceTrackingGroup? group, GateSlot gate)
            {
                // ゲートには感度を掛けない。ゲートの重みは補正の効き方そのものなので、0か1のまま切り替える
                var channel = group.HasValue ? sensitivity.GetEnabledChannel(group.Value) : null;

                // メニューに出すトグルが条件に1つも無ければ、重みは既定値のまま変わらない。
                // 条件の無い遷移は作れないので、レイヤーを作らずsetWeightだけで決める。
                // 感度があるときは、Radialの段階で重みが変わるのでレイヤーを作る
                var simplified = condition.Simplify(toggles);
                if (simplified == null || (simplified.Literals.Count == 0 && channel == null))
                {
                    return;
                }

                var existing = layerSlots.FirstOrDefault(
                    entry => entry.condition.SameAs(simplified) && entry.channel == channel);
                if (existing.groups == null)
                {
                    existing = (simplified, channel, new List<FaceTrackingGroup>(), new List<GateSlot>());
                    layerSlots.Add(existing);
                }

                if (group.HasValue)
                {
                    existing.groups.Add(group.Value);
                }

                if (gate != null)
                {
                    existing.gates.Add(gate);
                }
            }

            foreach (var group in FaceTrackingGroupTable.Order)
            {
                var condition = GroupCondition.For(group);
                float onWeight = sensitivity.GetEnabledChannel(group).HasValue ? sensitivity.DefaultStage.Weight : 1f;
                defaultWeights[group] = condition.EvaluateDefaults(toggles) ? onWeight : 0f;
                AddSlot(condition, group, null);
            }

            foreach (var gate in gates ?? new GateSlot[0])
            {
                gateWeights[gate.Key] = gate.Condition.EvaluateDefaults(toggles) ? 1f : 0f;
                AddSlot(gate.Condition, null, gate);
            }

            var layers = layerSlots
                .Select(entry => BuildLayer(
                    GetLayerName(entry.groups.Select(group => group.ToString()).Concat(entry.gates.Select(gate => gate.Key))),
                    entry.condition,
                    entry.groups,
                    entry.gates,
                    toggles,
                    entry.channel))
                .ToArray();

            return new ToggleControllerPlan(
                parameters, layers, defaultWeights, gateWeights, BuildVisemeLayer(toggles), floatParameters);
        }

        private static string GetLayerName(IEnumerable<string> slots)
        {
            return NativeFTManagerParameterNames.LayerPrefix + string.Join("/", slots);
        }

        /// <summary>
        /// ビゼームのレイヤー。トグルをメニューに出すなら切り替え、出さずに既定で無効ならOffに固定する。
        /// 出さずに既定で有効なら、Tracking Controlを置く理由が無いのでレイヤーを作らない
        /// </summary>
        private static VisemeLayerPlan BuildVisemeLayer(ToggleSet toggles)
        {
            var visemes = toggles[NativeFTToggle.Visemes];
            if (!visemes.IsDynamic)
            {
                return visemes.DefaultValue
                    ? null
                    : new VisemeLayerPlan(
                        new ToggleLayerPlan(VisemeLayerPlan.LayerName, new FaceTrackingGroup[0], new ToggleTransitionPlan[0]),
                        true);
            }

            var condition = new GroupCondition(new[] { new ToggleLiteral(NativeFTToggle.Visemes, true) });
            return new VisemeLayerPlan(
                BuildLayer(VisemeLayerPlan.LayerName, condition, new FaceTrackingGroup[0], new GateSlot[0], toggles),
                false);
        }

        /// <summary>
        /// Init / On / Off の3ステート（仕様書5.3）。
        /// Initは読み込み直後に必ずOnかOffへ抜け、一度Behaviorを走らせるために置く。
        /// 論理積はOnへの1本の遷移の条件の並び、Offへの論理和（¬A ∨ ¬B）は条件ごとに遷移を分ける
        /// </summary>
        private static ToggleLayerPlan BuildLayer(
            string name,
            GroupCondition condition,
            IReadOnlyList<FaceTrackingGroup> groups,
            IReadOnlyList<GateSlot> gates,
            ToggleSet toggles,
            SensitivityChannel? channel = null)
        {
            if (channel.HasValue)
            {
                return BuildSensitivityLayer(name, condition, groups, toggles, channel.Value);
            }

            var onConditions = condition.Literals
                .Select(literal => ToConditionPlan(literal, toggles))
                .ToArray();
            var offConditions = condition.Literals
                .Select(literal => ToConditionPlan(literal.Negate(), toggles))
                .ToArray();

            var transitions = new List<ToggleTransitionPlan>
            {
                new ToggleTransitionPlan(ToggleLayerPlan.InitState, ToggleLayerPlan.OnState, onConditions),
            };

            foreach (var off in offConditions)
            {
                transitions.Add(new ToggleTransitionPlan(
                    ToggleLayerPlan.InitState, ToggleLayerPlan.OffState, new[] { off }));
            }

            transitions.Add(new ToggleTransitionPlan(ToggleLayerPlan.OffState, ToggleLayerPlan.OnState, onConditions));

            foreach (var off in offConditions)
            {
                transitions.Add(new ToggleTransitionPlan(
                    ToggleLayerPlan.OnState, ToggleLayerPlan.OffState, new[] { off }));
            }

            return new ToggleLayerPlan(name, groups.ToArray(), transitions, gates.ToArray());
        }

        /// <summary>
        /// 感度のあるレイヤー（仕様書5.6）。Onの代わりに段階ごとのステートを置き、
        /// トグルの条件に加えてRadialの値の範囲で入る段階を決める。
        /// 段階から抜けるときは、Offへの遷移を先に並べてトグルの切り替えを優先する
        /// </summary>
        private static ToggleLayerPlan BuildSensitivityLayer(
            string name,
            GroupCondition condition,
            IReadOnlyList<FaceTrackingGroup> groups,
            ToggleSet toggles,
            SensitivityChannel channel)
        {
            var sensitivity = toggles.Sensitivity;
            string parameter = SensitivityConfig.GetParameterName(channel);
            var onConditions = condition.Literals
                .Select(literal => ToConditionPlan(literal, toggles))
                .ToArray();
            var offConditions = condition.Literals
                .Select(literal => ToConditionPlan(literal.Negate(), toggles))
                .ToArray();

            var stageConditions = sensitivity.Stages
                .ToDictionary(stage => stage.Index, stage => GetRangeConditions(stage, parameter));
            var transitions = new List<ToggleTransitionPlan>();

            foreach (var stage in sensitivity.Stages)
            {
                transitions.Add(new ToggleTransitionPlan(
                    ToggleLayerPlan.InitState, stage.StateName, onConditions.Concat(stageConditions[stage.Index]).ToArray()));
            }

            foreach (var off in offConditions)
            {
                transitions.Add(new ToggleTransitionPlan(
                    ToggleLayerPlan.InitState, ToggleLayerPlan.OffState, new[] { off }));
            }

            foreach (var stage in sensitivity.Stages)
            {
                transitions.Add(new ToggleTransitionPlan(
                    ToggleLayerPlan.OffState, stage.StateName, onConditions.Concat(stageConditions[stage.Index]).ToArray()));
            }

            foreach (var from in sensitivity.Stages)
            {
                foreach (var off in offConditions)
                {
                    transitions.Add(new ToggleTransitionPlan(from.StateName, ToggleLayerPlan.OffState, new[] { off }));
                }

                foreach (var to in sensitivity.Stages.Where(stage => stage.Index != from.Index))
                {
                    transitions.Add(new ToggleTransitionPlan(
                        from.StateName,
                        to.StateName,
                        stageConditions[to.Index],
                        SensitivityConfig.StageTransitionDuration));
                }
            }

            var onStates = sensitivity.Stages
                .Select(stage => new ToggleOnState(stage.StateName, stage.Weight))
                .ToArray();
            return new ToggleLayerPlan(name, groups.ToArray(), transitions, new GateSlot[0], onStates, channel);
        }

        private static ToggleConditionPlan[] GetRangeConditions(SensitivityStage stage, string parameter)
        {
            var conditions = new List<ToggleConditionPlan>();
            if (stage.LowerBound.HasValue)
            {
                conditions.Add(ToggleConditionPlan.Greater(parameter, stage.LowerBound.Value));
            }

            if (stage.UpperBound.HasValue)
            {
                conditions.Add(ToggleConditionPlan.Less(parameter, stage.UpperBound.Value));
            }

            return conditions.ToArray();
        }

        private static ToggleConditionPlan ToConditionPlan(ToggleLiteral literal, ToggleSet toggles)
        {
            return new ToggleConditionPlan(toggles[literal.Toggle].ParameterName, literal.Expected);
        }
    }
}
