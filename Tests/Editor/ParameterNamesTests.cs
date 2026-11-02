using NUnit.Framework;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// 公開パラメーター、Set、FXレイヤーの名前を固定する
    ///
    /// 名前は保存値と利用者のアニメーターから参照されるので変えない（仕様書17章のU-1）。
    /// このテストが落ちたら、名前を変えてしまっていないかを確かめる。
    /// </summary>
    public class ParameterNamesTests
    {
        [Test]
        public void Prefix_IsKxNFTM()
        {
            Assert.AreEqual("KxNFTM/", NativeFTManagerParameterNames.Prefix);
            Assert.AreEqual("KxNFTM", NativeFTManagerParameterNames.ShortPrefix);
        }

        [TestCase(NativeFTManagerParameterNames.FaceTracking, "KxNFTM/FaceTracking")]
        [TestCase(NativeFTManagerParameterNames.EyeTracking, "KxNFTM/EyeTracking")]
        [TestCase(NativeFTManagerParameterNames.LipTracking, "KxNFTM/LipTracking")]
        [TestCase(NativeFTManagerParameterNames.Pupil, "KxNFTM/Pupil")]
        [TestCase(NativeFTManagerParameterNames.Visemes, "KxNFTM/Visemes")]
        [TestCase(NativeFTManagerParameterNames.Emulation, "KxNFTM/Emulation")]
        [TestCase(NativeFTManagerParameterNames.Limits, "KxNFTM/Limits")]
        [TestCase(NativeFTManagerParameterNames.EyeSync, "KxNFTM/EyeSync")]
        [TestCase(NativeFTManagerParameterNames.SensitivityEye, "KxNFTM/Sensitivity/Eye")]
        [TestCase(NativeFTManagerParameterNames.SensitivityLip, "KxNFTM/Sensitivity/Lip")]
        public void PublicParameterNames_AreFixed(string actual, string expected)
        {
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void SetAndLayerPrefixes_AreFixed()
        {
            Assert.AreEqual("KxNFTM/", NativeFTManagerParameterNames.SetPrefix);
            Assert.AreEqual("KxNFTM/Gate/", NativeFTManagerParameterNames.GateSetPrefix);
            Assert.AreEqual("KxNFTM ", NativeFTManagerParameterNames.LayerPrefix);
        }
    }
}
