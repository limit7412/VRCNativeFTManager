using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using NativeFTManager.Domain;
using Object = UnityEngine.Object;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// 生成したFX、同期パラメーター、メニューを、アバター直下の専用オブジェクトへMAのコンポーネントとして置く
    ///
    /// メニューとパラメーターもビルド時に作る。メニューに出すトグルの設定でパラメーターの数が変わるので、
    /// プレハブに静的に置くと、設定を変えるたびにプレハブの中身を合わせ直すことになるためである
    /// </summary>
    internal static class GeneratedObjectsInstaller
    {
        public const string HolderObjectName = NativeFTManagerParameterNames.ShortPrefix + " Generated";

        public static GameObject Install(
            GameObject avatarRoot,
            AnimatorController controller,
            IReadOnlyList<ResolvedToggle> parameters,
            MenuPlan menu,
            Action<Object> saveAsset)
        {
            if (avatarRoot == null)
            {
                return null;
            }

            var holder = new GameObject(HolderObjectName);
            holder.transform.SetParent(avatarRoot.transform, false);

            if (controller != null)
            {
                var mergeAnimator = holder.AddComponent<ModularAvatarMergeAnimator>();
                mergeAnimator.animator = controller;
                mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
                mergeAnimator.deleteAttachedAnimator = true;

                // アニメーションを持たないので、パスもWrite Defaultsも結果に影響しない。
                // 配置場所で変わらないAbsoluteにし、Write Defaultsは生成時のOFFのままにする（仕様書5.3）
                mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
                mergeAnimator.matchAvatarWriteDefaults = false;
            }

            if (parameters.Count > 0)
            {
                var maParameters = holder.AddComponent<ModularAvatarParameters>();
                foreach (var toggle in parameters)
                {
                    // 内部パラメーター（自動リネーム）にはしない。保存値と利用者のアニメーターから
                    // 名前で参照されるため（仕様書7.1）
                    maParameters.parameters.Add(new ParameterConfig
                    {
                        nameOrPrefix = toggle.ParameterName,
                        syncType = ParameterSyncType.Bool,
                        defaultValue = toggle.DefaultValue ? 1f : 0f,
                        hasExplicitDefaultValue = true,
                        saved = toggle.Saved,
                        localOnly = false,
                        internalParameter = false,
                    });
                }
            }

            if (menu != null && !menu.IsEmpty)
            {
                var installer = holder.AddComponent<ModularAvatarMenuInstaller>();
                installer.menuToAppend = WriteMenu(menu, saveAsset);
            }

            return holder;
        }

        /// <summary>
        /// 「Face Tracking」サブメニュー1つを持つメニューを作る（仕様書8.3）
        /// </summary>
        private static VRCExpressionsMenu WriteMenu(MenuPlan plan, Action<Object> saveAsset)
        {
            var subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            subMenu.name = NativeFTManagerParameterNames.ShortPrefix + " " + MenuPlan.SubMenuName;
            subMenu.controls = new List<VRCExpressionsMenu.Control>();
            foreach (var toggle in plan.Toggles)
            {
                subMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = toggle.Label,
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = toggle.Parameter },
                    value = 1f,
                    subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                    labels = new VRCExpressionsMenu.Control.Label[0],
                });
            }

            var root = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            root.name = NativeFTManagerParameterNames.ShortPrefix + " Menu";
            root.controls = new List<VRCExpressionsMenu.Control>
            {
                new VRCExpressionsMenu.Control
                {
                    name = MenuPlan.SubMenuName,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
                    subMenu = subMenu,
                    subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                    labels = new VRCExpressionsMenu.Control.Label[0],
                },
            };

            saveAsset?.Invoke(subMenu);
            saveAsset?.Invoke(root);
            return root;
        }
    }
}
