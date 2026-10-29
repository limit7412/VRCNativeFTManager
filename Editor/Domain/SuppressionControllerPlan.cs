using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// FaceEmoの抑制のFXレイヤーの生成計画（仕様書10.3）
    ///
    /// 閾値やDriverの値といった判断はすべてSuppressionControllerPlannerが行い、
    /// Infra側のSuppressionControllerWriterはこの計画を機械的に実体化するだけになる。
    /// FE-JsT BridgeのBridgeControllerPlanを移したもの。
    /// 発動条件が論理和を含むので、遷移の条件はトグルの条件（ToggleConditionPlan）の論理積で持ち、
    /// 論理和は遷移を分けて表す
    /// </summary>
    internal sealed class SuppressionControllerPlan
    {
        public SuppressionControllerPlan(
            IReadOnlyList<SuppressionParameterPlan> parameters,
            IReadOnlyList<SuppressionLayerPlan> layers)
        {
            Parameters = parameters;
            Layers = layers;
        }

        public static SuppressionControllerPlan Empty =>
            new SuppressionControllerPlan(new SuppressionParameterPlan[0], new SuppressionLayerPlan[0]);

        public IReadOnlyList<SuppressionParameterPlan> Parameters { get; }

        public IReadOnlyList<SuppressionLayerPlan> Layers { get; }

        public bool IsEmpty => Layers.Count == 0;

        public SuppressionLayerPlan FindLayer(string name)
        {
            return Layers.FirstOrDefault(layer => layer.Name == name);
        }
    }

    /// <summary>コントローラに宣言するパラメーター</summary>
    internal sealed class SuppressionParameterPlan
    {
        public SuppressionParameterPlan(string name, SuppressionParameterType type, float defaultValue = 0f)
        {
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
        }

        public string Name { get; }

        public SuppressionParameterType Type { get; }

        /// <summary>
        /// コントローラ上の既定値。公開パラメーターはExpression Parametersの既定値が優先されるが、
        /// トグルのコントローラと宣言が食い違わないよう同じ値にそろえる
        /// </summary>
        public float DefaultValue { get; }
    }

    internal enum SuppressionParameterType
    {
        Bool,
        Float,
        Int,
    }

    /// <summary>レイヤー1つ分の定義</summary>
    internal sealed class SuppressionLayerPlan
    {
        public SuppressionLayerPlan(
            string name,
            string defaultStateName,
            IReadOnlyList<SuppressionStatePlan> states,
            IReadOnlyList<SuppressionTransitionPlan> transitions)
        {
            Name = name;
            DefaultStateName = defaultStateName;
            States = states;
            Transitions = transitions;
        }

        public string Name { get; }

        public string DefaultStateName { get; }

        public IReadOnlyList<SuppressionStatePlan> States { get; }

        /// <summary>
        /// レイヤー内の全遷移。同じ遷移元を持つものは記載順がそのまま優先順位になる
        /// </summary>
        public IReadOnlyList<SuppressionTransitionPlan> Transitions { get; }

        public SuppressionStatePlan FindState(string name)
        {
            return States.FirstOrDefault(state => state.Name == name);
        }

        public IReadOnlyList<SuppressionTransitionPlan> TransitionsFrom(string stateName)
        {
            return Transitions.Where(transition => transition.From == stateName).ToArray();
        }
    }

    /// <summary>ステート1つ分の定義</summary>
    internal sealed class SuppressionStatePlan
    {
        public SuppressionStatePlan(string name, float motionLengthSeconds, SuppressionDriverPlan driver = null)
        {
            Name = name;
            MotionLengthSeconds = motionLengthSeconds;
            Driver = driver;
        }

        public string Name { get; }

        /// <summary>割り当てる空クリップの長さ。同じ長さのクリップはコントローラ内で共有される</summary>
        public float MotionLengthSeconds { get; }

        public SuppressionDriverPlan Driver { get; }
    }

    /// <summary>VRCAvatarParameterDriverの定義</summary>
    internal sealed class SuppressionDriverPlan
    {
        public SuppressionDriverPlan(bool localOnly, IReadOnlyList<SuppressionDriverEntry> entries)
        {
            LocalOnly = localOnly;
            Entries = entries;
        }

        /// <summary>
        /// 同期パラメーターへ書くDriverはtrueで生成する。リモートでも書くと、書き込んだ値と届いた同期値が競合する。
        /// 同期しないパラメーターへ書くDriverはfalseで生成し、各クライアントが同じ結果へ到達するようにする
        /// </summary>
        public bool LocalOnly { get; }

        public IReadOnlyList<SuppressionDriverEntry> Entries { get; }
    }

    /// <summary>
    /// Driverが書き込むパラメーターと値の組
    /// Sourceを持つものは、値の代わりにSourceの現在値を写す (Copy)
    /// </summary>
    internal sealed class SuppressionDriverEntry
    {
        public SuppressionDriverEntry(string parameter, float value)
        {
            Parameter = parameter;
            Value = value;
        }

        private SuppressionDriverEntry(string parameter, string source)
        {
            Parameter = parameter;
            Source = source;
        }

        /// <summary>sourceの現在値をparameterへ写すエントリ</summary>
        public static SuppressionDriverEntry Copy(string source, string parameter)
        {
            return new SuppressionDriverEntry(parameter, source);
        }

        /// <summary>書き込み先のパラメーター</summary>
        public string Parameter { get; }

        /// <summary>書き込む値。Copyでは参照しない</summary>
        public float Value { get; }

        /// <summary>写し元のパラメーター。nullなら値を書く (Set)</summary>
        public string Source { get; }

        public bool IsCopy => Source != null;
    }

    /// <summary>
    /// 遷移1本分の定義。条件は論理積
    ///
    /// 条件が無く、Exit Timeも無い遷移はUnityでは発火しないので、条件が常に真の遷移は
    /// Exit Time 0で作る（発動条件をメニューに出さないトグルだけで組んだとき）
    /// </summary>
    internal sealed class SuppressionTransitionPlan
    {
        public SuppressionTransitionPlan(
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

        /// <summary>正規化時間で表した発火位置。HasExitTimeがfalseのときは参照されない</summary>
        public float ExitTime { get; }
    }

    /// <summary>
    /// 生成計画の入力となる設定値
    /// コンポーネントから切り離すことで、計画の構築をMonoBehaviourなしでテストできる
    /// </summary>
    internal sealed class SuppressionSettings
    {
        public SuppressionSettings(
            FaceEmoControlMethod controlMethod,
            FaceEmoSuppressionTrigger trigger,
            int faceEmoteIndex,
            bool controlFaceEmo = true,
            IReadOnlyList<ExtraParameterTarget> extraParameters = null)
        {
            ControlMethod = controlMethod;
            Trigger = trigger;

            // FaceEmoの表情番号は0から振られるため、負の値はDriverへ渡さない
            FaceEmoteIndex = System.Math.Max(0, faceEmoteIndex);
            ControlFaceEmo = controlFaceEmo;
            ExtraParameters = extraParameters ?? new ExtraParameterTarget[0];
        }

        public FaceEmoControlMethod ControlMethod { get; }

        public FaceEmoSuppressionTrigger Trigger { get; }

        /// <summary>表情制御方式で切り替え先にする表情番号</summary>
        public int FaceEmoteIndex { get; }

        /// <summary>
        /// FaceEmoを止めるレイヤーを作るか。ビルドでは常にtrueにする（FaceEmoの検出は見落としうるため）。
        /// falseなら追加パラメーターだけを書く
        /// </summary>
        public bool ControlFaceEmo { get; }

        /// <summary>フェイストラッキング有効中に追加で書き込むパラメーター</summary>
        public IReadOnlyList<ExtraParameterTarget> ExtraParameters { get; }
    }

    /// <summary>
    /// 設定値からFaceEmoの抑制の生成計画を組み立てる（FE-JsT BridgeのBridgePlanBuilderを移したもの）
    ///
    /// 移さなかったもの: Tracking Controlの再適用（確認項目2で決める）。
    /// Jerry'sとFaceEmoの適用順の逆転を直すためのもので、Jerry'sの無いネイティブFTでは前提が変わる
    /// </summary>
    internal static class SuppressionControllerPlanner
    {
        public const string ExpressionControlLayerName = NativeFTManagerParameterNames.LayerPrefix + "FaceEmo ExpressionControl";
        public const string BypassLayerName = NativeFTManagerParameterNames.LayerPrefix + "FaceEmo Bypass";
        public const string ExtraParametersLayerName = NativeFTManagerParameterNames.LayerPrefix + "ExtraParameters";
        public const string UnsyncedExtraParametersLayerName = NativeFTManagerParameterNames.LayerPrefix + "ExtraParametersUnsynced";

        public const string IdleStateName = "Idle";
        public const string BypassStateName = "Bypass";
        public const string EngagedStateName = "Engaged";
        public const string RedriveStateName = "Redrive";
        public const string InitialStateName = "Initial";
        public const string ReleasedStateName = "Released";
        public const string ReassertStateName = "Reassert";

        /// <summary>全ステートで共有する空クリップの長さ</summary>
        public const float DefaultClipLengthSeconds = 1.0f;

        public static SuppressionControllerPlan Build(
            SuppressionSettings settings,
            ToggleSet toggles,
            FaceEmoParameterNames faceEmo)
        {
            var trigger = SuppressionTrigger.Build(settings.Trigger, toggles);

            // 決して発動しないなら、どのレイヤーも何も書かない。解除時の値を書き込むステートも
            // 発動の後にしか入らないので、レイヤーごと作らない
            if (trigger.IsNeverEngaged)
            {
                return SuppressionControllerPlan.Empty;
            }

            var on = ToConditions(trigger.OnTerms, toggles);
            var off = ToConditions(trigger.OffTerms, toggles);

            var parameters = new List<SuppressionParameterPlan>();
            foreach (var toggle in toggles.Dynamic)
            {
                if (UsesToggle(trigger, toggle.Toggle))
                {
                    parameters.Add(new SuppressionParameterPlan(
                        toggle.ParameterName, SuppressionParameterType.Bool, toggle.DefaultValue ? 1f : 0f));
                }
            }

            var layers = new List<SuppressionLayerPlan>();

            // 出力先のパラメーターは方式ごとに違うため、使う分だけを宣言する
            if (settings.ControlFaceEmo)
            {
                if (settings.ControlMethod == FaceEmoControlMethod.ExpressionControl)
                {
                    parameters.Add(new SuppressionParameterPlan(faceEmo.EmoteLockEnable, SuppressionParameterType.Bool));
                    parameters.Add(new SuppressionParameterPlan(faceEmo.ForceBlinkDisable, SuppressionParameterType.Bool));
                    parameters.Add(new SuppressionParameterPlan(faceEmo.Emote, SuppressionParameterType.Int));
                    layers.Add(BuildExpressionControlLayer(settings, faceEmo, on, off));
                }
                else
                {
                    parameters.Add(new SuppressionParameterPlan(
                        FaceEmoParameterNames.ForceBypassEnable, SuppressionParameterType.Bool));
                    layers.Add(BuildBypassLayer(on, off));
                }
            }

            var extraParameters = settings.ExtraParameters;
            if (extraParameters.Count > 0)
            {
                DeclareExtraParameters(parameters, extraParameters);

                // 同期の扱いでDriverのlocalOnlyと書き直しの要否が変わるため、レイヤーを分ける
                var synced = extraParameters.Where(target => target.Synced).ToArray();
                var unsynced = extraParameters.Where(target => !target.Synced).ToArray();

                if (synced.Length > 0)
                {
                    layers.Add(BuildExtraParametersLayer(synced, on, off));
                }

                if (unsynced.Length > 0)
                {
                    layers.Add(BuildUnsyncedExtraParametersLayer(unsynced, on, off));
                }
            }

            if (layers.Count == 0)
            {
                return SuppressionControllerPlan.Empty;
            }

            return new SuppressionControllerPlan(parameters, layers);
        }

        private static bool UsesToggle(SuppressionTrigger trigger, NativeFTToggle toggle)
        {
            return trigger.OnTerms.Concat(trigger.OffTerms).Any(term => term.Any(literal => literal.Toggle == toggle));
        }

        private static IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> ToConditions(
            IReadOnlyList<IReadOnlyList<ToggleLiteral>> terms,
            ToggleSet toggles)
        {
            return terms
                .Select(term => (IReadOnlyList<ToggleConditionPlan>)term
                    .Select(literal => new ToggleConditionPlan(toggles[literal.Toggle].ParameterName, literal.Expected))
                    .ToArray())
                .ToArray();
        }

        /// <summary>
        /// 条件の論理和を、項ごとの遷移に分ける。常に真の項はExit Time 0の遷移にする
        /// </summary>
        private static IEnumerable<SuppressionTransitionPlan> When(
            string from,
            string to,
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> terms)
        {
            foreach (var term in terms)
            {
                yield return term.Count > 0
                    ? new SuppressionTransitionPlan(from, to, term)
                    : new SuppressionTransitionPlan(from, to, term, hasExitTime: true, exitTime: 0f);
            }
        }

        /// <summary>空クリップを1周したら抜ける、条件の無い遷移</summary>
        private static SuppressionTransitionPlan AfterLoop(string from, string to)
        {
            return new SuppressionTransitionPlan(from, to, new ToggleConditionPlan[0], hasExitTime: true, exitTime: 1.0f);
        }

        /// <summary>
        /// FaceEmoを動かしたまま、表情の書き込みを無害な状態へ寄せるレイヤー
        ///
        /// 表情ロックでジェスチャーによる表情の切り替えを止め、まばたきを強制的に停止し、
        /// メニューの表情選択と同じパラメーターで指定の表情へ切り替える。
        /// FaceEmoは書き込みを続けるため、FaceEmoより前にいる素体の表情レイヤーは
        /// 通常時と同じく押さえ込まれたままになる。
        ///
        /// バイパス方式と違い、周期的な書き直しは行わない。
        /// 書き込む先がすべて同期パラメーターであり、値そのものが後からjoinした人へ届くためである。
        /// 書き直し続けると、装着者がメニューで選び直した表情を毎周期奪ってしまう。
        ///
        /// Driverはlocal onlyで生成する。同期パラメーターをリモートでも書くと、
        /// 書き込んだ値と届いた同期値が競合する。FaceEmo自身も同じ理由でローカル駆動している
        /// </summary>
        private static SuppressionLayerPlan BuildExpressionControlLayer(
            SuppressionSettings settings,
            FaceEmoParameterNames faceEmo,
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> on,
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> off)
        {
            var states = new[]
            {
                new SuppressionStatePlan(IdleStateName, DefaultClipLengthSeconds, ReleaseDriver(faceEmo)),
                new SuppressionStatePlan(EngagedStateName, DefaultClipLengthSeconds, EngageDriver(faceEmo, settings.FaceEmoteIndex)),
            };

            var transitions = new List<SuppressionTransitionPlan>();
            transitions.AddRange(When(IdleStateName, EngagedStateName, on));
            transitions.AddRange(When(EngagedStateName, IdleStateName, off));

            return new SuppressionLayerPlan(ExpressionControlLayerName, IdleStateName, states, transitions);
        }

        /// <summary>
        /// 発動条件をCN_FORCE_BYPASS_ENABLEへ写すレイヤー
        ///
        /// 写しは一度きりにせず、空クリップを1周するたびにRedriveを経由して
        /// 元のステートへ入り直し、Driverを発火し直す。アバターのロード中は
        /// Driverの書き込みが失われることがあり、ステートが目的地に着いたまま
        /// 値だけが既定へ戻ると、装着者がトラッキングを切り替え直すまで
        /// バイパスが成立しない。FaceEmo側の遷移はこの値そのものを条件に持つため、
        /// 書き直しさえ届けば以降の連鎖はFaceEmoの中で復旧する。
        /// 同じ値の書き直しは無害で、非同期パラメーターのため同期帯域も使わない。
        ///
        /// 入り直しに自己遷移を使わないのは、通常遷移の自己遷移で突入時の挙動が
        /// 再実行されるかをUnityが仕様として明言していないため
        /// </summary>
        private static SuppressionLayerPlan BuildBypassLayer(
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> on,
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> off)
        {
            var states = new[]
            {
                new SuppressionStatePlan(IdleStateName, DefaultClipLengthSeconds, BypassDriver(0f)),
                new SuppressionStatePlan(BypassStateName, DefaultClipLengthSeconds, BypassDriver(1f)),
                new SuppressionStatePlan(RedriveStateName, DefaultClipLengthSeconds),
            };

            var transitions = new List<SuppressionTransitionPlan>();
            transitions.AddRange(When(IdleStateName, BypassStateName, on));
            transitions.AddRange(When(BypassStateName, IdleStateName, off));

            // 空クリップを1周したらRedriveへ抜ける。条件付きの遷移を記載順の
            // 先に置いてあるため、発動条件の切替はこのループより優先される
            transitions.Add(AfterLoop(IdleStateName, RedriveStateName));
            transitions.Add(AfterLoop(BypassStateName, RedriveStateName));

            // Redriveは発動条件の現在値に合うステートへ即座に戻る中継で、
            // 戻った先の突入でDriverが発火し直す
            transitions.AddRange(When(RedriveStateName, BypassStateName, on));
            transitions.AddRange(When(RedriveStateName, IdleStateName, off));

            return new SuppressionLayerPlan(BypassLayerName, IdleStateName, states, transitions);
        }

        /// <summary>
        /// 追加パラメーターと、その退避先を宣言に加える
        ///
        /// 宣言済みの名前と、追加パラメーター同士で重なる名前は宣言し直さない。
        /// AnimatorControllerは同名のパラメーターを追加すると別名へ付け替えるため、
        /// 重ねて宣言するとDriverが書く相手と別のパラメーターができてしまう。
        ///
        /// 退避先はExpression Parametersに載らないため、アニメーターだけのパラメーターになる。
        /// 型を書き込み先と揃え、Copyで値が変換されないようにする
        /// </summary>
        private static void DeclareExtraParameters(
            List<SuppressionParameterPlan> parameters, IReadOnlyList<ExtraParameterTarget> extraParameters)
        {
            var declared = new HashSet<string>(parameters.Select(parameter => parameter.Name));

            foreach (var target in extraParameters)
            {
                if (declared.Add(target.Name))
                {
                    parameters.Add(new SuppressionParameterPlan(target.Name, target.Type));
                }
            }

            foreach (var target in extraParameters)
            {
                if (target.Restore && declared.Add(target.StashName))
                {
                    parameters.Add(new SuppressionParameterPlan(target.StashName, target.Type));
                }
            }
        }

        /// <summary>
        /// 発動条件に合わせて、同期パラメーターを書き込むレイヤー
        ///
        /// 既定のステートはDriverを持たないInitialにする。
        /// 解除時の値を書くステートを既定にすると、アバターを読み込むたびに解除時の値が書かれ、
        /// 装着者が保存していたトグルの状態を上書きしてしまう。
        /// Initialから出る遷移は発動したときだけなので、解除時の値が書かれるのは
        /// フェイストラッキングを一度有効にしたあとに限られる。
        ///
        /// 表情制御方式と同じく、周期的な書き直しは行わない。
        /// フェイストラッキング中でも、装着者がメニューから選び直した値を奪わないためである
        /// </summary>
        private static SuppressionLayerPlan BuildExtraParametersLayer(
            IReadOnlyList<ExtraParameterTarget> extraParameters,
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> on,
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> off)
        {
            var states = new[]
            {
                new SuppressionStatePlan(InitialStateName, DefaultClipLengthSeconds),
                new SuppressionStatePlan(
                    EngagedStateName,
                    DefaultClipLengthSeconds,
                    ExtraDriver(localOnly: true, EngageEntries(extraParameters, stash: true))),
                new SuppressionStatePlan(
                    ReleasedStateName,
                    DefaultClipLengthSeconds,
                    ExtraDriver(localOnly: true, ReleaseEntries(extraParameters))),
            };

            var transitions = new List<SuppressionTransitionPlan>();
            transitions.AddRange(When(InitialStateName, EngagedStateName, on));
            transitions.AddRange(When(EngagedStateName, ReleasedStateName, off));
            transitions.AddRange(When(ReleasedStateName, EngagedStateName, on));

            return new SuppressionLayerPlan(ExtraParametersLayerName, InitialStateName, states, transitions);
        }

        /// <summary>
        /// 発動条件に合わせて、同期しないパラメーターを書き込むレイヤー
        ///
        /// 同期しないパラメーターは値がほかの人へ届かないため、Driverを全クライアントで走らせ、
        /// 各クライアントが同じ値へ到達するようにする。
        ///
        /// 発動している間は、空クリップを1周するたびにRedriveを経由してReassertへ入り、
        /// 値を書き直す。後からjoinした人のクライアントでは、アバターのロード中に
        /// Driverの書き込みが失われることがあり、同期しない値は同期で補われないためである。
        ///
        /// 書き直すのは値だけで、退避はEngagedへ入ったときの一度に限る。
        /// Reassertでも退避すると、フェイストラッキング中の値を退避してしまう。
        /// 解除時の書き込みは書き直さない。解除後の値は、ほかのギミックやメニューの持ち物である
        /// </summary>
        private static SuppressionLayerPlan BuildUnsyncedExtraParametersLayer(
            IReadOnlyList<ExtraParameterTarget> extraParameters,
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> on,
            IReadOnlyList<IReadOnlyList<ToggleConditionPlan>> off)
        {
            var states = new[]
            {
                new SuppressionStatePlan(InitialStateName, DefaultClipLengthSeconds),
                new SuppressionStatePlan(
                    EngagedStateName,
                    DefaultClipLengthSeconds,
                    ExtraDriver(localOnly: false, EngageEntries(extraParameters, stash: true))),
                new SuppressionStatePlan(
                    ReassertStateName,
                    DefaultClipLengthSeconds,
                    ExtraDriver(localOnly: false, EngageEntries(extraParameters, stash: false))),
                new SuppressionStatePlan(RedriveStateName, DefaultClipLengthSeconds),
                new SuppressionStatePlan(
                    ReleasedStateName,
                    DefaultClipLengthSeconds,
                    ExtraDriver(localOnly: false, ReleaseEntries(extraParameters))),
            };

            var transitions = new List<SuppressionTransitionPlan>();
            transitions.AddRange(When(InitialStateName, EngagedStateName, on));

            // 解除の遷移を記載順の先に置き、書き直しのループより優先させる
            transitions.AddRange(When(EngagedStateName, ReleasedStateName, off));
            transitions.Add(AfterLoop(EngagedStateName, RedriveStateName));
            transitions.AddRange(When(ReassertStateName, ReleasedStateName, off));
            transitions.Add(AfterLoop(ReassertStateName, RedriveStateName));

            // Redriveは発動条件の現在値に合う側へ即座に抜ける中継である
            transitions.AddRange(When(RedriveStateName, ReassertStateName, on));
            transitions.AddRange(When(RedriveStateName, ReleasedStateName, off));

            transitions.AddRange(When(ReleasedStateName, EngagedStateName, on));

            return new SuppressionLayerPlan(UnsyncedExtraParametersLayerName, InitialStateName, states, transitions);
        }

        /// <summary>
        /// 発動したときに書くエントリ
        /// 退避するときは、すべての退避を書き込みより前に並べる。Driverはエントリを記載順に処理する
        /// </summary>
        private static List<SuppressionDriverEntry> EngageEntries(
            IReadOnlyList<ExtraParameterTarget> extraParameters, bool stash)
        {
            var entries = new List<SuppressionDriverEntry>();

            if (stash)
            {
                foreach (var target in extraParameters)
                {
                    if (target.Restore)
                    {
                        entries.Add(SuppressionDriverEntry.Copy(target.Name, target.StashName));
                    }
                }
            }

            foreach (var target in extraParameters)
            {
                entries.Add(new SuppressionDriverEntry(target.Name, target.EngagedValue));
            }

            return entries;
        }

        /// <summary>解除したときに書くエントリ。復元は退避先から写し戻す</summary>
        private static List<SuppressionDriverEntry> ReleaseEntries(IReadOnlyList<ExtraParameterTarget> extraParameters)
        {
            var entries = new List<SuppressionDriverEntry>();

            foreach (var target in extraParameters)
            {
                if (target.Restore)
                {
                    entries.Add(SuppressionDriverEntry.Copy(target.StashName, target.Name));
                }
                else if (target.ReleasedValue.HasValue)
                {
                    entries.Add(new SuppressionDriverEntry(target.Name, target.ReleasedValue.Value));
                }
            }

            return entries;
        }

        /// <summary>書くものがなければDriverを付けない</summary>
        private static SuppressionDriverPlan ExtraDriver(bool localOnly, List<SuppressionDriverEntry> entries)
        {
            return entries.Count > 0 ? new SuppressionDriverPlan(localOnly, entries) : null;
        }

        private static SuppressionDriverPlan BypassDriver(float value)
        {
            return new SuppressionDriverPlan(
                localOnly: false,
                entries: new[] { new SuppressionDriverEntry(FaceEmoParameterNames.ForceBypassEnable, value) });
        }

        /// <summary>表情ロックとまばたき停止を起こし、指定の表情へ切り替えるDriver</summary>
        private static SuppressionDriverPlan EngageDriver(FaceEmoParameterNames faceEmo, int emoteIndex)
        {
            return new SuppressionDriverPlan(
                localOnly: true,
                entries: new[]
                {
                    new SuppressionDriverEntry(faceEmo.EmoteLockEnable, 1f),
                    new SuppressionDriverEntry(faceEmo.ForceBlinkDisable, 1f),
                    new SuppressionDriverEntry(faceEmo.Emote, emoteIndex),
                });
        }

        /// <summary>
        /// 表情ロックとまばたき停止を元へ戻すDriver
        ///
        /// 表情番号は戻さない。戻す先を覚える手立てがなく、ロックが外れれば
        /// FaceEmoがジェスチャーから決め直すためである
        /// </summary>
        private static SuppressionDriverPlan ReleaseDriver(FaceEmoParameterNames faceEmo)
        {
            return new SuppressionDriverPlan(
                localOnly: true,
                entries: new[]
                {
                    new SuppressionDriverEntry(faceEmo.EmoteLockEnable, 0f),
                    new SuppressionDriverEntry(faceEmo.ForceBlinkDisable, 0f),
                });
        }
    }
}
