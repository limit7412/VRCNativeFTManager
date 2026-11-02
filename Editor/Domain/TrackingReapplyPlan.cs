using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// Tracking Controlの再適用のレイヤーの計画（仕様書10.3、F-22）
    ///
    /// VRC Animator Tracking ControlでEyes &amp; EyelidsやMouth &amp; JawをAnimationにすると、
    /// ネイティブFTの視線と口も止まる（確認項目2、2026-11-02の実機）。アバターのFXやFaceEmoの表情が
    /// Animationにしたまま戻さないと、FT中でも視線や口が動かない。
    /// FT中は目と口をTrackingへ周期的に戻し、後から入ったAnimationを上書きし直す。
    ///
    /// Tracking Controlはステートに入ったときに一度だけ効くので、Applyのステートに入り直して繰り返す。
    /// 同じ値の再適用は見た目を変えないので、繰り返しても害はない。
    /// FTが無効の部位はNo Changeのままにし、アバター側の切り替えを妨げない
    /// </summary>
    internal sealed class TrackingReapplyPlan
    {
        public const string LayerName = NativeFTManagerParameterNames.LayerPrefix + "TrackingReapply";

        /// <summary>入り直しを待つステート。条件の組み合わせに応じてApplyかIdleへ抜ける</summary>
        public const string ArmedState = "Armed";

        /// <summary>目も口もFTが無効の間にいるステート。Tracking Controlを持たない</summary>
        public const string IdleState = "Idle";

        /// <summary>
        /// Applyにとどまる長さ（秒）。この間隔で再適用する。
        /// アバター側がAnimationにしてからFTの動きが戻るまでの遅れの上限になる
        /// </summary>
        public const float IntervalSeconds = 0.5f;

        public TrackingReapplyPlan(
            IReadOnlyList<TrackingReapplyStatePlan> states,
            IReadOnlyList<TrackingReapplyTransitionPlan> transitions)
        {
            States = states;
            Transitions = transitions;
        }

        /// <summary>ステート。先頭がArmedで、既定のステートになる</summary>
        public IReadOnlyList<TrackingReapplyStatePlan> States { get; }

        /// <summary>遷移。同じ遷移元を持つものは記載順がそのまま優先順位になる</summary>
        public IReadOnlyList<TrackingReapplyTransitionPlan> Transitions { get; }

        public IReadOnlyList<TrackingReapplyTransitionPlan> TransitionsFrom(string state)
        {
            return Transitions.Where(transition => transition.From == state).ToArray();
        }

        public static string GetApplyStateName(bool eyes, bool mouth)
        {
            if (eyes && mouth)
            {
                return "Apply Eyes+Mouth";
            }

            return eyes ? "Apply Eyes" : "Apply Mouth";
        }
    }

    internal sealed class TrackingReapplyStatePlan
    {
        public TrackingReapplyStatePlan(string name, bool eyes, bool mouth, bool loops)
        {
            Name = name;
            Eyes = eyes;
            Mouth = mouth;
            Loops = loops;
        }

        public string Name { get; }

        /// <summary>Eyes &amp; EyelidsをTrackingにするか。falseならNo Change</summary>
        public bool Eyes { get; }

        /// <summary>Mouth &amp; JawをTrackingにするか。falseならNo Change</summary>
        public bool Mouth { get; }

        /// <summary>
        /// IntervalSecondsの長さの空のクリップを持ち、終わるとArmedへ戻るか（Applyのステート）
        /// </summary>
        public bool Loops { get; }

        public bool HasTrackingControl => Eyes || Mouth;
    }

    /// <summary>
    /// 遷移1本。条件は論理積。Has Exit Timeありの遷移は、ステートを1周（正規化時間1）したときに抜ける
    /// </summary>
    internal sealed class TrackingReapplyTransitionPlan
    {
        public TrackingReapplyTransitionPlan(
            string from,
            string to,
            IReadOnlyList<ToggleConditionPlan> conditions,
            bool hasExitTime = false,
            float exitTime = 0f)
        {
            From = from;
            To = to;
            Conditions = conditions;
            HasExitTime = hasExitTime;
            ExitTime = exitTime;
        }

        public string From { get; }

        public string To { get; }

        public IReadOnlyList<ToggleConditionPlan> Conditions { get; }

        public bool HasExitTime { get; }

        public float ExitTime { get; }
    }

    /// <summary>
    /// トグルの設定から再適用のレイヤーを組み立てる
    /// </summary>
    internal static class TrackingReapplyPlanner
    {
        /// <summary>
        /// 目を戻す条件はEyeのSetと同じ（FT ∧ Eye）、口はLipのSetと同じ（FT ∧ Lip）。
        /// メニューに出さないトグルは既定値で畳む。目も口も決して戻さない設定ならnullを返す
        /// </summary>
        public static TrackingReapplyPlan Build(ToggleSet toggles)
        {
            var eyes = new Part(GroupCondition.For(FaceTrackingGroup.Eye).Simplify(toggles), toggles);
            var mouth = new Part(GroupCondition.For(FaceTrackingGroup.Lip).Simplify(toggles), toggles);

            var combinations = eyes.Values
                .SelectMany(eye => mouth.Values.Select(lip => (eye, lip)))
                .ToArray();
            if (combinations.All(combination => !combination.eye && !combination.lip))
            {
                return null;
            }

            var states = new List<TrackingReapplyStatePlan>
            {
                new TrackingReapplyStatePlan(TrackingReapplyPlan.ArmedState, false, false, false),
            };
            foreach (var (eye, lip) in combinations)
            {
                states.Add(new TrackingReapplyStatePlan(GetStateName(eye, lip), eye, lip, eye || lip));
            }

            var transitions = new List<TrackingReapplyTransitionPlan>();

            // Armedからは、いまの組み合わせのステートへすぐ抜ける。組み合わせどうしは排他なので並びは問わない。
            // 条件が1つも無い組み合わせ（どちらも常に成り立つか常に成り立たない）は、
            // 条件の無い遷移を作れないので終了時間0で抜ける
            foreach (var (eye, lip) in combinations)
            {
                foreach (var eyeTerm in eyes.Terms(eye))
                {
                    foreach (var lipTerm in mouth.Terms(lip))
                    {
                        var conditions = Combine(eyeTerm, lipTerm);
                        if (conditions == null)
                        {
                            continue;
                        }

                        transitions.Add(new TrackingReapplyTransitionPlan(
                            TrackingReapplyPlan.ArmedState,
                            GetStateName(eye, lip),
                            conditions,
                            hasExitTime: conditions.Length == 0));
                    }
                }
            }

            // 組み合わせが変わったら、周期を待たずにArmedへ戻って選び直す。
            // 条件付きの遷移を先に並べ、周期のループより優先させる
            foreach (var (eye, lip) in combinations)
            {
                string from = GetStateName(eye, lip);
                var added = new HashSet<string>();
                foreach (var term in eyes.Changes(eye).Concat(mouth.Changes(lip)))
                {
                    // 目と口で同じ条件（Face Trackingのオフなど）は1本にする
                    if (added.Add(string.Join("&", term.Select(c => (c.Expected ? "" : "!") + c.Parameter))))
                    {
                        transitions.Add(new TrackingReapplyTransitionPlan(from, TrackingReapplyPlan.ArmedState, term));
                    }
                }

                if (eye || lip)
                {
                    // 空のクリップを1周したらArmedへ戻り、Applyに入り直してTracking Controlを適用し直す
                    transitions.Add(new TrackingReapplyTransitionPlan(
                        from,
                        TrackingReapplyPlan.ArmedState,
                        new ToggleConditionPlan[0],
                        hasExitTime: true,
                        exitTime: 1f));
                }
            }

            return new TrackingReapplyPlan(states, transitions);
        }

        /// <summary>
        /// 2つの項の論理積。目と口の条件はFace Trackingを共有するので、重なる条件は1つにまとめ、
        /// 同じパラメーターに逆の値を求める（決して成り立たない）組み合わせはnullを返す
        /// </summary>
        private static ToggleConditionPlan[] Combine(ToggleConditionPlan[] first, ToggleConditionPlan[] second)
        {
            var combined = new List<ToggleConditionPlan>();
            foreach (var condition in first.Concat(second))
            {
                var existing = combined.FirstOrDefault(entry => entry.Parameter == condition.Parameter);
                if (existing == null)
                {
                    combined.Add(condition);
                }
                else if (existing.Expected != condition.Expected)
                {
                    return null;
                }
            }

            return combined.ToArray();
        }

        private static string GetStateName(bool eye, bool lip)
        {
            return eye || lip ? TrackingReapplyPlan.GetApplyStateName(eye, lip) : TrackingReapplyPlan.IdleState;
        }

        /// <summary>
        /// 目か口の一方。畳んだ後の条件から、取りうる値と、その値になる条件（論理和標準形の項）を出す
        /// </summary>
        private sealed class Part
        {
            private readonly GroupCondition _condition;
            private readonly ToggleSet _toggles;

            public Part(GroupCondition condition, ToggleSet toggles)
            {
                _condition = condition;
                _toggles = toggles;
            }

            /// <summary>取りうる値。条件がnull（常に偽）ならfalseだけ、空（常に真）ならtrueだけ</summary>
            public IEnumerable<bool> Values
            {
                get
                {
                    if (_condition == null)
                    {
                        return new[] { false };
                    }

                    return _condition.Literals.Count == 0 ? new[] { true } : new[] { true, false };
                }
            }

            /// <summary>値がvalueになる条件の項。成り立つ側は論理積1項、成り立たない側は否定ごとの項</summary>
            public IEnumerable<ToggleConditionPlan[]> Terms(bool value)
            {
                if (_condition == null || _condition.Literals.Count == 0)
                {
                    return new[] { new ToggleConditionPlan[0] };
                }

                return value
                    ? new[] { _condition.Literals.Select(ToPlan).ToArray() }
                    : _condition.Literals.Select(literal => new[] { ToPlan(literal.Negate()) }).ToArray();
            }

            /// <summary>値がvalueから変わる条件の項。値が変わりえないなら無い</summary>
            public IEnumerable<ToggleConditionPlan[]> Changes(bool value)
            {
                if (_condition == null || _condition.Literals.Count == 0)
                {
                    return new ToggleConditionPlan[0][];
                }

                return Terms(!value);
            }

            private ToggleConditionPlan ToPlan(ToggleLiteral literal)
            {
                return new ToggleConditionPlan(_toggles[literal.Toggle].ParameterName, literal.Expected);
            }
        }
    }
}
