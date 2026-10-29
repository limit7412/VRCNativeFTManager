using System;
using System.Collections.Generic;

namespace NativeFTManager
{
    /// <summary>
    /// 設定のまとまり（仕様書6.3）
    ///
    /// 中身（トグルの構成と表情補正の有無）は <see cref="NativeFTPresets"/> に置く。
    /// どのまとまりとも一致しない設定はCustomになる
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
        Pupil = 3,
        Visemes = 4,
        Emulation = 5,
        Limits = 6,
        EyeSync = 7,
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

        public ToggleSettings(NativeFTToggle toggle, bool showInMenu, bool defaultValue)
        {
            this.toggle = toggle;
            this.showInMenu = showInMenu;
            this.defaultValue = defaultValue;
        }

        /// <summary>
        /// 設定がリストに無いトグルの扱い。メニューに出さず、M1までの挙動を変えない値に固定する。
        /// M1の版で保存したコンポーネントはPupil以降を持たないため、ここで挙動が決まる
        /// </summary>
        public static ToggleSettings CreateMissing(NativeFTToggle toggle)
        {
            // Eye SyncはM1に無かったので無効のまま。ほかは有効で、Setの重みを変えない側になる。
            // エミュレーションと制限は、コンポーネントのenableEmulation / enableLimitsが既定でfalseなので生成されない
            return new ToggleSettings(toggle, false, toggle != NativeFTToggle.EyeSync);
        }

        /// <summary>
        /// UEプリセットのトグル（すべてメニューに出す）
        /// </summary>
        public static List<ToggleSettings> CreateDefaults()
        {
            return NativeFTPresets.CreateToggles(NativeFTPreset.UE);
        }

        public ToggleSettings Clone()
        {
            return new ToggleSettings(toggle, showInMenu, defaultValue) { saved = saved };
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
