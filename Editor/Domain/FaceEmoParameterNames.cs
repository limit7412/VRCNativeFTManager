using System.Collections.Generic;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// FaceEmoの抑制が書き込むFaceEmo側のパラメーター名（仕様書10.3）
    ///
    /// いずれもFaceEmoの内部名であり、FaceEmoの更新時に再検証が必要な接点である。
    /// 書き込む3つには、FaceEmoが外部連携用として維持する約束がない。
    ///
    /// 3つはいずれもFaceEmoがMA Parametersへ登録しており、AddParameterPrefix設定が有効なアバターでは
    /// FaceEmo_ を前置した名前へリネームされる。リネームはMA Parametersに登録された名前にしか働かないため、
    /// 生成するコントローラにはリネーム後の名前を宣言しなければ接続されない。
    /// FaceEmoの検出に使うCN_FORCE_BYPASS_ENABLEはMA Parametersに登録されないため、この解決の対象にならない。
    /// FE-JsT Bridgeの同名の型を移したもの
    /// </summary>
    internal readonly struct FaceEmoParameterNames
    {
        /// <summary>
        /// FaceEmoのバイパスを起動する外部連携用パラメーター。抑制では書き込まず、FaceEmoの検出にだけ使う
        /// </summary>
        public const string ForceBypassEnable = "CN_FORCE_BYPASS_ENABLE";

        /// <summary>
        /// FaceEmoの表情ロック。メニューの「表情固定ON」が書き込むものと同じ。
        /// trueの間はジェスチャーによる表情の切り替えが止まる
        /// </summary>
        public const string RawEmoteLockEnable = "CN_EMOTE_LOCK_ENABLE";

        /// <summary>
        /// FaceEmoのまばたき強制停止。メニューの「まばたきOFF」が書き込むものと同じ。
        /// BLINKレイヤーはこれがtrueなら無条件で停止側へ遷移するため、
        /// 表情ステートがまばたきの有効を駆動し直しても止まったままになる
        /// </summary>
        public const string RawForceBlinkDisable = "SYNC_CN_FORCE_BLINK_DISABLE";

        /// <summary>
        /// FaceEmoの再生中の表情番号。メニューの「表情選択」が書き込むものと同じ。
        /// FaceEmo側でこの値を書き直すのはジェスチャーが変化した瞬間だけなので、
        /// 外から書いた値は次の変化まで保たれる。表情ロック中はその変化自体が起きない
        /// </summary>
        public const string RawEmote = "SYNC_EM_EMOTE";

        public FaceEmoParameterNames(string emoteLockEnable, string forceBlinkDisable, string emote, bool resolved)
        {
            EmoteLockEnable = emoteLockEnable;
            ForceBlinkDisable = forceBlinkDisable;
            Emote = emote;
            Resolved = resolved;
        }

        public string EmoteLockEnable { get; }

        public string ForceBlinkDisable { get; }

        public string Emote { get; }

        /// <summary>
        /// FaceEmoのMA Parametersから名前を引けたか
        ///
        /// 引けなかった場合はFaceEmoの生の名前を持つ。
        /// プレフィックスを使っていないアバターならそれで正しく接続されるが、
        /// 使っているアバターでは何も起きないため、ビルド時に警告する材料にする
        /// </summary>
        public bool Resolved { get; }

        /// <summary>リネームを解決できなかったときに使うFaceEmoの生の名前</summary>
        public static FaceEmoParameterNames Raw =>
            new FaceEmoParameterNames(RawEmoteLockEnable, RawForceBlinkDisable, RawEmote, resolved: false);

        /// <summary>
        /// MA Parametersの登録内容から名前を解決する
        /// </summary>
        /// <param name="parameterRemaps">
        /// MA Parametersコンポーネント1つにつき1件の、元の名前からリネーム後の名前への対応。
        /// リネームしない登録は、元の名前をそのまま値に持つ
        /// </param>
        /// <remarks>
        /// 3つすべてを登録しているコンポーネントだけをFaceEmoのものとみなす。
        /// FaceEmoは制御パラメーターを1つのMA Parametersへまとめて登録するため、
        /// 一部だけを持つコンポーネントは別のツールのものである
        /// </remarks>
        public static FaceEmoParameterNames Resolve(
            IEnumerable<IReadOnlyDictionary<string, string>> parameterRemaps)
        {
            if (parameterRemaps == null)
            {
                return Raw;
            }

            foreach (var remap in parameterRemaps)
            {
                if (remap == null)
                {
                    continue;
                }

                if (!remap.TryGetValue(RawEmoteLockEnable, out var emoteLockEnable)
                    || !remap.TryGetValue(RawForceBlinkDisable, out var forceBlinkDisable)
                    || !remap.TryGetValue(RawEmote, out var emote))
                {
                    continue;
                }

                return new FaceEmoParameterNames(emoteLockEnable, forceBlinkDisable, emote, resolved: true);
            }

            return Raw;
        }
    }
}
