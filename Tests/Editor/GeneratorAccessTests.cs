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
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
#endif
    }
}
