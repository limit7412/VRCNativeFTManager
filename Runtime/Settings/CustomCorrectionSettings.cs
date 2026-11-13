using System;
using UnityEngine;

namespace NativeFTManager
{
    /// <summary>
    /// カスタム補正が式の結果をシェイプへ反映する方法
    ///
    /// 値はシリアライズされるので、並びを変えずに末尾へ足す
    /// </summary>
    public enum CustomCorrectionMode
    {
        /// <summary>式の結果をシェイプへそのまま書く</summary>
        Apply = 0,

        /// <summary>シェイプの最終値の上限を式の結果にする</summary>
        LimitMax = 1,

        /// <summary>シェイプの最終値の下限を式の結果にする</summary>
        LimitMin = 2,
    }

    /// <summary>
    /// カスタム補正1件。トラッキングキーに割り当てられないシェイプ（組み合わせ補正用や段階用のシェイプ）を、
    /// Custom式の結果で動かす
    /// </summary>
    [Serializable]
    public class CustomCorrectionRule
    {
        [Tooltip("Generate this rule")]
        public bool enabled = true;

        [Tooltip("Name of the blend shape on a Face Tracking Mesh that this rule drives")]
        public string blendShape = string.Empty;

        [Tooltip("How the result of the expression is applied to the blend shape")]
        public CustomCorrectionMode mode = CustomCorrectionMode.Apply;

        [Tooltip("Custom expression of a Parameter Behavior. Inputs are Unified Expressions key names")]
        public string expression = string.Empty;
    }
}
