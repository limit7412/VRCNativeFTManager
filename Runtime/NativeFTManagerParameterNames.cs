namespace NativeFTManager
{
    /// <summary>
    /// 本製品が定義する同期パラメーター、Set、FXレイヤーの名前
    ///
    /// 接頭辞はここにだけ書き、ほかの名前はすべて接頭辞から組み立てる。
    /// 接頭辞は仕様書17章のU-1で <c>KxNFTM/</c> に確定した。
    ///
    /// 名前は保存値と利用者のアニメーターから参照されるので変えない。
    /// 変えると、保存したトグルの値が既定値へ戻り、名前を参照するアニメーターも効かなくなる。
    /// 誤って変えたときに気付けるよう、名前の値は <c>ParameterNamesTests</c> で固定している。
    /// MAの内部パラメーター（自動リネーム）にしないのも同じ理由による。
    /// </summary>
    public static class NativeFTManagerParameterNames
    {
        /// <summary>パラメーターとSetの名前の接頭辞</summary>
        public const string Prefix = "KxNFTM/";

        /// <summary>FXレイヤーと生成物のオブジェクト名に使う接頭辞（区切りの / を含まない）</summary>
        public const string ShortPrefix = "KxNFTM";

        /// <summary>FT全体のトグル</summary>
        public const string FaceTracking = Prefix + "FaceTracking";

        /// <summary>目のトグル</summary>
        public const string EyeTracking = Prefix + "EyeTracking";

        /// <summary>口のトグル</summary>
        public const string LipTracking = Prefix + "LipTracking";

        /// <summary>瞳孔のトグル（M2）</summary>
        public const string Pupil = Prefix + "Pupil";

        /// <summary>ビゼームのトグル（M2）</summary>
        public const string Visemes = Prefix + "Visemes";

        /// <summary>エミュレーションのトグル（M2）</summary>
        public const string Emulation = Prefix + "Emulation";

        /// <summary>制限のトグル（M2）</summary>
        public const string Limits = Prefix + "Limits";

        /// <summary>Eye Syncのトグル（M2）</summary>
        public const string EyeSync = Prefix + "EyeSync";

        /// <summary>目の感度（M4、Float）</summary>
        public const string SensitivityEye = Prefix + "Sensitivity/Eye";

        /// <summary>口の感度（M4、Float）</summary>
        public const string SensitivityLip = Prefix + "Sensitivity/Lip";

        /// <summary>分割後のSetの名前の接頭辞。後ろにグループ名が付く（例: KxNFTM/Lip）</summary>
        public const string SetPrefix = Prefix;

        /// <summary>ゲートSetの名前の接頭辞（M2。例: KxNFTM/Gate/Emulation）</summary>
        public const string GateSetPrefix = Prefix + "Gate/";

        /// <summary>生成するFXレイヤーの名前の接頭辞。後ろにグループ名が付く（例: KxNFTM Lip）</summary>
        public const string LayerPrefix = ShortPrefix + " ";
    }
}
