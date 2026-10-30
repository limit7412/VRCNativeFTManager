using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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

        private NativeFTManagerComponent CreateExpanded(NativeFTPreset preset)
        {
            var gameObject = new GameObject("MenuHierarchyBuilderTests");
            _created.Add(gameObject);
            var component = gameObject.AddComponent<NativeFTManagerComponent>();
            NativeFTPresets.Apply(component, preset);
            var toggles = ComponentSettings.ResolveToggles(component);
            component.menuMode = NativeFTMenuMode.Hierarchy;
            MenuHierarchyBuilder.Expand(component, toggles, UndoName);
            return component;
        }

        private static MenuHierarchy Read(NativeFTManagerComponent component)
        {
            return MenuHierarchyScanner.Scan(component);
        }

        [TestCase(NativeFTPreset.UE)]
        [TestCase(NativeFTPreset.ARKit)]
        [TestCase(NativeFTPreset.Minimal)]
        public void Expand_ReadsBackAsThePreset(NativeFTPreset preset)
        {
            var component = CreateExpanded(preset);

            var menu = Read(component);

            Assert.That(
                NativeFTPresets.Matches(component, preset, ComponentSettings.GetToggleSource(component, menu), true),
                Is.True);
            Assert.That(
                ComponentSettings.ResolveToggles(component, menu).Dynamic.Select(t => t.Toggle),
                Is.EqualTo(NativeFTPresets.CreateToggles(preset).Where(t => t.showInMenu).Select(t => t.toggle)));
        }

        [Test]
        public void Expand_MakesOneInstalledSubMenu()
        {
            var component = CreateExpanded(NativeFTPreset.UE);

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
            var component = CreateExpanded(NativeFTPreset.UE);
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
            var component = CreateExpanded(NativeFTPreset.UE);
            int count = component.GetComponentsInChildren<ModularAvatarMenuItem>(true).Length;

            MenuHierarchyBuilder.Expand(component, ComponentSettings.ResolveToggles(component, Read(component)), UndoName);

            Assert.That(component.GetComponentsInChildren<ModularAvatarMenuItem>(true).Length, Is.EqualTo(count));
        }

        [Test]
        public void ApplyPreset_RemovesHiddenTogglesAndEmptySettings()
        {
            var component = CreateExpanded(NativeFTPreset.UE);

            NativeFTPresets.Apply(component, NativeFTPreset.Minimal);
            MenuHierarchyBuilder.ApplyPreset(component, NativeFTPreset.Minimal, UndoName);

            var menu = Read(component);
            Assert.That(
                NativeFTPresets.Matches(
                    component, NativeFTPreset.Minimal, ComponentSettings.GetToggleSource(component, menu), true),
                Is.True);
            var root = MenuHierarchyBuilder.FindRoot(component).transform;
            Assert.That(root.childCount, Is.EqualTo(3));
        }

        [Test]
        public void Expand_KeepsTheFixedValueOfHiddenToggles()
        {
            var gameObject = new GameObject("MenuHierarchyBuilderTests");
            _created.Add(gameObject);
            var component = gameObject.AddComponent<NativeFTManagerComponent>();
            NativeFTPresets.Apply(component, NativeFTPreset.UE);
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
            var component = CreateExpanded(NativeFTPreset.UE);
            var radial = MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Eye, true, UndoName);
            var subMenu = radial.transform.parent.GetComponent<ModularAvatarMenuItem>();
            subMenu.MenuSource = SubmenuSource.MenuAsset;

            MenuHierarchyBuilder.RemoveRadial(component, SensitivityChannel.Eye, UndoName);

            Assert.That(subMenu == null, Is.False);
        }

        [Test]
        public void ApplyPreset_KeepsRenamedItems()
        {
            var component = CreateExpanded(NativeFTPreset.ARKit);
            var root = MenuHierarchyBuilder.FindRoot(component).transform;
            root.GetChild(0).name = "Renamed";

            NativeFTPresets.Apply(component, NativeFTPreset.UE);
            MenuHierarchyBuilder.ApplyPreset(component, NativeFTPreset.UE, UndoName);

            Assert.That(root.GetChild(0).name, Is.EqualTo("Renamed"));
            Assert.That(Read(component).Contains(NativeFTToggle.Pupil), Is.True);
        }

        [Test]
        public void AddToggle_GoesWhereTheOthersWereMoved()
        {
            var component = CreateExpanded(NativeFTPreset.ARKit);
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
            var component = CreateExpanded(NativeFTPreset.UE);

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
            var component = CreateExpanded(NativeFTPreset.UE);
            var root = MenuHierarchyBuilder.FindRoot(component).transform;
            int count = root.childCount;
            MenuHierarchyBuilder.AddRadial(component, SensitivityChannel.Lip, true, UndoName);

            MenuHierarchyBuilder.RemoveRadial(component, SensitivityChannel.Lip, UndoName);

            Assert.That(root.childCount, Is.EqualTo(count));
            Assert.That(Read(component).Radials, Is.Empty);
        }
    }
}
