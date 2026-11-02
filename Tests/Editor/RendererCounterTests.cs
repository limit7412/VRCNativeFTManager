using System.Collections.Generic;
using NUnit.Framework;
using NativeFTManager.Infra;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// 見積もり（F-13）でシェイプを持つRendererを数える部分の検証。
    /// SDKはFace Tracking MeshのRendererごとに数えるので、同じMeshのRendererもまとめない（issue #42）
    /// </summary>
    public class RendererCounterTests
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

        private Mesh MeshWith(params string[] shapes)
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f) },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (var shape in shapes)
            {
                mesh.AddBlendShapeFrame(shape, 100f, new[] { Vector3.up, Vector3.up, Vector3.zero }, null, null);
            }

            _created.Add(mesh);
            return mesh;
        }

        private VRCAvatarDescriptor DescriptorWith(params Mesh[] meshes)
        {
            var root = new GameObject("Avatar");
            _created.Add(root);
            var descriptor = root.AddComponent<VRCAvatarDescriptor>();
            descriptor.faceTrackingMeshes = new List<SkinnedMeshRenderer>();
            for (int index = 0; index < meshes.Length; index++)
            {
                var child = new GameObject("Body" + index);
                child.transform.SetParent(root.transform);
                var renderer = child.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = meshes[index];
                descriptor.faceTrackingMeshes.Add(renderer);
            }

            return descriptor;
        }

        [Test]
        public void SameMeshOnTwoRenderers_CountsBoth()
        {
            var mesh = MeshWith("JawOpen");
            var counter = FaceTrackingDescriptorAccess.GetRendererCounter(DescriptorWith(mesh, mesh));

            Assert.That(counter("JawOpen"), Is.EqualTo(2));
        }

        [Test]
        public void DifferentMeshes_CountOnlyRenderersWithTheShape()
        {
            var counter = FaceTrackingDescriptorAccess.GetRendererCounter(
                DescriptorWith(MeshWith("JawOpen", "EyeClosedLeft"), MeshWith("JawOpen")));

            Assert.That(counter("JawOpen"), Is.EqualTo(2));
            Assert.That(counter("EyeClosedLeft"), Is.EqualTo(1));
            Assert.That(counter("TongueOut"), Is.EqualTo(0));
        }

        [Test]
        public void NoFaceTrackingMeshes_CountsOne()
        {
            var counter = FaceTrackingDescriptorAccess.GetRendererCounter(DescriptorWith());

            Assert.That(counter("JawOpen"), Is.EqualTo(1));
        }
    }
}
