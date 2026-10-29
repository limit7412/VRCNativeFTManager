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
        /// <summary>
        /// 生成する表情補正の機能。Eye Syncの規則を個別に外したときは、Eye Sync自体を生成しない扱いにする。
        /// トグルだけが残ると、ONにしたときにEyeClosedのSetが止まり、代わりに出す規則も無いのでまばたきが消えるため
        /// </summary>
        public static CorrectionFeatures GetFeatures(NativeFTManagerComponent component)
        {
            bool eyeSync = component.enableEyeSync
                           && (component.excludedRules == null
                               || !component.excludedRules.Contains(CorrectionRuleTable.EyeSyncRuleId));
            return new CorrectionFeatures(component.enableEmulation, component.enableLimits, eyeSync);
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
