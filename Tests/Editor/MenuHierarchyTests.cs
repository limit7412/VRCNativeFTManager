using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class MenuHierarchyTests
    {
        private static MenuEntry Toggle(string parameter, bool isDefault = true, bool isSaved = true, float value = 1f)
        {
            return new MenuEntry(MenuEntryKind.Toggle, parameter, value, false, isDefault, isSaved);
        }

        [Test]
        public void Read_ToggleWithPublicName_IsShownWithItsDefaultAndSaved()
        {
            var menu = MenuHierarchy.Read(new[]
            {
                Toggle(NativeFTManagerParameterNames.EyeTracking, isDefault: false, isSaved: false),
            });

            var eye = menu.GetToggle(NativeFTToggle.EyeTracking);
            Assert.That(menu.Contains(NativeFTToggle.EyeTracking), Is.True);
            Assert.That(eye.showInMenu, Is.True);
            Assert.That(eye.defaultValue, Is.False);
            Assert.That(eye.saved, Is.False);
            Assert.That(menu.IsEmpty, Is.False);
        }

        [Test]
        public void Read_MissingToggle_IsHiddenLikeAMissingSetting()
        {
            var menu = MenuHierarchy.Read(new[] { Toggle(NativeFTManagerParameterNames.FaceTracking) });

            Assert.That(menu.GetToggle(NativeFTToggle.Pupil).showInMenu, Is.False);
            Assert.That(menu.GetToggle(NativeFTToggle.Pupil).defaultValue, Is.True);
            Assert.That(menu.GetToggle(NativeFTToggle.EyeSync).defaultValue, Is.False);
        }

        [Test]
        public void GetToggle_MissingToggle_TakesTheFixedValueFromHidden()
        {
            var menu = MenuHierarchy.Read(new[] { Toggle(NativeFTManagerParameterNames.FaceTracking) });

            var visemes = menu.GetToggle(
                NativeFTToggle.Visemes, toggle => new ToggleSettings(toggle, true, false) { saved = false });

            Assert.That(visemes.showInMenu, Is.False);
            Assert.That(visemes.defaultValue, Is.False);
            Assert.That(visemes.saved, Is.False);
        }

        [Test]
        public void Read_OtherParameters_AreIgnored()
        {
            var menu = MenuHierarchy.Read(new[]
            {
                Toggle("FT/v2/EyeTrackingActive"),
                new MenuEntry(MenuEntryKind.Radial, "SomeRadial"),
                new MenuEntry(MenuEntryKind.Other, NativeFTManagerParameterNames.FaceTracking),
                null,
            });

            Assert.That(menu.IsEmpty, Is.True);
        }

        [Test]
        public void Read_ValueOtherThanOne_IsReportedAndNotRead()
        {
            var menu = MenuHierarchy.Read(new[] { Toggle(NativeFTManagerParameterNames.Visemes, value: 2f) });

            Assert.That(menu.Contains(NativeFTToggle.Visemes), Is.False);
            Assert.That(menu.InvalidValues, Is.EqualTo(new[] { NativeFTManagerParameterNames.Visemes }));
        }

        [Test]
        public void Read_AutomaticValue_IsReadWhateverTheValue()
        {
            var menu = MenuHierarchy.Read(new[]
            {
                new MenuEntry(MenuEntryKind.Toggle, NativeFTManagerParameterNames.Visemes, 0f, true, true, true),
            });

            Assert.That(menu.Contains(NativeFTToggle.Visemes), Is.True);
            Assert.That(menu.InvalidValues, Is.Empty);
        }

        [Test]
        public void Read_DuplicatedToggleWithAutomaticValue_IsReportedAndNotRead()
        {
            var menu = MenuHierarchy.Read(new[]
            {
                Toggle(NativeFTManagerParameterNames.Visemes),
                new MenuEntry(MenuEntryKind.Toggle, NativeFTManagerParameterNames.Visemes, 1f, true, false, true),
            });

            Assert.That(menu.Contains(NativeFTToggle.Visemes), Is.False);
            Assert.That(menu.InvalidValues, Is.EqualTo(new[] { NativeFTManagerParameterNames.Visemes }));
        }

        [Test]
        public void Read_DuplicatedToggle_IsDefaultWhenAnyIsDefault()
        {
            var menu = MenuHierarchy.Read(new[]
            {
                Toggle(NativeFTManagerParameterNames.LipTracking, isDefault: false, isSaved: false),
                Toggle(NativeFTManagerParameterNames.LipTracking, isDefault: true, isSaved: true),
            });

            var lip = menu.GetToggle(NativeFTToggle.LipTracking);
            Assert.That(lip.defaultValue, Is.True);
            Assert.That(lip.saved, Is.False);
        }

        [Test]
        public void Read_Radials_EnableTheirChannels()
        {
            var menu = MenuHierarchy.Read(new[]
            {
                new MenuEntry(MenuEntryKind.Radial, NativeFTManagerParameterNames.SensitivityLip, isSaved: false),
            });

            Assert.That(menu.Contains(SensitivityChannel.Lip), Is.True);
            Assert.That(menu.Contains(SensitivityChannel.Eye), Is.False);
            Assert.That(menu.SensitivitySaved, Is.False);
            Assert.That(menu.IsEmpty, Is.False);
        }

        [Test]
        public void GetToggle_ReturnsACopy()
        {
            var menu = MenuHierarchy.Read(new[] { Toggle(NativeFTManagerParameterNames.FaceTracking) });

            menu.GetToggle(NativeFTToggle.FaceTracking).defaultValue = false;

            Assert.That(menu.GetToggle(NativeFTToggle.FaceTracking).defaultValue, Is.True);
        }
    }
}
