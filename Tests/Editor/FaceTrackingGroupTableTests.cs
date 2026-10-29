using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// UEキーとグループの対応（仕様書の付録B）
    /// </summary>
    /// <remarks>
    /// 引数に internal の <see cref="FaceTrackingGroup"/> を取るテストがあるため、クラスも internal にする。
    /// public のままだとメソッドの公開範囲が引数の型より広くなり CS0051 になる。
    /// NUnit 3 は internal のフィクスチャも検出する。
    /// </remarks>
    internal class FaceTrackingGroupTableTests
    {
        [Test]
        public void KeyNames_CoverEveryKey()
        {
            Assert.That(FaceTrackingGroupTable.GetKeyNames().Count, Is.EqualTo(FaceTrackingGroupTable.KeyCount));
        }

        [TestCase(0, FaceTrackingGroup.Eye)]
        [TestCase(7, FaceTrackingGroup.Eye)]
        [TestCase(8, FaceTrackingGroup.EyeClosed)]
        [TestCase(9, FaceTrackingGroup.EyeClosed)]
        [TestCase(10, FaceTrackingGroup.Pupil)]
        [TestCase(13, FaceTrackingGroup.Pupil)]
        [TestCase(14, FaceTrackingGroup.Eye)]
        [TestCase(17, FaceTrackingGroup.Eye)]
        [TestCase(18, FaceTrackingGroup.Brow)]
        [TestCase(25, FaceTrackingGroup.Brow)]
        [TestCase(26, FaceTrackingGroup.Lip)]
        [TestCase(36, FaceTrackingGroup.Lip)]
        [TestCase(99, FaceTrackingGroup.Lip)]
        [TestCase(100, FaceTrackingGroup.Other)]
        [TestCase(101, FaceTrackingGroup.Other)]
        public void GetGroup_FollowsTheSpecTable(int key, FaceTrackingGroup expected)
        {
            Assert.That(FaceTrackingGroupTable.GetGroup(key), Is.EqualTo(expected));
        }

        [Test]
        public void KeyNames_MatchTheGroupBoundaries()
        {
            var names = FaceTrackingGroupTable.GetKeyNames();

            Assert.That(names[8], Is.EqualTo("EyeClosedRight"));
            Assert.That(names[10], Is.EqualTo("EyeDilationRight"));
            Assert.That(names[18], Is.EqualTo("BrowPinchRight"));
            Assert.That(names[36], Is.EqualTo("JawOpen"));
            Assert.That(names[100], Is.EqualTo("NeckFlexRight"));
        }

        [Test]
        public void GetGroup_RejectsOutOfRangeKeys()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => FaceTrackingGroupTable.GetGroup(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => FaceTrackingGroupTable.GetGroup(FaceTrackingGroupTable.KeyCount));
        }
    }
}
