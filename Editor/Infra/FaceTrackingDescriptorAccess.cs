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
    /// SDK 3.10.6-beta.2で加わった型に触れるのはここだけにし、
    /// ほかの層はSDKに依存しないFaceTrackingSetDataで扱う。
    /// ベータの間に型が変わったときの直し先を1か所にするためである
    /// </summary>
    internal static class FaceTrackingDescriptorAccess
    {
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
