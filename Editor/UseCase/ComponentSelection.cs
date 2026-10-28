using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// アバター内のNativeFTManagerComponentの収集と選定
    ///
    /// ビルドが採用するものと、エディタ上の重複排除が残すものを一致させるため、
    /// 選定の規則はここにだけ置く。
    /// </summary>
    internal static class ComponentSelection
    {
        /// <summary>
        /// アバター内の処理対象コンポーネントを選定する（アバタールート直付けを優先）
        /// </summary>
        public static NativeFTManagerComponent SelectPrimaryComponent(
            Transform avatarRoot,
            IReadOnlyList<NativeFTManagerComponent> components)
        {
            if (components == null || components.Count == 0)
            {
                return null;
            }

            if (avatarRoot != null)
            {
                var onRoot = components.FirstOrDefault(c => c != null && c.transform == avatarRoot);
                if (onRoot != null)
                {
                    return onRoot;
                }
            }

            return components.FirstOrDefault(c => c != null);
        }

        public static NativeFTManagerComponent SelectPrimaryComponent(
            GameObject avatarRoot,
            IReadOnlyList<NativeFTManagerComponent> components)
        {
            return SelectPrimaryComponent(avatarRoot != null ? avatarRoot.transform : null, components);
        }

        /// <summary>
        /// 選定から漏れたコンポーネントを返す（重複の自動排除で削除する対象）
        /// </summary>
        public static NativeFTManagerComponent[] SelectDuplicateComponents(
            Transform avatarRoot,
            IReadOnlyList<NativeFTManagerComponent> components)
        {
            var primary = SelectPrimaryComponent(avatarRoot, components);
            if (primary == null)
            {
                return new NativeFTManagerComponent[0];
            }

            return components.Where(c => c != null && c != primary).ToArray();
        }

        public static NativeFTManagerComponent[] CollectComponents(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return new NativeFTManagerComponent[0];
            }

            return avatarRoot.GetComponentsInChildren<NativeFTManagerComponent>(true)
                .Where(c => c != null)
                .ToArray();
        }

        public static void RemoveComponents(IReadOnlyList<NativeFTManagerComponent> components)
        {
            if (components == null)
            {
                return;
            }

            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                Object.DestroyImmediate(component);
            }
        }
    }
}
