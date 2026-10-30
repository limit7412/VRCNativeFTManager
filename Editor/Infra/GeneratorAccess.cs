using System.Linq;
using UnityEngine;
using ARKitBlendShapeGenerator;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// Kx VRC ARKit BlendShape Generatorとの連携（仕様書10.1、F-12）
    ///
    /// Generatorは必須の依存なので、型を直接参照する。Generatorの既定（Set書き込みはオフ）は変えず、
    /// Managerがビルド用の複製の上でだけ有効にする。Jerry'sを使い続ける利用者を壊さないため
    /// </summary>
    internal static class GeneratorAccess
    {
        /// <summary>GeneratorがDescriptorへ書くSetの名前</summary>
        public static string SetName => FaceTrackingSetNames.Generator;

        /// <summary>アバターにGeneratorのコンポーネントがあるか</summary>
        public static bool IsPresent(GameObject avatarRoot)
        {
            return Find(avatarRoot) != null;
        }

        /// <summary>アバターのGeneratorでSet書き込みが有効か</summary>
        public static bool IsSetWriteEnabled(GameObject avatarRoot)
        {
            var generator = Find(avatarRoot);
            return generator != null && generator.writeFaceTrackingBlendshapesSet;
        }

        /// <summary>アバターのGeneratorのSet書き込みを有効にする。ビルド用の複製にだけ使う</summary>
        public static void EnableSetWrite(GameObject avatarRoot)
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
