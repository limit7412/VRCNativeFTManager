using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using NativeFTManager.Domain;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// VRCAvatarDescriptorのFace Trackingの項目の読み書き
    ///
    /// SDK 3.10.6で加わった型に触れるのはここだけにし、
    /// ほかの層はSDKに依存しないFaceTrackingSetDataで扱う。
    /// SDKの更新で型が変わったときの直し先を1か所にするためである
    /// </summary>
    internal static class FaceTrackingDescriptorAccess
    {
        /// <summary>
        /// Lip SyncがViseme Blend Shape系のとき、ビゼームに割り当てたシェイプ（仕様書5.5）。
        /// ほかの方式では、残っている名前は口パクに使われないので返さない。
        /// 方式の名前で判定し、ネイティブFTで加わったViseme Blend Shapeの派生も含める
        /// </summary>
        public static IReadOnlyList<string> ReadLipSyncVisemeShapes(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null
                || descriptor.VisemeSkinnedMesh == null
                || descriptor.VisemeBlendShapes == null
                || descriptor.lipSync.ToString().IndexOf("VisemeBlendShape", StringComparison.Ordinal) < 0)
            {
                return new string[0];
            }

            return descriptor.VisemeBlendShapes.Where(name => !string.IsNullOrEmpty(name)).ToArray();
        }

        public static List<FaceTrackingSetData> ReadSets(VRCAvatarDescriptor descriptor)
        {
            var result = new List<FaceTrackingSetData>();
            if (descriptor == null || descriptor.faceTrackingBlendshapesSetList == null)
            {
                return result;
            }

            foreach (var set in descriptor.faceTrackingBlendshapesSetList)
            {
                if (set == null)
                {
                    continue;
                }

                result.Add(new FaceTrackingSetData
                {
                    Name = set.setName,
                    Weight = set.setWeight,
                    BlendShapeNames = set.faceTrackingBlendshapeNames,
                    BlendShapeMultipliers = set.faceTrackingBlendshapeMultipliers,
                    VisemeBlendShapeNames = set.visemeBlendshapeNames,
                    VisemeBlendShapeMultipliers = set.visemeBlendshapeMultipliers,
                });
            }

            return result;
        }

        public static void WriteSets(VRCAvatarDescriptor descriptor, IEnumerable<FaceTrackingSetData> sets)
        {
            descriptor.faceTrackingBlendshapesSetList = sets
                .Select(data => new VRCAvatarDescriptor.FaceTrackingBlendshapesSet
                {
                    setName = data.Name,
                    setWeight = data.Weight,
                    faceTrackingBlendshapeNames = data.BlendShapeNames,
                    faceTrackingBlendshapeMultipliers = data.BlendShapeMultipliers,
                    visemeBlendshapeNames = data.VisemeBlendShapeNames,
                    visemeBlendshapeMultipliers = data.VisemeBlendShapeMultipliers,
                })
                .ToList();
        }

        /// <summary>
        /// Descriptorのアニメーションレイヤーに設定されたコントローラ（既定のものは除く）
        /// </summary>
        public static IEnumerable<UnityEditor.Animations.AnimatorController> GetAnimatorControllers(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null)
            {
                yield break;
            }

            foreach (var layers in new[] { descriptor.baseAnimationLayers, descriptor.specialAnimationLayers })
            {
                if (layers == null)
                {
                    continue;
                }

                foreach (var layer in layers)
                {
                    if (!layer.isDefault && layer.animatorController is UnityEditor.Animations.AnimatorController controller)
                    {
                        yield return controller;
                    }
                }
            }
        }

        /// <summary>
        /// 既存のParameter BehaviorがtargetBlendSetNameかweightBlendSetNameに持つSet名
        /// </summary>
        public static HashSet<string> GetSetNamesReferencedByBehaviors(VRCAvatarDescriptor descriptor)
        {
            var names = new HashSet<string>();
            if (descriptor == null || descriptor.faceTrackingParameterBehaviors == null)
            {
                return names;
            }

            foreach (var behavior in descriptor.faceTrackingParameterBehaviors)
            {
                if (behavior == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(behavior.targetBlendSetName))
                {
                    names.Add(behavior.targetBlendSetName);
                }

                if (!string.IsNullOrEmpty(behavior.weightBlendSetName))
                {
                    names.Add(behavior.weightBlendSetName);
                }
            }

            return names;
        }

        /// <summary>
        /// 既存のParameter Behaviorsの見積もり材料（命令数の残りと、Face Tracking Operationsの見積もりに使う）
        /// </summary>
        public static List<BuildEstimate.ExistingBehavior> ReadExistingBehaviors(VRCAvatarDescriptor descriptor)
        {
            var result = new List<BuildEstimate.ExistingBehavior>();
            if (descriptor == null || descriptor.faceTrackingParameterBehaviors == null)
            {
                return result;
            }

            foreach (var behavior in descriptor.faceTrackingParameterBehaviors)
            {
                if (behavior == null)
                {
                    continue;
                }

                bool custom = behavior.combineMode == VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom;
                result.Add(new BuildEstimate.ExistingBehavior(
                    behavior.targetBlendshapeName,
                    behavior.inputTrackingKeys != null ? behavior.inputTrackingKeys.Length : 0,
                    custom ? behavior.customExpression ?? string.Empty : null));
            }

            return result;
        }

        /// <summary>
        /// 表情補正のParameter Behaviorsを、既存のものの後ろへ足す（仕様書8.1）。
        /// 入力はCustom式の中にキー名で書くので、Input Tracking Keysは空にする
        /// </summary>
        public static void AppendBehaviors(VRCAvatarDescriptor descriptor, IEnumerable<CorrectionBehaviorPlan> behaviors)
        {
            var list = descriptor.faceTrackingParameterBehaviors
                       ?? new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior>();
            foreach (var plan in behaviors)
            {
                list.Add(new VRCAvatarDescriptor.FaceTrackingParameterBehavior
                {
                    targetMode = ToTargetMode(plan.Mode),
                    outputParameterName = string.Empty,
                    targetBlendSetName = string.Empty,
                    targetBlendshapeName = plan.TargetBlendShape,
                    weightBlendSetName = plan.GateSetName ?? string.Empty,
                    inputTrackingKeys = new int[0],
                    inputTrackingKeyNames = new string[0],
                    inputInverted = new bool[0],
                    combineMode = VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom,
                    customExpression = plan.Expression,
                });
            }

            descriptor.faceTrackingParameterBehaviors = list;
        }

        private static VRCAvatarDescriptor.ParameterBehaviorTargetMode ToTargetMode(CorrectionMode mode)
        {
            switch (mode)
            {
                case CorrectionMode.ApplyToBlendshape:
                    return VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape;
                case CorrectionMode.LimitMax:
                    return VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax;
                case CorrectionMode.LimitMin:
                    return VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMin;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        /// <summary>
        /// Face Tracking Meshesにあるシェイプ名。カスタム補正の対象があるかを確かめるのに使う。
        /// Face Tracking Meshesが空ならリップシンクのメッシュを読む。「シェイプキーを自動でセット」が
        /// ビルド時にSetを作るときに読むメッシュと同じ選び方である。どちらも無ければ確かめようがないのでnullを返す
        /// </summary>
        public static HashSet<string> ReadFaceTrackingShapeNames(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return null;
            }

            var meshes = (descriptor.faceTrackingMeshes ?? new List<SkinnedMeshRenderer>())
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .Select(renderer => renderer.sharedMesh)
                .ToList();
            if (meshes.Count == 0 && descriptor.VisemeSkinnedMesh != null && descriptor.VisemeSkinnedMesh.sharedMesh != null)
            {
                meshes.Add(descriptor.VisemeSkinnedMesh.sharedMesh);
            }

            if (meshes.Count == 0)
            {
                return null;
            }

            var names = new HashSet<string>();
            foreach (var mesh in meshes.Distinct())
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    names.Add(mesh.GetBlendShapeName(i));
                }
            }

            return names;
        }

        /// <summary>
        /// シェイプ名を持つFace Tracking MeshのRendererの数を返す関数。見積もり（F-13）に使う。
        /// SDKはRendererごとに数えるので、同じMeshを使うRendererもまとめない（issue #42）。
        /// Face Tracking Meshesが空のときは、Rendererの数を数えられないので1つとして扱う
        /// </summary>
        public static Func<string, int> GetRendererCounter(VRCAvatarDescriptor descriptor)
        {
            var meshes = descriptor != null && descriptor.faceTrackingMeshes != null
                ? descriptor.faceTrackingMeshes
                    .Where(renderer => renderer != null && renderer.sharedMesh != null)
                    .Select(renderer => renderer.sharedMesh)
                    .ToArray()
                : new Mesh[0];
            if (meshes.Length == 0)
            {
                return shape => 1;
            }

            var cache = new Dictionary<string, int>();
            return shape =>
            {
                if (!cache.TryGetValue(shape, out var count))
                {
                    count = meshes.Count(mesh => mesh.GetBlendShapeIndex(shape) >= 0);
                    cache[shape] = count;
                }

                return count;
            };
        }

        /// <summary>
        /// 対応表のキーの並びがSDKの列挙体と一致するか確かめる。
        /// Setはキーの番号を添字にした配列なので、順序が1つずれると全部の割り当てが別のグループへ行く。
        /// 一致しないときはその内容を返し、一致するときはnull
        /// </summary>
        public static string FindKeyMismatch()
        {
            int sdkCount = (int)VRCAvatarDescriptor.FaceTrackingKey.Max;
            if (sdkCount != FaceTrackingGroupTable.KeyCount)
            {
                return $"FaceTrackingKey.Max = {sdkCount}, table = {FaceTrackingGroupTable.KeyCount}";
            }

            var names = FaceTrackingGroupTable.GetKeyNames();
            for (int index = 0; index < names.Count; index++)
            {
                var sdkName = Enum.GetName(typeof(VRCAvatarDescriptor.FaceTrackingKey), index);
                if (sdkName != names[index])
                {
                    return $"[{index}] SDK = {sdkName}, table = {names[index]}";
                }
            }

            return null;
        }

        /// <summary>
        /// Descriptorの静的なFace Trackingの設定をビルド用の複製へ書く（仕様書5.7、F-10）
        /// </summary>
        public static void ApplySettings(VRCAvatarDescriptor descriptor, NativeFTManagerComponent component)
        {
            descriptor.enableFaceTracking = true;

            if (component.setUseAllTrackingKeys)
            {
                descriptor.useAllTrackingKeys = true;
            }

            switch (component.visemeDampenOverride)
            {
                case VisemeDampenOverride.DoNotChange:
                    break;
                case VisemeDampenOverride.None:
                    descriptor.visemeDampenMode = VRCAvatarDescriptor.VisemeDampenMode.None;
                    break;
                case VisemeDampenOverride.DampenTracking:
                    descriptor.visemeDampenMode = VRCAvatarDescriptor.VisemeDampenMode.DampenTracking;
                    descriptor.visemeDampenAmount = component.visemeDampenAmount;
                    break;
                case VisemeDampenOverride.DampenVisemes:
                    descriptor.visemeDampenMode = VRCAvatarDescriptor.VisemeDampenMode.DampenVisemes;
                    descriptor.visemeDampenAmount = component.visemeDampenAmount;
                    break;
            }
        }

        public static bool GetUseAllTrackingKeys(VRCAvatarDescriptor descriptor)
        {
            return descriptor != null && descriptor.useAllTrackingKeys;
        }

        /// <summary>
        /// Eye Lookの上下左右の回転角（度、左右の目の大きい方）。Eye Lookが無効ならnull
        /// </summary>
        public static IReadOnlyList<float> GetEyeLookAngles(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null || !descriptor.enableEyeLook)
            {
                return null;
            }

            var settings = descriptor.customEyeLookSettings;
            return new[]
            {
                GetAngle(settings.eyesLookingUp),
                GetAngle(settings.eyesLookingDown),
                GetAngle(settings.eyesLookingLeft),
                GetAngle(settings.eyesLookingRight),
            };
        }

        /// <summary>
        /// Eye Lookの設定のうち、ネイティブFTで目を動かすかに関わるもの（仕様書5.7、I-07）
        /// </summary>
        public static EyeLookSettings ReadEyeLookSettings(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return null;
            }

            var settings = descriptor.customEyeLookSettings;
            return new EyeLookSettings
            {
                Enabled = descriptor.enableEyeLook,
                // SDKの列挙体の型名に依らないよう、値で写す（値の並びはEyeGazeMode・EyelidTypeのコメントのとおり）
                GazeMode = (EyeGazeMode)Convert.ToInt32(settings.eyeGazeMode),
                EyelidType = (Domain.EyelidType)Convert.ToInt32(settings.eyelidType),
                HasEyeBones = settings.leftEye != null || settings.rightEye != null,
            };
        }

        /// <summary>
        /// Eye Lookの有効・無効、Eye Gaze Mode、Eyelids Typeを書く。ビルド用の複製にだけ使う
        /// </summary>
        public static void WriteEyeLookSettings(VRCAvatarDescriptor descriptor, EyeLookSettings value)
        {
            descriptor.enableEyeLook = value.Enabled;
            var settings = descriptor.customEyeLookSettings;
            settings.eyeGazeMode = ToEnum(settings.eyeGazeMode, (int)value.GazeMode);
            settings.eyelidType = ToEnum(settings.eyelidType, (int)value.EyelidType);
            descriptor.customEyeLookSettings = settings;
        }

        /// <summary>
        /// 型を書き先のフィールドから推論させて、整数を同じ列挙体の値にする
        /// </summary>
        private static T ToEnum<T>(T current, int value) where T : struct, Enum
        {
            return (T)Enum.ToObject(typeof(T), value);
        }

        private static float GetAngle(VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations rotations)
        {
            if (rotations == null)
            {
                return 0f;
            }

            return Mathf.Max(
                Quaternion.Angle(Quaternion.identity, rotations.left),
                Quaternion.Angle(Quaternion.identity, rotations.right));
        }
    }
}
