using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using nadena.dev.ndmf.localization;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using NativeFTManager.Domain;
using NativeFTManager.Infra;
using NativeFTManager.UseCase;
using Object = UnityEngine.Object;

namespace NativeFTManager.Tests
{
    public class MenuHierarchyBuilderTests
    {
        private const string UndoName = "MenuHierarchyBuilderTests";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }

            _created.Clear();
        }

        /// <summary>
        /// 既定の設定で展開したコンポーネント。テストではResetが走らないので、既定値をここで入れる
        /// </summary>
        private NativeFTManagerComponent CreateExpanded()
        {
            var gameObject = new GameObject("MenuHierarchyBuilderTests");
            _created.Add(gameObject);
            var component = gameObject.AddComponent<NativeFTManagerComponent>();
            component.toggles = ToggleSettings.CreateDefaults();
            component.enableEmulation = true;
            component.enableLimits = true;
            component.enableEyeSync = true;
            var toggles = ComponentSettings.ResolveToggles(component);
            component.menuMode = NativeFTMenuMode.Hierarchy;
            MenuHierarchyBuilder.Expand(component, toggles, UndoName);
            return component;
        }

        /// <summary>
        /// 既定の設定で展開してから、Pupilのトグルを消したもの（ARKitのシェイプのアバターでよくある形）
        /// </summary>
        private NativeFTManagerComponent CreateExpandedWithoutPupil()
        {
            var component = CreateExpanded();
            MenuHierarchyBuilder.RemoveToggle(component, NativeFTToggle.Pupil, UndoName);
            return component;
        }

        private static MenuHierarchy Read(NativeFTManagerComponent component)
        {
            return MenuHierarchyScanner.Scan(component);
        }

        [Test]
        public void Expand_ReadsBackAsTheDefaults()
        {
            var component = CreateExpanded();

            var menu = Read(component);

            var resolved = ComponentSettings.ResolveToggles(component, menu);
            Assert.That(
                resolved.Dynamic.Select(t => t.Toggle),
                Is.EqualTo(System.Enum.GetValues(typeof(NativeFTToggle)).Cast<NativeFTToggle>()));
            foreach (var expected in ToggleSettings.CreateDefaults())
            {
                Assert.That(resolved[expected.toggle].DefaultValue, Is.EqualTo(expected.defaultValue), expected.toggle.ToString());
            }
        }

        [Test]
        public void Expand_MakesOneInstalledSubMenu()
        {
            var component = CreateExpanded();

            var root = MenuHierarchyBuilder.FindRoot(component);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.parent, Is.EqualTo(component.transform));
            Assert.That(component.GetComponentsInChildren<ModularAvatarMenuInstaller>(true).Length, Is.EqualTo(1));

            // メイン5つとSettingsのサブメニュー
            var children = Enumerable.Range(0, root.transform.childCount).Select(i => root.transform.GetChild(i)).ToArray();
            Assert.That(children.Length, Is.EqualTo(6));
            var settings = children.Last().GetComponent<ModularAvatarMenuItem>();
            Assert.That(settings.Control.type, Is.EqualTo(VRCExpressionsMenu.Control.ControlType.SubMenu));
            Assert.That(settings.MenuSource, Is.EqualTo(SubmenuSource.Children));
            Assert.That(settings.transform.childCount, Is.EqualTo(3));
        }

        [Test]
        public void Expand_KeepsTheEnumOrder()
        {
            var component = CreateExpanded();
            var root = MenuHierarchyBuilder.FindRoot(component).transform;

            var parameters = Enumerable.Range(0, 5)
                .Select(i => root.GetChild(i).GetComponent<ModularAvatarMenuItem>().Control.parameter.name)
                .ToArray();

            Assert.That(parameters, Is.EqualTo(new[]
            {
                NativeFTManagerParameterNames.FaceTracking,
                NativeFTManagerParameterNames.EyeTracking,
                NativeFTManagerParameterNames.LipTracking,
                NativeFTManagerParameterNames.Pupil,
                NativeFTManagerParameterNames.Visemes,
            }));
        }

        [Test]
        public void Expand_Twice_DoesNotDuplicate()
        {
            var component = CreateExpanded();
            int count = component.GetComponentsInChildren<ModularAvatarMenuItem>(true).Length;

            MenuHierarchyBuilder.Expand(component, ComponentSettings.ResolveToggles(component, Read(component)), UndoName);

            Assert.That(component.GetComponentsInChildren<ModularAvatarMenuItem>(true).Length, Is.EqualTo(count));
        }

        [Test]
        public void Expand_GivesEveryItemADefaultIcon()
        {
            var component = CreateExpanded();
            MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Eye, true, UndoName);
            MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Lip, true, UndoName);

            var items = MenuHierarchyBuilder.FindRoot(component).GetComponentsInChildren<ModularAvatarMenuItem>(true);
            Assert.That(items.Length, Is.EqualTo(13));
            foreach (var item in items)
            {
                Assert.That(item.Control.icon, Is.Not.Null, item.gameObject.name);
            }

            Assert.That(
                MenuHierarchyBuilder.FindRoot(component).GetComponent<ModularAvatarMenuItem>().Control.icon,
                Is.EqualTo(MenuIconAssets.Load(MenuIcon.FaceTrackingMenu)));
        }

        [Test]
        public void Expand_FillsOnlyEmptyIcons()
        {
            var component = CreateExpanded();
            var root = MenuHierarchyBuilder.FindRoot(component).transform;
            var emptied = root.GetChild(1).GetComponent<ModularAvatarMenuItem>();
            emptied.Control.icon = null;
            var custom = new Texture2D(4, 4);
            _created.Add(custom);
            var customized = root.GetChild(0).GetComponent<ModularAvatarMenuItem>();
            customized.Control.icon = custom;
            var settings = root.GetChild(root.childCount - 1).GetComponent<ModularAvatarMenuItem>();
            settings.Control.icon = null;

            MenuHierarchyBuilder.Expand(component, ComponentSettings.ResolveToggles(component, Read(component)), UndoName);

            Assert.That(emptied.Control.icon, Is.EqualTo(MenuIconAssets.Load(MenuIcon.EyeTracking)));
            Assert.That(customized.Control.icon, Is.SameAs(custom));
            Assert.That(settings.Control.icon, Is.EqualTo(MenuIconAssets.Load(MenuIcon.Settings)));
        }

        [Test]
        public void Expand_LeavesTheUsersOwnSubMenuIconEmpty()
        {
            var component = CreateExpanded();
            var root = MenuHierarchyBuilder.FindRoot(component).transform;

            // Settingsの項目を、根の外に置いた利用者のサブメニューへ移す
            var own = new GameObject("Own");
            own.transform.SetParent(component.transform, false);
            var ownItem = own.AddComponent<ModularAvatarMenuItem>();
            ownItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
            };
            ownItem.MenuSource = SubmenuSource.Children;
            var settings = root.GetChild(root.childCount - 1);
            while (settings.childCount > 0)
            {
                settings.GetChild(0).SetParent(own.transform, false);
            }

            MenuHierarchyBuilder.Expand(component, ComponentSettings.ResolveToggles(component, Read(component)), UndoName);

            Assert.That(ownItem.Control.icon, Is.Null);
        }

        [Test]
        public void Expand_LeavesTheUsersOwnSubMenuUnderTheRootIconEmpty()
        {
            var component = CreateExpanded();
            var root = MenuHierarchyBuilder.FindRoot(component).transform;

            // Settingsの項目を、根の直下に作った利用者のサブメニューへ移す
            var own = new GameObject("My Settings");
            own.transform.SetParent(root, false);
            var ownItem = own.AddComponent<ModularAvatarMenuItem>();
            ownItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
            };
            ownItem.MenuSource = SubmenuSource.Children;
            var settings = root.GetChild(root.childCount - 2);
            while (settings.childCount > 0)
            {
                settings.GetChild(0).SetParent(own.transform, false);
            }

            MenuHierarchyBuilder.FillMissingIcons(component, UndoName);

            Assert.That(ownItem.Control.icon, Is.Null);
        }

        [Test]
        public void RestoreToggles_AddsOnlyTheDeletedToggles()
        {
            var component = CreateExpanded();
            var root = MenuHierarchyBuilder.FindRoot(component).transform;
            root.GetChild(0).name = "Renamed";
            MenuHierarchyBuilder.RemoveToggle(component, NativeFTToggle.Pupil, UndoName);
            MenuHierarchyBuilder.RemoveToggle(component, NativeFTToggle.EyeSync, UndoName);

            int restored = MenuHierarchyBuilder.RestoreToggles(
                component, ComponentSettings.GetFeatures(component), UndoName);

            Assert.That(restored, Is.EqualTo(2));
            Assert.That(root.GetChild(0).name, Is.EqualTo("Renamed"));
            var menu = Read(component);
            Assert.That(menu.Contains(NativeFTToggle.Pupil), Is.True);
            Assert.That(menu.GetToggle(NativeFTToggle.EyeSync).defaultValue, Is.False);
            Assert.That(MenuHierarchyBuilder.RestoreToggles(
                component, ComponentSettings.GetFeatures(component), UndoName), Is.EqualTo(0));
        }

        [Test]
        public void RestoreToggles_SkipsCorrectionsThatAreNotGenerated()
        {
            var component = CreateExpanded();
            MenuHierarchyBuilder.RemoveToggle(component, NativeFTToggle.Limits, UndoName);
            component.enableLimits = false;

            MenuHierarchyBuilder.RestoreToggles(component, ComponentSettings.GetFeatures(component), UndoName);

            Assert.That(Read(component).Contains(NativeFTToggle.Limits), Is.False);
        }

        [Test]
        public void RestoreToggles_UsesTheFixedValueOfTheHiddenToggle()
        {
            var component = CreateExpanded();
            MenuHierarchyBuilder.RemoveToggle(component, NativeFTToggle.Visemes, UndoName);
            var visemes = component.toggles.First(t => t.toggle == NativeFTToggle.Visemes);
            visemes.defaultValue = false;
            visemes.saved = false;

            MenuHierarchyBuilder.RestoreToggles(component, ComponentSettings.GetFeatures(component), UndoName);

            var restored = Read(component).GetToggle(NativeFTToggle.Visemes);
            Assert.That(restored.showInMenu, Is.True);
            Assert.That(restored.defaultValue, Is.False);
            Assert.That(restored.saved, Is.False);
        }

        [Test]
        public void Expand_KeepsTheFixedValueOfHiddenToggles()
        {
            var gameObject = new GameObject("MenuHierarchyBuilderTests");
            _created.Add(gameObject);
            var component = gameObject.AddComponent<NativeFTManagerComponent>();
            component.toggles = ToggleSettings.CreateDefaults();
            var visemes = component.toggles.First(t => t.toggle == NativeFTToggle.Visemes);
            visemes.showInMenu = false;
            visemes.defaultValue = false;

            var toggles = ComponentSettings.ResolveToggles(component);
            component.menuMode = NativeFTMenuMode.Hierarchy;
            MenuHierarchyBuilder.Expand(component, toggles, UndoName);

            var resolved = ComponentSettings.ResolveToggles(component, Read(component));
            Assert.That(resolved[NativeFTToggle.Visemes].IsDynamic, Is.False);
            Assert.That(resolved[NativeFTToggle.Visemes].DefaultValue, Is.False);
        }

        [Test]
        public void RemoveRadial_KeepsAnAssetSubMenu()
        {
            var component = CreateExpanded();
            var radial = MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Eye, true, UndoName);
            var subMenu = radial.transform.parent.GetComponent<ModularAvatarMenuItem>();
            subMenu.MenuSource = SubmenuSource.MenuAsset;

            MenuHierarchyBuilder.RemoveRadial(component, SensitivityChannel.Eye, UndoName);

            Assert.That(subMenu == null, Is.False);
        }

        [TestCase("ja-jp")]
        [TestCase("en-us")]
        public void Expand_NamesObjectsInEnglishWhateverTheLanguage(string language)
        {
            var previous = LanguagePrefs.Language;
            try
            {
                LanguagePrefs.Language = language;
                var component = CreateExpandedWithoutPupil();
                MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Eye, true, UndoName);
                var root = MenuHierarchyBuilder.FindRoot(component);
                var items = root.GetComponentsInChildren<ModularAvatarMenuItem>(true);

                var expected = new[]
                    {
                        MenuPlan.SubMenuName,
                        MenuPlan.SettingsMenuName,
                        MenuPlan.SensitivityMenuName,
                        SensitivityConfig.GetLabel(SensitivityChannel.Eye),
                    }
                    .Concat(System.Enum.GetValues(typeof(NativeFTToggle)).Cast<NativeFTToggle>().Select(MenuPlan.GetLabel))
                    .ToArray();
                Assert.That(items, Is.Not.Empty);
                foreach (var item in items)
                {
                    Assert.That(expected, Does.Contain(item.gameObject.name));
                }

                Assert.That(root.name, Is.EqualTo(MenuPlan.SubMenuName));
            }
            finally
            {
                LanguagePrefs.Language = previous;
            }
        }

        [TestCase("ja-jp")]
        [TestCase("en-us")]
        public void Expand_LeavesLabelsEmptyWhateverTheLanguage(string language)
        {
            var previous = LanguagePrefs.Language;
            try
            {
                LanguagePrefs.Language = language;
                var component = CreateExpanded();
                MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Eye, true, UndoName);
                var items = MenuHierarchyBuilder.FindRoot(component).GetComponentsInChildren<ModularAvatarMenuItem>(true);

                Assert.That(items, Is.Not.Empty);
                foreach (var item in items)
                {
                    Assert.That(item.label, Is.Empty);
                }
            }
            finally
            {
                LanguagePrefs.Language = previous;
            }
        }

        [Test]
        public void RestoreEnglishNames_ClearsJapaneseLabelsWrittenByOlderVersions()
        {
            // 0.1.0-test13からtest15までは、日本語のエディタで作るとlabelに日本語を書いていた
            var component = CreateExpanded();
            var radial = MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Eye, true, UndoName);
            var root = MenuHierarchyBuilder.FindRoot(component).GetComponent<ModularAvatarMenuItem>();
            var settings = root.transform.Find(MenuPlan.SettingsMenuName).GetComponent<ModularAvatarMenuItem>();
            var eye = FindToggleItem(component, NativeFTToggle.EyeTracking);
            var lip = FindToggleItem(component, NativeFTToggle.LipTracking);
            root.label = "フェイストラッキング";
            settings.label = "設定";
            radial.label = "目";
            eye.label = "目のトラッキング";
            lip.label = "くち";

            MenuHierarchyBuilder.RestoreEnglishNames(component, UndoName);

            Assert.That(root.label, Is.Empty);
            Assert.That(settings.label, Is.Empty);
            Assert.That(radial.label, Is.Empty);
            Assert.That(eye.label, Is.Empty);
            Assert.That(lip.label, Is.EqualTo("くち"));
        }

        [Test]
        public void RestoreEnglishNames_RenamesJapaneseObjectNamesWrittenByOlderVersions()
        {
            // 0.1.0-test12までは、日本語のエディタで作るとオブジェクト名が日本語だった
            var component = CreateExpanded();
            var root = MenuHierarchyBuilder.FindRoot(component);
            var settings = root.transform.Find(MenuPlan.SettingsMenuName);
            var eye = FindToggleItem(component, NativeFTToggle.EyeTracking);
            var lip = FindToggleItem(component, NativeFTToggle.LipTracking);
            var visemes = FindToggleItem(component, NativeFTToggle.Visemes);
            root.name = "フェイストラッキング";
            settings.name = "設定";
            eye.gameObject.name = "目のトラッキング";
            lip.gameObject.name = "くち";
            visemes.gameObject.name = "ビゼーム";
            visemes.label = "リップシンク";

            MenuHierarchyBuilder.RestoreEnglishNames(component, UndoName);

            Assert.That(root.name, Is.EqualTo(MenuPlan.SubMenuName));
            Assert.That(settings.name, Is.EqualTo(MenuPlan.SettingsMenuName));
            Assert.That(eye.gameObject.name, Is.EqualTo(MenuPlan.GetLabel(NativeFTToggle.EyeTracking)));
            Assert.That(lip.gameObject.name, Is.EqualTo("くち"));

            // 利用者がlabelで表示名を付けていれば、オブジェクト名を変えても表示名は変わらないので、そのままにする
            Assert.That(visemes.gameObject.name, Is.EqualTo("ビゼーム"));
            Assert.That(visemes.label, Is.EqualTo("リップシンク"));
        }

        private static ModularAvatarMenuItem FindToggleItem(NativeFTManagerComponent component, NativeFTToggle toggle)
        {
            var parameter = ResolvedToggle.GetParameterName(toggle);
            return MenuHierarchyBuilder.FindRoot(component).GetComponentsInChildren<ModularAvatarMenuItem>(true)
                .First(item => item.Control != null
                    && item.Control.type == VRCExpressionsMenu.Control.ControlType.Toggle
                    && item.Control.parameter != null
                    && item.Control.parameter.name == parameter);
        }

        [Test]
        public void RestoreToggles_KeepsItemsWithJapaneseObjectNames()
        {
            // 0.1.0-test12までは、日本語のエディタで作るとオブジェクト名が日本語だった
            var component = CreateExpandedWithoutPupil();
            var root = MenuHierarchyBuilder.FindRoot(component);
            root.name = "フェイストラッキング";
            foreach (Transform child in root.transform)
            {
                child.name = "目のトラッキング";
            }

            MenuHierarchyBuilder.RestoreToggles(component, ComponentSettings.GetFeatures(component), UndoName);

            Assert.That(MenuHierarchyBuilder.FindRoot(component), Is.SameAs(root));
            Assert.That(root.name, Is.EqualTo("フェイストラッキング"));
            Assert.That(Read(component).Contains(NativeFTToggle.Pupil), Is.True);
        }

        [Test]
        public void AddToggle_GoesWhereTheOthersWereMoved()
        {
            var component = CreateExpandedWithoutPupil();
            var moved = new GameObject("Moved");
            moved.transform.SetParent(component.transform, false);
            var root = MenuHierarchyBuilder.FindRoot(component).transform;
            root.GetChild(0).SetParent(moved.transform, false);

            var pupil = MenuHierarchyBuilder.AddToggle(component, NativeFTToggle.Pupil, true, true, UndoName);

            Assert.That(pupil.transform.parent, Is.EqualTo(moved.transform));
        }

        [Test]
        public void AddRadial_CreatesSensitivityBeforeSettings()
        {
            var component = CreateExpanded();

            var radial = MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Eye, false, UndoName);

            Assert.That(radial.Control.type, Is.EqualTo(VRCExpressionsMenu.Control.ControlType.RadialPuppet));
            Assert.That(radial.Control.subParameters[0].name, Is.EqualTo(NativeFTManagerParameterNames.SensitivityEye));
            var root = MenuHierarchyBuilder.FindRoot(component).transform;
            Assert.That(radial.transform.parent.GetSiblingIndex(), Is.EqualTo(root.childCount - 2));

            var menu = Read(component);
            var sensitivity = ComponentSettings.GetSensitivity(component, menu);
            Assert.That(sensitivity.Eye, Is.True);
            Assert.That(sensitivity.Lip, Is.False);
            Assert.That(sensitivity.Saved, Is.False);
        }

        [Test]
        public void RemoveRadial_RemovesTheEmptySubMenu()
        {
            var component = CreateExpanded();
            var root = MenuHierarchyBuilder.FindRoot(component).transform;
            int count = root.childCount;
            MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Lip, true, UndoName);

            MenuHierarchyBuilder.RemoveRadial(component, SensitivityChannel.Lip, UndoName);

            Assert.That(root.childCount, Is.EqualTo(count));
            Assert.That(Read(component).Radials, Is.Empty);
        }
    }
}
