using System.Collections.Generic;
using UnityEngine;
using NativeFTManager.Domain;
using NativeFTManager.Infra;

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

        /// <summary>
        /// メニューをヒエラルキーのMA Menu Itemで持つか（仕様書8.3）
        /// </summary>
        public static bool UsesHierarchyMenu(NativeFTManagerComponent component)
        {
            return component.menuMode == NativeFTMenuMode.Hierarchy;
        }

        /// <summary>
        /// ヒエラルキーのメニューを読む。ビルド時に作るメニューのコンポーネントではnullを返す。
        /// Menu ItemはMAのマージで消えるので、MAより前（Generating）か、ビルドの外で呼ぶ
        /// </summary>
        public static MenuHierarchy ReadMenu(NativeFTManagerComponent component, GameObject avatarRoot)
        {
            return UsesHierarchyMenu(component) ? MenuHierarchyScanner.Scan(avatarRoot) : null;
        }

        /// <summary>
        /// トグルを解決する。menuを渡すと、トグルの表示・既定値・保存はMenu Itemから決める。
        /// Menu Itemの無いトグルの固定の値だけは、コンポーネントのtogglesの既定値を使う
        /// </summary>
        public static ToggleSet ResolveToggles(NativeFTManagerComponent component, MenuHierarchy menu = null)
        {
            return ToggleSet.Resolve(GetToggleSource(component, menu), GetFeatures(component), GetSensitivity(component, menu));
        }

        /// <summary>
        /// トグルの設定の読み先。menuがnullならコンポーネントのtoggles
        /// </summary>
        public static System.Func<NativeFTToggle, ToggleSettings> GetToggleSource(
            NativeFTManagerComponent component,
            MenuHierarchy menu)
        {
            if (menu == null)
            {
                return component.GetToggle;
            }

            return toggle => menu.GetToggle(toggle, component.GetToggle);
        }

        /// <summary>
        /// 感度の設定（仕様書5.6）。M3までに保存したコンポーネントは設定を持たないので、どの系統も作らない。
        /// menuを渡すと、作る系統と保存はRadialのMenu Itemから決め、上限の倍率だけをコンポーネントから読む
        /// </summary>
        public static SensitivityConfig GetSensitivity(NativeFTManagerComponent component, MenuHierarchy menu = null)
        {
            var settings = component.sensitivity;
            float maxMultiplier = settings != null ? settings.maxMultiplier : 1f;
            if (menu != null)
            {
                return new SensitivityConfig(
                    menu.Contains(SensitivityChannel.Eye),
                    menu.Contains(SensitivityChannel.Lip),
                    maxMultiplier,
                    menu.SensitivitySaved);
            }

            if (settings == null)
            {
                return SensitivityConfig.None;
            }

            return new SensitivityConfig(settings.eye, settings.lip, settings.maxMultiplier, settings.saved);
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
