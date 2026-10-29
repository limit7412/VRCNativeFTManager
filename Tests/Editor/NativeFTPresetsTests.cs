using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NativeFTManager.Tests
{
    public class NativeFTPresetsTests
    {
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

        private NativeFTManagerComponent CreateComponent()
        {
            var gameObject = new GameObject("NativeFTPresetsTests");
            _created.Add(gameObject);
            return gameObject.AddComponent<NativeFTManagerComponent>();
        }

        [TestCase(NativeFTPreset.UE)]
        [TestCase(NativeFTPreset.ARKit)]
        [TestCase(NativeFTPreset.Minimal)]
        public void Apply_ThenMatches(NativeFTPreset preset)
        {
            var component = CreateComponent();
            NativeFTPresets.Apply(component, preset);

            Assert.That(NativeFTPresets.Matches(component, preset), Is.True);
        }

        [Test]
        public void Matches_ChangedToggle_IsNoLongerThePreset()
        {
            var component = CreateComponent();
            NativeFTPresets.Apply(component, NativeFTPreset.UE);
            component.toggles.First(t => t.toggle == NativeFTToggle.Pupil).showInMenu = false;

            Assert.That(NativeFTPresets.Matches(component, NativeFTPreset.UE), Is.False);
            Assert.That(NativeFTPresets.Matches(component, NativeFTPreset.ARKit), Is.True);
        }

        [Test]
        public void CreateToggles_EyeSyncIsOffByDefault()
        {
            foreach (var preset in new[] { NativeFTPreset.UE, NativeFTPreset.ARKit, NativeFTPreset.Minimal })
            {
                var eyeSync = NativeFTPresets.CreateToggles(preset).Single(t => t.toggle == NativeFTToggle.EyeSync);
                Assert.That(eyeSync.defaultValue, Is.False, preset.ToString());
            }
        }

        [Test]
        public void Minimal_DisablesCorrections()
        {
            var component = CreateComponent();
            NativeFTPresets.Apply(component, NativeFTPreset.Minimal);

            Assert.That(component.enableEmulation || component.enableLimits || component.enableEyeSync, Is.False);
        }
    }
}
