using System;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// Blendshapes Set 1件分の内容（VRCAvatarDescriptor.FaceTrackingBlendshapesSetの写し）
    ///
    /// SDKの型に依存しない形で持ち、Descriptorとの読み書きはInfra側が行う。
    /// 配列はSDKと同じく、キー（またはビゼーム）の番号を添字にする
    /// </summary>
    internal sealed class FaceTrackingSetData
    {
        public string Name;
        public float Weight;
        public string[] BlendShapeNames;
        public float[] BlendShapeMultipliers;
        public string[] VisemeBlendShapeNames;
        public float[] VisemeBlendShapeMultipliers;

        /// <summary>
        /// 全キーが空の、長さをそろえたSet
        /// </summary>
        public static FaceTrackingSetData CreateEmpty(string name, float weight)
        {
            return new FaceTrackingSetData
            {
                Name = name,
                Weight = weight,
                BlendShapeNames = Filled(FaceTrackingGroupTable.KeyCount, string.Empty),
                BlendShapeMultipliers = Filled(FaceTrackingGroupTable.KeyCount, 1f),
                VisemeBlendShapeNames = Filled(FaceTrackingGroupTable.VisemeCount, string.Empty),
                VisemeBlendShapeMultipliers = Filled(FaceTrackingGroupTable.VisemeCount, 1f),
            };
        }

        /// <summary>
        /// キーの番号に割り当てられたシェイプ名。配列が短い、または未割り当てならnull
        /// </summary>
        public string GetBlendShapeName(int keyIndex)
        {
            return GetName(BlendShapeNames, keyIndex);
        }

        public string GetVisemeBlendShapeName(int visemeIndex)
        {
            return GetName(VisemeBlendShapeNames, visemeIndex);
        }

        /// <summary>
        /// 割り当てのあるキーの数（ビゼームは含めない）
        /// </summary>
        public int AssignedKeyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < FaceTrackingGroupTable.KeyCount; i++)
                {
                    if (GetBlendShapeName(i) != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        internal static float GetMultiplier(float[] multipliers, int index)
        {
            // SDKの新しいSetは倍率の配列を持たないことがある（new FaceTrackingBlendshapesSet() は null）。
            // 倍率が無いときは等倍として扱う
            if (multipliers == null || index < 0 || index >= multipliers.Length)
            {
                return 1f;
            }

            return multipliers[index];
        }

        private static string GetName(string[] names, int index)
        {
            if (names == null || index < 0 || index >= names.Length)
            {
                return null;
            }

            return string.IsNullOrEmpty(names[index]) ? null : names[index];
        }

        private static T[] Filled<T>(int length, T value)
        {
            var array = new T[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = value;
            }

            return array;
        }
    }
}
