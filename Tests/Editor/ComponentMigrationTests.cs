using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// 旧版で保存したコンポーネントの読み込み
    ///
    /// 名前や型を変えたフィールドは、FormerlySerializedAsで旧名から読む。
    /// 実際の保存形式で確かめるため、プレハブのYAMLを旧版の形に書き換えて読み直す
    /// </summary>
    public class ComponentMigrationTests
    {
        private const string Folder = "Assets/NativeFTManagerTests";
        private const string PrefabPath = Folder + "/ComponentMigration.prefab";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        /// <summary>
        /// 旧名で保存した「シェイプキーを自動でセット」を読む。
        /// 0.1.0-test10までは列挙体GeneratorIntegration（Off=0、Auto=1）、その後はbool useGeneratorComponentだった
        /// </summary>
        [TestCase("generatorIntegration", 0, false)]
        [TestCase("generatorIntegration", 1, true)]
        [TestCase("useGeneratorComponent", 0, false)]
        [TestCase("useGeneratorComponent", 1, true)]
        public void AutoSetShapeKeys_ReadsFormerNames(string formerName, int saved, bool expected)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "NativeFTManagerTests");
            }

            var gameObject = new GameObject("ComponentMigration");
            var component = gameObject.AddComponent<NativeFTManagerComponent>();
            component.autoSetShapeKeys = !expected;
            PrefabUtility.SaveAsPrefabAsset(gameObject, PrefabPath);
            Object.DestroyImmediate(gameObject);

            var yaml = File.ReadAllText(PrefabPath);
            var current = "autoSetShapeKeys: " + (expected ? 0 : 1);
            StringAssert.Contains(current, yaml);
            File.WriteAllText(PrefabPath, yaml.Replace(current, formerName + ": " + saved));
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab.GetComponent<NativeFTManagerComponent>().autoSetShapeKeys, Is.EqualTo(expected));
        }
    }
}
