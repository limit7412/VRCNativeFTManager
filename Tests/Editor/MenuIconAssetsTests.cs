using System;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;
using NativeFTManager.Infra;

namespace NativeFTManager.Tests
{
    public class MenuIconAssetsTests
    {
        [Test]
        public void EveryIcon_LoadsA256Texture()
        {
            foreach (var icon in Enum.GetValues(typeof(MenuIcon)).Cast<MenuIcon>().Where(icon => icon != MenuIcon.None))
            {
                var texture = MenuIconAssets.Load(icon);
                Assert.That(texture, Is.Not.Null, icon.ToString());
                Assert.That(texture.width, Is.EqualTo(256), icon.ToString());
                Assert.That(texture.height, Is.EqualTo(256), icon.ToString());
            }
        }

        [Test]
        public void None_LoadsNothing()
        {
            Assert.That(MenuIconAssets.Load(MenuIcon.None), Is.Null);
        }

        [Test]
        public void EveryToggleAndChannel_HasAnIcon()
        {
            foreach (var toggle in Enum.GetValues(typeof(NativeFTToggle)).Cast<NativeFTToggle>())
            {
                Assert.That(MenuIcons.For(toggle), Is.Not.EqualTo(MenuIcon.None));
            }

            foreach (var channel in Enum.GetValues(typeof(SensitivityChannel)).Cast<SensitivityChannel>())
            {
                Assert.That(MenuIcons.For(channel), Is.Not.EqualTo(MenuIcon.None));
            }
        }

        [Test]
        public void Guids_AreDistinct()
        {
            var guids = Enum.GetValues(typeof(MenuIcon)).Cast<MenuIcon>()
                .Where(icon => icon != MenuIcon.None)
                .Select(MenuIconAssets.GetGuid)
                .ToArray();
            Assert.That(guids.Distinct().Count(), Is.EqualTo(guids.Length));
        }
    }
}
