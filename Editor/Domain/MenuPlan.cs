using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// EXメニューの生成計画（仕様書8.3）
    ///
    /// 表示名はVRChatのメニューに出るので、エディタの言語とは関係なく英語で固定する。
    /// 日本語のメニューを選べるようにするかはM4で見直す
    /// </summary>
    internal sealed class MenuPlan
    {
        public const string SubMenuName = "Face Tracking";

        public MenuPlan(IReadOnlyList<MenuTogglePlan> toggles)
        {
            Toggles = toggles;
        }

        public IReadOnlyList<MenuTogglePlan> Toggles { get; }

        public bool IsEmpty => Toggles.Count == 0;

        public static MenuPlan Build(ToggleSet toggles)
        {
            return new MenuPlan(toggles.Dynamic
                .Select(toggle => new MenuTogglePlan(GetLabel(toggle.Toggle), toggle.ParameterName))
                .ToArray());
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
