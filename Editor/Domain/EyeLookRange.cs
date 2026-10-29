using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// DescriptorのEye Lookの回転幅の判定（仕様書5.7、W-07）
    ///
    /// 回転幅が狭いと、視線が動いていても見た目にはほとんど動かない。
    /// 2026-10-20の目線不具合（回転幅が約±3°）で確認している
    /// </summary>
    internal static class EyeLookRange
    {
        /// <summary>これ未満の方向があれば警告する（度）</summary>
        public const float MinimumDegrees = 5f;

        /// <summary>
        /// 上下左右の回転角（度）のうち、1つでも下限を下回るか
        /// </summary>
        public static bool IsTooNarrow(IEnumerable<float> directionDegrees)
        {
            var angles = directionDegrees?.ToArray();
            if (angles == null || angles.Length == 0)
            {
                return false;
            }

            return angles.Any(angle => angle < MinimumDegrees);
        }
    }
}
