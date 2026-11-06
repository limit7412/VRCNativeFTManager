using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NativeFTManager.Domain;
using NativeFTManager.Infra;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// booth版でGeneratorを取得して取り込む段取りの検証。
    ///
    /// 取りに行く名前は、booth用zipへ同梱するときの`.github/scripts/fetch_generator.py`とそろえる。
    /// 版の下限は、package.jsonのvpmDependenciesと食い違わないことを確かめる
    /// </summary>
    public class GeneratorInstallPlanTests
    {
        private const string Root = GeneratorInstallPlan.InstallRoot;

        // InstallLocationはinternalなので、publicなテストメソッドの引数には取れない。1つのテストの中で並べる
        [Test]
        public void ShouldOffer_OnlyForBoothWithoutTheGenerator()
        {
            Assert.That(GeneratorInstallPlan.ShouldOffer(InstallLocation.Booth, false), Is.True);
            Assert.That(GeneratorInstallPlan.ShouldOffer(InstallLocation.Booth, true), Is.False);
            Assert.That(GeneratorInstallPlan.ShouldOffer(InstallLocation.Vpm, false), Is.False);
            Assert.That(GeneratorInstallPlan.ShouldOffer(InstallLocation.Unknown, false), Is.False);
        }

        [TestCase("v0.2.5")]
        [TestCase("0.2.5")]
        public void AssetNames_MatchTheGeneratorRelease(string tag)
        {
            Assert.That(
                GeneratorInstallPlan.BoothAssetName(tag), Is.EqualTo("VRCARKitBlendShapeGenerator_0.2.5.zip"));
            Assert.That(
                GeneratorInstallPlan.UnityPackageName(tag),
                Is.EqualTo("VRCARKitBlendShapeGenerator_0.2.5.unitypackage"));
        }

        [TestCase("0.2.3", true)]
        [TestCase("v0.3.0", true)]
        [TestCase("0.2.2", false)]
        [TestCase("0.2.4-test1", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void SatisfiesMinimum_AcceptsStableReleasesFromTheMinimum(string tag, bool expected)
        {
            Assert.That(GeneratorInstallPlan.SatisfiesMinimum(tag), Is.EqualTo(expected));
        }

        [Test]
        public void TrySelectBoothAsset_PicksTheBoothZipByName()
        {
            var assets = new[]
            {
                new ReleaseAsset("com.qazx7412.kx-vrc-arkit-blendshape-generator-0.2.5.zip", "https://example.com/vpm", ""),
                new ReleaseAsset("VRCARKitBlendShapeGenerator_0.2.5.zip", "https://example.com/booth", "sha256:00"),
            };

            Assert.That(GeneratorInstallPlan.TrySelectBoothAsset(assets, "v0.2.5", out var selected), Is.True);
            Assert.That(selected.DownloadUrl, Is.EqualTo("https://example.com/booth"));
            Assert.That(GeneratorInstallPlan.TrySelectBoothAsset(assets, "0.2.6", out _), Is.False);
        }

        [Test]
        public void IsExpectedPackage_RequiresTheGeneratorAssemblies()
        {
            var pathnames = new[]
            {
                Root,
                Root + "/package.json",
                Root + "/Editor/ARKitBlendShapeGenerator.Editor.asmdef",
                Root + "/Runtime/ARKitBlendShapeGenerator.Runtime.asmdef",
            };

            Assert.That(GeneratorInstallPlan.IsExpectedPackage(pathnames), Is.True);
            Assert.That(GeneratorInstallPlan.IsExpectedPackage(new[] { Root, Root + "/package.json" }), Is.False);
            Assert.That(GeneratorInstallPlan.IsExpectedPackage(null), Is.False);
        }

        [Test]
        public void MinimumVersion_MatchesThePackageManifest()
        {
            var manifestPath = Path.Combine(PackageLocation.Root, "package.json");
            Assert.That(File.Exists(manifestPath), Is.True, $"{manifestPath}が見つからない");

            var pattern = Regex.Escape("\"" + GeneratorInstallPlan.PackageName + "\"") + @"\s*:\s*"">=\s*([^""\s]+)\s*""";
            var match = Regex.Match(File.ReadAllText(manifestPath), pattern);

            Assert.That(match.Success, Is.True, "package.jsonのvpmDependenciesにGeneratorの下限が無い");
            Assert.That(match.Groups[1].Value, Is.EqualTo(GeneratorInstallPlan.MinimumVersion));
        }
    }
}
