using System;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Infra;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// Generatorをリフレクションで引く部分の検証
    ///
    /// CIはGeneratorを入れた構成と入れない構成の両方で回す。
    /// どちらの構成かはテストのasmdefのversionDefines（KX_TEST_GENERATOR_INSTALLED）で見分ける。
    /// Generatorを入れた構成では、型やフィールドの名前がGenerator側の変更で食い違ったときにここで落ちる
    /// </summary>
    public class GeneratorAccessTests
    {
#if KX_TEST_GENERATOR_INSTALLED
        [Test]
        public void IsInstalled_WithGenerator_IsTrue()
        {
            Assert.That(GeneratorAccess.IsInstalled, Is.True);
        }

        [Test]
        public void SetName_MatchesGeneratorConstant()
        {
            var setNames = FindType(GeneratorAccess.SetNamesTypeName);
            Assert.That(setNames, Is.Not.Null);
            var constant = (string)setNames.GetField("Generator").GetValue(null);

            Assert.That(GeneratorAccess.DefaultSetName, Is.EqualTo(constant));
            Assert.That(GeneratorAccess.SetName, Is.EqualTo(constant));
        }

        [Test]
        public void EnableSetWrite_TurnsOnTheGeneratorField()
        {
            var root = new GameObject("Avatar");
            try
            {
                var child = new GameObject("Generator");
                child.transform.SetParent(root.transform);
                child.AddComponent(FindType(GeneratorAccess.ComponentTypeName));

                Assert.That(GeneratorAccess.IsPresent(root), Is.True);
                Assert.That(GeneratorAccess.IsSetWriteEnabled(root), Is.False);

                GeneratorAccess.EnableSetWrite(root);

                Assert.That(GeneratorAccess.IsSetWriteEnabled(root), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FindBlendShapeNames_MapsArkitShapes_WhenTheGeneratorPublishesItsTable()
        {
            // 対応表の公開はGenerator 0.2.2から。依存の下限（0.2.3）を満たす版なら必ずある
            Assert.That(FindType(GeneratorAccess.SetMappingTypeName), Is.Not.Null);
            Assert.That(GeneratorAccess.CanMapShapes, Is.True);
            var names = GeneratorAccess.FindBlendShapeNames(new[] { "eyeBlinkLeft", "jawOpen" });
            var keys = NativeFTManager.Domain.FaceTrackingGroupTable.GetKeyNames().ToList();

            Assert.That(names.Length, Is.EqualTo(NativeFTManager.Domain.FaceTrackingGroupTable.KeyCount));
            Assert.That(names[keys.IndexOf("EyeClosedLeft")], Is.EqualTo("eyeBlinkLeft"));
            Assert.That(names[keys.IndexOf("JawOpen")], Is.EqualTo("jawOpen"));
            Assert.That(names[keys.IndexOf("TongueRoll")], Is.EqualTo(string.Empty));
        }

        [Test]
        public void ConvertToUnifiedExpressions_AddsUeShapes_WhenTheGeneratorPublishesTheConversion()
        {
            // UE変換の公開はGenerator 0.2.3から。依存の下限（0.2.3）を満たす版なら必ずある
            Assert.That(FindType(GeneratorAccess.ConversionTypeName), Is.Not.Null);
            Assert.That(GeneratorAccess.CanConvertToUnifiedExpressions, Is.True);
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f) },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.AddBlendShapeFrame("browInnerUp", 100f, new[] { Vector3.up, Vector3.up, Vector3.zero }, null, null);
            var gameObject = new GameObject("Body");
            try
            {
                var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;

                var converted = GeneratorAccess.ConvertToUnifiedExpressions(renderer);

                Assert.That(converted, Is.EquivalentTo(new[] { "BrowInnerUpLeft", "BrowInnerUpRight" }));
                Assert.That(renderer.sharedMesh, Is.Not.SameAs(mesh));
                Assert.That(mesh.blendShapeCount, Is.EqualTo(1));
                Object.DestroyImmediate(renderer.sharedMesh);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(mesh);
            }
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
#else
        [Test]
        public void IsInstalled_WithoutGenerator_IsFalse()
        {
            Assert.That(GeneratorAccess.IsInstalled, Is.False);
        }

        [Test]
        public void WithoutGenerator_BehavesAsIfTheAvatarHasNoGenerator()
        {
            var root = new GameObject("Avatar");
            try
            {
                Assert.That(GeneratorAccess.IsPresent(root), Is.False);
                Assert.That(GeneratorAccess.IsSetWriteEnabled(root), Is.False);
                Assert.DoesNotThrow(() => GeneratorAccess.EnableSetWrite(root));
                Assert.That(GeneratorAccess.SetName, Is.EqualTo(GeneratorAccess.DefaultSetName));
                Assert.That(GeneratorAccess.CanMapShapes, Is.False);
                Assert.That(GeneratorAccess.FindBlendShapeNames(new[] { "jawOpen" }), Is.Null);
                Assert.That(GeneratorAccess.CanConvertToUnifiedExpressions, Is.False);
                Assert.That(GeneratorAccess.ConvertToUnifiedExpressions(null), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
#endif
    }
}
