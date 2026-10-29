using System;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// フェイストラッキング有効中に追加で書き込むパラメーターのうち、名前と値を解決し終えたもの（仕様書10.3、F-18）
    ///
    /// 名前はMAのリネームを反映したあとの、FXで使われる名前を持つ。
    /// 生成計画はこの値をそのまま写すだけで、メニューアイテムやMA Parametersは参照しない。
    /// FE-JsT Bridgeの同名の型を移したもの
    /// </summary>
    internal sealed class ExtraParameterTarget
    {
        /// <summary>
        /// 退避先の名前の接頭辞
        /// この接頭辞で始まる名前は本製品の予約名であり、追加パラメーターの書き込み先には使えない。
        /// 使えると、ある項目の退避先と別の項目の書き込み先が同じ名前になり、退避した値が上書きされる
        /// </summary>
        public const string StashPrefix = NativeFTManagerParameterNames.Prefix + "Stash/";

        /// <param name="name">FXで使われる名前</param>
        /// <param name="type">パラメーターの型</param>
        /// <param name="engagedValue">発動したときに書く値</param>
        /// <param name="releasedValue">解除したときに書く値。restoreがtrueのときは無視する</param>
        /// <param name="restore">解除したときに、発動する直前の値へ戻すか</param>
        /// <param name="synced">同期パラメーターか</param>
        public ExtraParameterTarget(
            string name,
            SuppressionParameterType type,
            float engagedValue,
            float? releasedValue,
            bool restore = false,
            bool synced = true)
        {
            Name = name;
            Type = type;
            EngagedValue = NormalizeValue(type, engagedValue);
            Restore = restore;
            ReleasedValue = !restore && releasedValue.HasValue
                ? NormalizeValue(type, releasedValue.Value)
                : (float?)null;
            Synced = synced;
        }

        public string Name { get; }

        public SuppressionParameterType Type { get; }

        /// <summary>発動したときに書く値</summary>
        public float EngagedValue { get; }

        /// <summary>
        /// 解除したときに書く値
        /// nullなら値は書かない。Restoreがtrueのときは、値の代わりに退避しておいた値を書き戻す
        /// </summary>
        public float? ReleasedValue { get; }

        /// <summary>発動する直前の値を退避し、解除したときに書き戻すか</summary>
        public bool Restore { get; }

        /// <summary>
        /// 同期パラメーターか
        /// 同期パラメーターは装着者のクライアントだけで書き、同期しないパラメーターは各クライアントで書く
        /// </summary>
        public bool Synced { get; }

        /// <summary>
        /// 退避先のパラメーター名
        /// 生成するコントローラにだけ宣言し、Expression Parametersには載せない
        /// </summary>
        public string StashName => StashPrefix + Name;

        public static bool IsReservedName(string name)
        {
            return name != null && name.StartsWith(StashPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 直接指定の設定から組み立てる
        /// </summary>
        public static ExtraParameterTarget FromDirect(
            string name,
            ExtraParameterType type,
            float engagedValue,
            ExtraReleaseMode releaseMode,
            float releasedValue,
            bool synced = true)
        {
            return new ExtraParameterTarget(
                name,
                ToSuppressionType(type),
                engagedValue,
                releaseMode == ExtraReleaseMode.Revert ? releasedValue : (float?)null,
                releaseMode == ExtraReleaseMode.Restore,
                synced);
        }

        /// <summary>
        /// メニューアイテムの設定から組み立てる
        ///
        /// ONはトグルの値を、OFFは0を書く。VRChatのトグルは、選ばれたときに自分の値を書き、
        /// 選択が外れたときに0を書くので、それと同じ値になる。
        /// 解除時の値を書く設定 (Revert) では、フェイストラッキング中と反対の状態へ戻す
        /// </summary>
        public static ExtraParameterTarget FromMenuItem(
            string name,
            SuppressionParameterType type,
            float toggleValue,
            ExtraToggleState engagedState,
            ExtraReleaseMode releaseMode,
            bool synced = true)
        {
            var onValue = toggleValue;
            const float offValue = 0f;

            var engagedValue = engagedState == ExtraToggleState.On ? onValue : offValue;
            var releasedValue = engagedState == ExtraToggleState.On ? offValue : onValue;

            return new ExtraParameterTarget(
                name,
                type,
                engagedValue,
                releaseMode == ExtraReleaseMode.Revert ? releasedValue : (float?)null,
                releaseMode == ExtraReleaseMode.Restore,
                synced);
        }

        /// <summary>
        /// 型に合わない値を、Driverに渡せる値へ揃える
        /// Boolは0以外を1へ、Intは最も近い整数へ丸める（偶数丸めでUnityのMathf.Roundと同じ）
        /// </summary>
        public static float NormalizeValue(SuppressionParameterType type, float value)
        {
            switch (type)
            {
                case SuppressionParameterType.Bool:
                    return value == 0f ? 0f : 1f;
                case SuppressionParameterType.Int:
                    return (float)Math.Round(value);
                default:
                    return value;
            }
        }

        public static SuppressionParameterType ToSuppressionType(ExtraParameterType type)
        {
            switch (type)
            {
                case ExtraParameterType.Int:
                    return SuppressionParameterType.Int;
                case ExtraParameterType.Float:
                    return SuppressionParameterType.Float;
                default:
                    return SuppressionParameterType.Bool;
            }
        }
    }
}
