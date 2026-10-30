using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using NativeFTManager.Infra;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// 導入されているSDKのState Behaviorへ、Setの重みを書けるかを確かめる。
    /// SDKの形が変わったとき、実機で調べる前にここで気づけるようにする
    /// </summary>
    public class FaceTrackingStateBehaviorWriterTests
    {
        private AnimatorController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = new AnimatorController();
            _controller.AddLayer("Layer");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_controller);
        }

        [Test]
        public void TryResolve_FindsTheBehaviorOfTheInstalledSdk()
        {
            Assert.That(FaceTrackingStateBehaviorWriter.TryResolve(out var failure), Is.True, failure);
        }

        [Test]
        public void Add_WritesEverySet_AndTheNamesCanBeReadBack()
        {
            var state = _controller.layers[0].stateMachine.AddState("On");

            var added = FaceTrackingStateBehaviorWriter.Add(
                state, new[] { ("KxNFTM/Set/Eye", 1f), ("KxNFTM/Set/Brow", 1f) }, 8f);

            Assert.That(added, Is.Not.Empty);
            Assert.That(state.behaviours, Is.Not.Empty);
            Assert.That(
                FaceTrackingStateBehaviorWriter.CollectReferencedSetNames(new[] { _controller }).OrderBy(name => name),
                Is.EqualTo(new[] { "KxNFTM/Set/Brow", "KxNFTM/Set/Eye" }));
        }
    }
}
