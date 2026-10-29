namespace NativeFTManager
{
    /// <summary>
    /// FaceEmoの抑制の発動条件（仕様書10.3、F-17）
    ///
    /// 値はシリアライズされるので、並びを変えずに末尾へ足す
    /// </summary>
    public enum FaceEmoSuppressionTrigger
    {
        /// <summary>FaceTracking ∧ (EyeTracking ∨ LipTracking)。Jerry'sのFacialExpressionsDisabledと同じ意味</summary>
        EyeOrLip = 0,

        /// <summary>FaceTracking ∧ LipTracking。目だけのトラッキングではFaceEmoを止めない</summary>
        LipOnly = 1,
    }

    /// <summary>
    /// 追加パラメーターの指定方法
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraParameterSource
    {
        /// <summary>MA Menu Itemからパラメーターを読む</summary>
        MenuItem = 0,

        /// <summary>パラメーター名を直接書く</summary>
        ParameterName = 1,
    }

    /// <summary>
    /// メニューアイテムを切り替える先の状態
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraToggleState
    {
        On = 0,
        Off = 1,
    }

    /// <summary>
    /// 直接指定したパラメーターの型
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraParameterType
    {
        Bool = 0,
        Int = 1,
        Float = 2,
    }

    /// <summary>
    /// フェイストラッキングを無効にしたときの扱い
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraReleaseMode
    {
        /// <summary>解除時の値を書き込む</summary>
        Revert = 0,

        /// <summary>何も書き込まず、フェイストラッキング中の値を残す</summary>
        Keep = 1,

        /// <summary>フェイストラッキングを有効にする直前の値へ戻す</summary>
        Restore = 2,
    }

    /// <summary>
    /// 追加パラメーターの同期の扱い
    ///
    /// 同期パラメーターは装着者のクライアントだけで書き、値は同期でほかの人へ届ける。
    /// 同期しないパラメーターは、各クライアントがそれぞれ書く。
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraSyncMode
    {
        /// <summary>Expression Parameters、MA Parameters、MA Menu Itemの設定から判定する</summary>
        Auto = 0,

        Synced = 1,

        Unsynced = 2,
    }
}
