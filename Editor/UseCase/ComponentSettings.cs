using System.Collections.Generic;
using NativeFTManager.Domain;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// コンポーネントの設定をDomainの型へ写す
    ///
    /// DomainはUnityの型に触れないので、MonoBehaviourから値を取り出すのはここで行う
    /// </summary>
    internal static class ComponentSettings
    {
        public static CorrectionFeatures GetFeatures(NativeFTManagerComponent component)
        {
            return new CorrectionFeatures(component.enableEmulation, component.enableLimits, component.enableEyeSync);
        }

        public static ToggleSet ResolveToggles(NativeFTManagerComponent component)
        {
            return ToggleSet.Resolve(component.GetToggle, GetFeatures(component));
        }

        /// <summary>
        /// 個別に外した表情補正の規則のid
        /// </summary>
        public static HashSet<string> GetExcludedRules(NativeFTManagerComponent component)
        {
            return new HashSet<string>(component.excludedRules ?? new List<string>());
        }
    }
}
