using System;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// EXメニューの項目に付ける既定のアイコン（仕様書8.3）。
    /// 画像はRuntime/Iconsに同梱し、読み込みはMenuIconsが担う
    /// </summary>
    internal enum MenuIcon
    {
        None = 0,
        FaceTrackingMenu,
        FaceTracking,
        EyeTracking,
        LipTracking,
        Pupil,
        Visemes,
        Settings,
        Emulation,
        Limits,
        EyeSync,
        Sensitivity,
        SensitivityEye,
        SensitivityLip,
    }

    internal static class MenuIcons
    {
        public static MenuIcon For(NativeFTToggle toggle)
        {
            switch (toggle)
            {
                case NativeFTToggle.FaceTracking:
                    return MenuIcon.FaceTracking;
                case NativeFTToggle.EyeTracking:
                    return MenuIcon.EyeTracking;
                case NativeFTToggle.LipTracking:
                    return MenuIcon.LipTracking;
                case NativeFTToggle.Pupil:
                    return MenuIcon.Pupil;
                case NativeFTToggle.Visemes:
                    return MenuIcon.Visemes;
                case NativeFTToggle.Emulation:
                    return MenuIcon.Emulation;
                case NativeFTToggle.Limits:
                    return MenuIcon.Limits;
                case NativeFTToggle.EyeSync:
                    return MenuIcon.EyeSync;
                default:
                    throw new ArgumentOutOfRangeException(nameof(toggle), toggle, null);
            }
        }

        public static MenuIcon For(SensitivityChannel channel)
        {
            return channel == SensitivityChannel.Eye ? MenuIcon.SensitivityEye : MenuIcon.SensitivityLip;
        }
    }
}
