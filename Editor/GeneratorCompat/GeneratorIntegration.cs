using System.Linq;
using UnityEditor;
using UnityEngine;
using ARKitBlendShapeGenerator;
using NativeFTManager.Infra;

namespace NativeFTManager.GeneratorCompat
{
    /// <summary>
    /// Kx VRC ARKit BlendShape Generatorとの連携の実装（仕様書10.1、F-12）
    ///
    /// エディタの読み込み時に本体の窓口へ登録する。Generatorの既定（Set書き込みはオフ）は変えず、
    /// Managerがビルド用の複製の上でだけ有効にする。Jerry'sを使い続ける利用者を壊さないため
    /// </summary>
    [InitializeOnLoad]
    internal sealed class GeneratorIntegration : IGeneratorIntegration
    {
        static GeneratorIntegration()
        {
            GeneratorIntegrationRegistry.Current = new GeneratorIntegration();
        }

        public string SetName => FaceTrackingSetNames.Generator;

        public bool IsPresent(GameObject avatarRoot)
        {
            return Find(avatarRoot) != null;
        }

        public bool IsSetWriteEnabled(GameObject avatarRoot)
        {
            var generator = Find(avatarRoot);
            return generator != null && generator.writeFaceTrackingBlendshapesSet;
        }

        public void EnableSetWrite(GameObject avatarRoot)
        {
            var generator = Find(avatarRoot);
            if (generator != null)
            {
                generator.writeFaceTrackingBlendshapesSet = true;
            }
        }

        /// <summary>
        /// アバター内のGenerator。Generator自身の重複排除で1つに絞られている前提で、最初の1つを使う
        /// </summary>
        private static ARKitBlendShapeGeneratorComponent Find(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return null;
            }

            return avatarRoot.GetComponentsInChildren<ARKitBlendShapeGeneratorComponent>(true)
                .FirstOrDefault(component => component != null);
        }
    }
}
