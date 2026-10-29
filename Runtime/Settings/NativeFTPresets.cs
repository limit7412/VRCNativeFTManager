using System.Collections.Generic;

namespace NativeFTManager
{
    /// <summary>
    /// プリセットの中身（仕様書6.3）
    ///
    /// プリセットはトグルの構成と、エミュレーション・制限・Eye Syncを生成するかの組で決まる。
    /// 既定値はJerry's 7.0.5のExpression Parametersに合わせる（U-4の推奨。エミュレーションと制限は有効、Eye Syncは無効）。
    /// Pupilの既定値は、Jerry'sでは無効（EyeDilationEnable = 0）だが、仕様書7.1に合わせて有効にしている
    /// </summary>
    public static class NativeFTPresets
    {
        /// <summary>
        /// プリセットのトグル。Customは中身を持たないのでUEと同じものを返す
        /// </summary>
        public static List<ToggleSettings> CreateToggles(NativeFTPreset preset)
        {
            switch (preset)
            {
                case NativeFTPreset.ARKit:
                    return new List<ToggleSettings>
                    {
                        new ToggleSettings(NativeFTToggle.FaceTracking, true, true),
                        new ToggleSettings(NativeFTToggle.EyeTracking, true, true),
                        new ToggleSettings(NativeFTToggle.LipTracking, true, true),

                        // ARKitのシェイプに瞳孔は無いので、メニューに出さない
                        new ToggleSettings(NativeFTToggle.Pupil, false, true),
                        new ToggleSettings(NativeFTToggle.Visemes, true, true),
                        new ToggleSettings(NativeFTToggle.Emulation, true, true),
                        new ToggleSettings(NativeFTToggle.Limits, true, true),
                        new ToggleSettings(NativeFTToggle.EyeSync, true, false),
                    };
                case NativeFTPreset.Minimal:
                    return new List<ToggleSettings>
                    {
                        new ToggleSettings(NativeFTToggle.FaceTracking, true, true),
                        new ToggleSettings(NativeFTToggle.EyeTracking, true, true),
                        new ToggleSettings(NativeFTToggle.LipTracking, true, true),
                        new ToggleSettings(NativeFTToggle.Pupil, false, true),
                        new ToggleSettings(NativeFTToggle.Visemes, false, true),
                        new ToggleSettings(NativeFTToggle.Emulation, false, false),
                        new ToggleSettings(NativeFTToggle.Limits, false, false),
                        new ToggleSettings(NativeFTToggle.EyeSync, false, false),
                    };
                default:
                    return new List<ToggleSettings>
                    {
                        new ToggleSettings(NativeFTToggle.FaceTracking, true, true),
                        new ToggleSettings(NativeFTToggle.EyeTracking, true, true),
                        new ToggleSettings(NativeFTToggle.LipTracking, true, true),
                        new ToggleSettings(NativeFTToggle.Pupil, true, true),
                        new ToggleSettings(NativeFTToggle.Visemes, true, true),
                        new ToggleSettings(NativeFTToggle.Emulation, true, true),
                        new ToggleSettings(NativeFTToggle.Limits, true, true),
                        new ToggleSettings(NativeFTToggle.EyeSync, true, false),
                    };
            }
        }

        /// <summary>
        /// プリセットでエミュレーション・制限・Eye Syncを生成するか
        /// </summary>
        public static bool EnablesCorrections(NativeFTPreset preset)
        {
            return preset != NativeFTPreset.Minimal;
        }

        /// <summary>
        /// コンポーネントへプリセットの中身を書く。Customは何も変えない
        /// </summary>
        public static void Apply(NativeFTManagerComponent component, NativeFTPreset preset)
        {
            component.preset = preset;
            if (preset == NativeFTPreset.Custom)
            {
                return;
            }

            component.toggles = CreateToggles(preset);
            bool corrections = EnablesCorrections(preset);
            component.enableEmulation = corrections;
            component.enableLimits = corrections;
            component.enableEyeSync = corrections;
            component.excludedRules = new List<string>();
        }

        /// <summary>
        /// コンポーネントの設定がプリセットの中身と一致するか。
        /// 保存の有無（saved）はプリセットで決めないので比べない
        /// </summary>
        public static bool Matches(NativeFTManagerComponent component, NativeFTPreset preset)
        {
            if (preset == NativeFTPreset.Custom)
            {
                return true;
            }

            bool corrections = EnablesCorrections(preset);
            if (component.enableEmulation != corrections
                || component.enableLimits != corrections
                || component.enableEyeSync != corrections
                || (component.excludedRules != null && component.excludedRules.Count > 0))
            {
                return false;
            }

            foreach (var expected in CreateToggles(preset))
            {
                var actual = component.GetToggle(expected.toggle);
                if (actual.showInMenu != expected.showInMenu || actual.defaultValue != expected.defaultValue)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
