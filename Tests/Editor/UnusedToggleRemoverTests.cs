using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using NativeFTManager.Infra;

namespace NativeFTManager.Tests
{
    public class UnusedToggleRemoverTests
    {
        private GameObject _avatar;
        private AnimatorController _fx;
        private VRCExpressionsMenu _root;
        private VRCExpressionsMenu _sub;
        private VRCExpressionParameters _parameters;

        [SetUp]
        public void SetUp()
        {
            _avatar = new GameObject("Avatar");
            var descriptor = _avatar.AddComponent<VRCAvatarDescriptor>();

            _fx = new AnimatorController();
            _fx.AddParameter("KxNFTM/FaceTracking", AnimatorControllerParameterType.Bool);
            _fx.AddLayer("KxNFTM/Other");
            var stateMachine = _fx.layers[0].stateMachine;
            var on = stateMachine.AddState("On");
            var off = stateMachine.AddState("Off");
            off.AddTransition(on).AddCondition(AnimatorConditionMode.If, 0f, "KxNFTM/FaceTracking");

            _sub = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            _sub.controls = new List<VRCExpressionsMenu.Control> { Toggle("Face Tracking", "KxNFTM/FaceTracking"), Toggle("Pupil", "KxNFTM/Pupil") };
            _root = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            _root.controls = new List<VRCExpressionsMenu.Control>
            {
                Toggle("Hat", "Hat"),
                new VRCExpressionsMenu.Control
                {
                    name = "Face Tracking",
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = _sub,
                },
            };
            descriptor.expressionsMenu = _root;

            _parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            _parameters.parameters = new[]
            {
                Parameter("Hat"),
                Parameter("KxNFTM/FaceTracking"),
                Parameter("KxNFTM/Pupil"),
            };
            descriptor.expressionParameters = _parameters;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_avatar);
            Object.DestroyImmediate(_fx);
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_sub);
            Object.DestroyImmediate(_parameters);
        }

        [Test]
        public void Remove_TakesOutOnlyTheTogglesNoConditionReads()
        {
            var removed = UnusedToggleRemover.Remove(
                _avatar.GetComponent<VRCAvatarDescriptor>(),
                new[] { _fx },
                new[] { "KxNFTM/FaceTracking", "KxNFTM/Pupil" });

            Assert.That(removed, Is.EqualTo(new[] { "KxNFTM/Pupil" }));
            Assert.That(_sub.controls.ConvertAll(control => control.name), Is.EqualTo(new[] { "Face Tracking" }));
            Assert.That(_root.controls.Count, Is.EqualTo(2));
            Assert.That(
                System.Array.ConvertAll(_parameters.parameters, parameter => parameter.name),
                Is.EqualTo(new[] { "Hat", "KxNFTM/FaceTracking" }));
        }

        [Test]
        public void Remove_LeavesAToggle_ThatAnotherControllerReads()
        {
            var gesture = new AnimatorController();
            gesture.AddParameter("KxNFTM/Pupil", AnimatorControllerParameterType.Bool);
            gesture.AddLayer("User");
            var stateMachine = gesture.layers[0].stateMachine;
            stateMachine.AddState("A").AddTransition(stateMachine.AddState("B"))
                .AddCondition(AnimatorConditionMode.If, 0f, "KxNFTM/Pupil");

            var removed = UnusedToggleRemover.Remove(
                _avatar.GetComponent<VRCAvatarDescriptor>(),
                new[] { _fx, gesture },
                new[] { "KxNFTM/FaceTracking", "KxNFTM/Pupil" });

            Assert.That(removed, Is.Empty);
            Object.DestroyImmediate(gesture);
        }

        [Test]
        public void Remove_LeavesAToggle_ThatAParameterDriverCopies()
        {
            var gesture = new AnimatorController();
            gesture.AddLayer("User");
            var state = gesture.layers[0].stateMachine.AddState("Copy");
            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
            {
                new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Copy,
                    source = "KxNFTM/Pupil",
                    name = "UserPupil",
                },
            };

            var removed = UnusedToggleRemover.Remove(
                _avatar.GetComponent<VRCAvatarDescriptor>(),
                new[] { _fx, gesture },
                new[] { "KxNFTM/FaceTracking", "KxNFTM/Pupil" });

            Assert.That(removed, Is.Empty);
            Object.DestroyImmediate(gesture);
        }

        private static VRCExpressionsMenu.Control Toggle(string name, string parameter)
        {
            return new VRCExpressionsMenu.Control
            {
                name = name,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter },
            };
        }

        private static VRCExpressionParameters.Parameter Parameter(string name)
        {
            return new VRCExpressionParameters.Parameter
            {
                name = name,
                valueType = VRCExpressionParameters.ValueType.Bool,
            };
        }
    }
}
