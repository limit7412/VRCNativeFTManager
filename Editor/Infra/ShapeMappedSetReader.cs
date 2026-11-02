using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using NativeFTManager.Domain;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// メッシュのシェイプ名から作った分割前のSetと、そのシェイプを読んだメッシュ
    /// </summary>
    internal sealed class ShapeMappedSetSource
    {
        public ShapeMappedSetSource(FaceTrackingSetData set, IReadOnlyList<SkinnedMeshRenderer> renderers)
        {
            Set = set;
            Renderers = renderers;
        }

        public FaceTrackingSetData Set { get; }

        /// <summary>シェイプを読んだメッシュ。DescriptorのFace Tracking Meshesに無ければ、ビルド時に加える</summary>
        public IReadOnlyList<SkinnedMeshRenderer> Renderers { get; }
    }

    /// <summary>
    /// Descriptorに1つもSetが無いときに、Generatorの対応表でメッシュのシェイプからSetを作る（仕様書5.1）
    ///
    /// 読むメッシュは、DescriptorのFace Tracking Meshesに入っていればそれら、空ならリップシンクのメッシュ。
    /// どちらも無い、Generatorが対応表を公開していない、または割り当てが1つも無いときはnullを返し、
    /// 呼び出し側は従来どおりE-03にする
    /// </summary>
    internal static class ShapeMappedSetReader
    {
        public static ShapeMappedSetSource Read(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null || !GeneratorAccess.CanMapShapes)
            {
                return null;
            }

            var renderers = GetSourceRenderers(descriptor);
            if (renderers.Count == 0)
            {
                return null;
            }

            var shapeNames = new List<string>();
            var seen = new HashSet<string>();
            foreach (var mesh in renderers.Select(renderer => renderer.sharedMesh).Distinct())
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    var name = mesh.GetBlendShapeName(i);
                    if (seen.Add(name))
                    {
                        shapeNames.Add(name);
                    }
                }
            }

            var set = ShapeMappedSet.Create(GeneratorAccess.FindBlendShapeNames(shapeNames));
            return set != null ? new ShapeMappedSetSource(set, renderers) : null;
        }

        /// <summary>
        /// Setを作るのと同じメッシュで、ARKit名のシェイプキーからUE名のシェイプキーを書き足す（仕様書5.1）。
        /// 書き足したメッシュは複製に差し替わる。ビルド用の複製にだけ使う。
        /// Generatorが変換を公開していない版なら何もしない
        /// </summary>
        public static void ConvertToUnifiedExpressions(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null || !GeneratorAccess.CanConvertToUnifiedExpressions)
            {
                return;
            }

            foreach (var renderer in GetSourceRenderers(descriptor))
            {
                GeneratorAccess.ConvertToUnifiedExpressions(renderer);
            }
        }

        /// <summary>
        /// Setが動かすメッシュをDescriptorのFace Tracking Meshesへ加え、Face Trackingを有効にする。ビルド用の複製にだけ使う
        /// </summary>
        public static void AddToDescriptor(VRCAvatarDescriptor descriptor, ShapeMappedSetSource source)
        {
            if (descriptor.faceTrackingMeshes == null)
            {
                descriptor.faceTrackingMeshes = new List<SkinnedMeshRenderer>();
            }

            foreach (var renderer in source.Renderers)
            {
                if (!descriptor.faceTrackingMeshes.Contains(renderer))
                {
                    descriptor.faceTrackingMeshes.Add(renderer);
                }
            }
        }

        private static List<SkinnedMeshRenderer> GetSourceRenderers(VRCAvatarDescriptor descriptor)
        {
            var meshes = (descriptor.faceTrackingMeshes ?? new List<SkinnedMeshRenderer>())
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .Distinct()
                .ToList();
            if (meshes.Count > 0)
            {
                return meshes;
            }

            var lipSync = descriptor.VisemeSkinnedMesh;
            return lipSync != null && lipSync.sharedMesh != null
                ? new List<SkinnedMeshRenderer> { lipSync }
                : new List<SkinnedMeshRenderer>();
        }
    }
}
