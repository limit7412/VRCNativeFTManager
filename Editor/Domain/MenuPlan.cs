using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// EXメニューの生成計画（仕様書8.3）
    ///
    /// 表示名はVRChatのメニューに出るので、エディタの言語とは関係なく英語で固定する。
    /// Jerry'sのメニューも英語なので、移行した利用者が迷わないようにそろえている（M4で見直し、変えないことにした）
    /// </summary>
    internal sealed class MenuPlan
    {
        public const string SubMenuName = "Face Tracking";

        public const string SettingsMenuName = "Settings";

        public const string SensitivityMenuName = "Sensitivity";

        public MenuPlan(
            IReadOnlyList<MenuTogglePlan> toggles,
            IReadOnlyList<MenuTogglePlan> settings = null,
            IReadOnlyList<MenuTogglePlan> sensitivity = null)
        {
            Toggles = toggles;
            Settings = settings ?? new MenuTogglePlan[0];
            Sensitivity = sensitivity ?? new MenuTogglePlan[0];
        }

        /// <summary>「Face Tracking」サブメニューの直下に並べるトグル</summary>
        public IReadOnlyList<MenuTogglePlan> Toggles { get; }

        /// <summary>「Settings」サブメニューに並べる表情補正のトグル</summary>
        public IReadOnlyList<MenuTogglePlan> Settings { get; }

        /// <summary>「Sensitivity」サブメニューに並べるRadial（仕様書5.6）。パラメーターはFloat</summary>
        public IReadOnlyList<MenuTogglePlan> Sensitivity { get; }

        public bool IsEmpty => Toggles.Count == 0 && Settings.Count == 0 && Sensitivity.Count == 0;

        public static MenuPlan Build(ToggleSet toggles)
        {
            var main = toggles.Dynamic.Where(toggle => !IsSetting(toggle.Toggle));
            var settings = toggles.Dynamic.Where(toggle => IsSetting(toggle.Toggle));
            return new MenuPlan(
                main.Select(toggle => new MenuTogglePlan(GetLabel(toggle.Toggle), toggle.ParameterName)).ToArray(),
                settings.Select(toggle => new MenuTogglePlan(GetLabel(toggle.Toggle), toggle.ParameterName)).ToArray(),
                toggles.Sensitivity.Channels
                    .Select(channel => new MenuTogglePlan(
                        SensitivityConfig.GetLabel(channel), SensitivityConfig.GetParameterName(channel)))
                    .ToArray());
        }

        /// <summary>Settingsサブメニューに入れるトグル（仕様書8.3）</summary>
        public static bool IsSetting(NativeFTToggle toggle)
        {
            return toggle == NativeFTToggle.Emulation
                   || toggle == NativeFTToggle.Limits
                   || toggle == NativeFTToggle.EyeSync;
        }

        public static string GetLabel(NativeFTToggle toggle)
        {
            switch (toggle)
            {
                case NativeFTToggle.FaceTracking:
                    return "Face Tracking";
                case NativeFTToggle.EyeTracking:
                    return "Eye Tracking";
                case NativeFTToggle.LipTracking:
                    return "Lip Tracking";
                case NativeFTToggle.Pupil:
                    return "Pupil";
                case NativeFTToggle.Visemes:
                    return "Visemes";
                case NativeFTToggle.Emulation:
                    return "Emulation";
                case NativeFTToggle.Limits:
                    return "Limits";
                case NativeFTToggle.EyeSync:
                    return "Eye Sync";
                default:
                    throw new ArgumentOutOfRangeException(nameof(toggle), toggle, null);
            }
        }
    }

    internal sealed class MenuTogglePlan
    {
        public MenuTogglePlan(string label, string parameter)
        {
            Label = label;
            Parameter = parameter;
        }

        public string Label { get; }

        public string Parameter { get; }
    }
}
