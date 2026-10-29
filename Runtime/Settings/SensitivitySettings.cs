using System;
using UnityEngine;

namespace NativeFTManager
{
    /// <summary>
    /// 感度の設定（仕様書5.6、M4）
    ///
    /// EXメニューのRadialで、目と口のSetの効き方を8段階に変える。
    /// 既定ではどちらも作らない。同期bitが1系統につき8bit増えるため、使う人だけが有効にする
    /// </summary>
    [Serializable]
    public class SensitivitySettings
    {
        /// <summary>倍率の上限の範囲（インスペクタ）</summary>
        public const float MinMaxMultiplier = 1f;

        public const float MaxMaxMultiplier = 4f;

        [Tooltip("Add a Radial that changes the strength of the eye and brow Blendshapes Sets")]
        public bool eye;

        [Tooltip("Add a Radial that changes the strength of the mouth Blendshapes Sets")]
        public bool lip;

        [Tooltip("Strength at the top end of the Radial. The Blendshapes Set multipliers are raised to this value at build time")]
        [Range(MinMaxMultiplier, MaxMaxMultiplier)]
        public float maxMultiplier = 2f;

        [Tooltip("Keep the Radial value across worlds")]
        public bool saved = true;
    }
}
