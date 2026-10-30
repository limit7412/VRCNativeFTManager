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
    /// パラメーターはビルド時に作る。メニューに出すトグルでパラメーターの数が変わるためである。
    /// メニューは、ビルド時に作る形（menuModeがGenerated）のときだけここで作る。
    /// ヒエラルキーにMenu Itemを置く形では、MAがMenu Itemからメニューを組む
    /// </summary>
    internal static class GeneratedObjectsInstaller
    {
        public const string HolderObjectName = NativeFTManagerParameterNames.ShortPrefix + " Generated";

        /// <summary>FaceEmoの抑制のMerge Animatorを載せる子オブジェクトの名前</summary>
        public const string SuppressionObjectName = NativeFTManagerParameterNames.ShortPrefix + " FaceEmo";

        public static GameObject Install(
            GameObject avatarRoot,
            AnimatorController controller,
            IReadOnlyList<ResolvedToggle> parameters,
            MenuPlan menu,
            Action<Object> saveAsset,
            AnimatorController suppressionController = null,
            IReadOnlyList<FloatParameterPlan> floatParameters = null)
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

            if (suppressionController != null)
            {
                InstallSuppression(holder, suppressionController);
            }

            floatParameters = floatParameters ?? new FloatParameterPlan[0];
            if (parameters.Count > 0 || floatParameters.Count > 0)
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

                // 感度のRadial（仕様書5.6）。トグルと同じく名前を固定する
                foreach (var parameter in floatParameters)
                {
                    maParameters.parameters.Add(new ParameterConfig
                    {
                        nameOrPrefix = parameter.Name,
                        syncType = ParameterSyncType.Float,
                        defaultValue = parameter.DefaultValue,
                        hasExplicitDefaultValue = true,
                        saved = parameter.Saved,
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
        /// FaceEmoの抑制のコントローラを、トグルとは別のMerge Animatorで置く（仕様書10.3）。
        /// トグルのレイヤーはWrite Defaultsを生成時のOFFのまま入れるが、こちらは空クリップを持つので、
        /// FE-JsT Bridgeと同じくアバターのWrite Defaultsに合わせる
        /// </summary>
        private static void InstallSuppression(GameObject holder, AnimatorController controller)
        {
            var child = new GameObject(SuppressionObjectName);
            child.transform.SetParent(holder.transform, false);

            var mergeAnimator = child.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.deleteAttachedAnimator = true;

            // クリップが持つのは束縛先のないダミーカーブだけなので、
            // 配置場所によってパスが変わらないAbsoluteで固定する
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
        }

        /// <summary>
        /// 「Face Tracking」サブメニュー1つを持つメニューを作る（仕様書8.3）。
        /// 表情補正のトグルは、その中の「Settings」サブメニューへ入れる
        /// </summary>
        private static VRCExpressionsMenu WriteMenu(MenuPlan plan, Action<Object> saveAsset)
        {
            var subMenu = CreateMenu(NativeFTManagerParameterNames.ShortPrefix + " " + MenuPlan.SubMenuName);
            foreach (var toggle in plan.Toggles)
            {
                subMenu.controls.Add(CreateToggle(toggle));
            }

            if (plan.Sensitivity.Count > 0)
            {
                var sensitivityMenu = CreateMenu(NativeFTManagerParameterNames.ShortPrefix + " " + MenuPlan.SensitivityMenuName);
                foreach (var radial in plan.Sensitivity)
                {
                    sensitivityMenu.controls.Add(CreateRadial(radial));
                }

                subMenu.controls.Add(CreateSubMenu(MenuPlan.SensitivityMenuName, sensitivityMenu));
                saveAsset?.Invoke(sensitivityMenu);
            }

            if (plan.Settings.Count > 0)
            {
                var settingsMenu = CreateMenu(NativeFTManagerParameterNames.ShortPrefix + " " + MenuPlan.SettingsMenuName);
                foreach (var toggle in plan.Settings)
                {
                    settingsMenu.controls.Add(CreateToggle(toggle));
                }

                subMenu.controls.Add(CreateSubMenu(MenuPlan.SettingsMenuName, settingsMenu));
                saveAsset?.Invoke(settingsMenu);
            }

            var root = CreateMenu(NativeFTManagerParameterNames.ShortPrefix + " Menu");
            root.controls.Add(CreateSubMenu(MenuPlan.SubMenuName, subMenu));

            saveAsset?.Invoke(subMenu);
            saveAsset?.Invoke(root);
            return root;
        }

        private static VRCExpressionsMenu CreateMenu(string name)
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = name;
            menu.controls = new List<VRCExpressionsMenu.Control>();
            return menu;
        }

        private static VRCExpressionsMenu.Control CreateToggle(MenuTogglePlan toggle)
        {
            return new VRCExpressionsMenu.Control
            {
                name = toggle.Label,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = toggle.Parameter },
                value = 1f,
                subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                labels = new VRCExpressionsMenu.Control.Label[0],
            };
        }

        private static VRCExpressionsMenu.Control CreateRadial(MenuTogglePlan radial)
        {
            return new VRCExpressionsMenu.Control
            {
                name = radial.Label,
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
                subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = radial.Parameter } },
                labels = new VRCExpressionsMenu.Control.Label[0],
            };
        }

        private static VRCExpressionsMenu.Control CreateSubMenu(string name, VRCExpressionsMenu subMenu)
        {
            return new VRCExpressionsMenu.Control
            {
                name = name,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
                subMenu = subMenu,
                subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                labels = new VRCExpressionsMenu.Control.Label[0],
            };
        }
    }
}
