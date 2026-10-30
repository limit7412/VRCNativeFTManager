using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using NativeFTManager.Domain;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// アバターに置かれたMA Menu Itemから、本製品の公開パラメーターを指すものを読む（仕様書8.3）
    ///
    /// Manager の子に限らず、アバター全体から探す。利用者がMenu Itemを別のサブメニューへ移しても読めるようにするため。
    /// MAはメニューを組むときにオブジェクトのアクティブ状態を見ないので、非アクティブなものも数える。
    /// EditorOnlyのオブジェクトは、ビルドではこの時点までにNDMFが取り除いている
    /// </summary>
    internal static class MenuHierarchyScanner
    {
        public static MenuHierarchy Scan(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return MenuHierarchy.Empty;
            }

            return MenuHierarchy.Read(avatarRoot.GetComponentsInChildren<ModularAvatarMenuItem>(true).Select(ToEntry));
        }

        /// <summary>
        /// コンポーネントから見たメニューを読む（ビルドの外で使う）
        /// </summary>
        public static MenuHierarchy Scan(NativeFTManagerComponent component)
        {
            return Scan(GetSearchRoot(component));
        }

        /// <summary>
        /// 公開パラメーターを指すMenu Itemを探す範囲。Menu Itemはアバターのどこへ移してもよいのでアバター全体を見る。
        /// アバターの外に置いたManagerでは、Managerの配下だけを見る
        /// </summary>
        public static GameObject GetSearchRoot(NativeFTManagerComponent component)
        {
            GameObject found = null;
            for (var cursor = component.transform; cursor != null; cursor = cursor.parent)
            {
                if (cursor.GetComponent<VRCAvatarDescriptor>() != null)
                {
                    found = cursor.gameObject;
                }
            }

            return found != null ? found : component.gameObject;
        }

        public static MenuEntry ToEntry(ModularAvatarMenuItem item)
        {
            var control = item != null ? item.Control : null;
            if (control == null)
            {
                return null;
            }

            switch (control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    return new MenuEntry(
                        MenuEntryKind.Toggle,
                        control.parameter?.name,
                        control.value,
                        item.automaticValue,
                        item.isDefault,
                        item.isSaved);
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    var subParameters = control.subParameters;
                    return new MenuEntry(
                        MenuEntryKind.Radial,
                        subParameters != null && subParameters.Length > 0 ? subParameters[0]?.name : null,
                        isSaved: item.isSaved);
                default:
                    return null;
            }
        }
    }
}
