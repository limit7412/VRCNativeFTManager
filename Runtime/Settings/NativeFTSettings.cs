using System;
using System.Collections.Generic;

namespace NativeFTManager
{
    /// <summary>
    /// 設定のまとまり（仕様書6.3）
    ///
    /// M1ではトグルの構成がどれも同じで、ARKitかどうかをGenerator連携の警告に使うだけである。
    /// 表情補正の既定値の違いはM2で足す。
    /// </summary>
    public enum NativeFTPreset
    {
        UE = 0,
        ARKit = 1,
        Minimal = 2,
        Custom = 3,
    }

    /// <summary>
    /// メニューから操作するトグルの種類
    ///
    /// 値はシリアライズされるので、並びを変えずに末尾へ足す
    /// </summary>
    public enum NativeFTToggle
    {
        FaceTracking = 0,
        EyeTracking = 1,
        LipTracking = 2,
    }

    /// <summary>
    /// トグル1つ分の設定
    /// </summary>
    [Serializable]
    public class ToggleSettings
    {
        public NativeFTToggle toggle;

        /// <summary>
        /// メニューに出すか。出さないトグルはパラメーターを作らず、条件では常に既定値として扱う
        /// </summary>
        public bool showInMenu = true;

        /// <summary>パラメーターの既定値</summary>
        public bool defaultValue = true;

        /// <summary>ワールドをまたいで値を保存するか</summary>
        public bool saved = true;

        public ToggleSettings()
        {
        }

        public ToggleSettings(NativeFTToggle toggle)
        {
            this.toggle = toggle;
        }

        /// <summary>
        /// すべてのトグルをメニューに出し、既定で有効にした設定
        /// </summary>
        public static List<ToggleSettings> CreateDefaults()
        {
            var list = new List<ToggleSettings>();
            foreach (NativeFTToggle toggle in Enum.GetValues(typeof(NativeFTToggle)))
            {
                list.Add(new ToggleSettings(toggle));
            }

            return list;
        }
    }

    /// <summary>
    /// DescriptorのViseme Dampenをビルド時にどう書くか（仕様書5.7）
    /// </summary>
    public enum VisemeDampenOverride
    {
        /// <summary>Descriptorの値を変えない</summary>
        DoNotChange = 0,
        None = 1,
        DampenTracking = 2,
        DampenVisemes = 3,
    }

    /// <summary>
    /// ARKit BlendShape Generatorとの連携（仕様書10.1）
    /// </summary>
    public enum GeneratorIntegration
    {
        /// <summary>何もしない</summary>
        Off = 0,

        /// <summary>アバターにGeneratorがあれば、ビルド用の複製の上でSet書き込みを有効にする</summary>
        Auto = 1,
    }
}
