using UnityEditor;
using UnityEngine;
using NativeFTManager.Infra;

namespace NativeFTManager.Handler
{
    /// <summary>
    /// SDKのFace Tracking BlendshapesのState Behaviorの型とフィールドをコンソールへ出す
    ///
    /// 仕様書14.1の確認項目6（型とフィールドのダンプ）を、SDKの入ったプロジェクトで1回実行するためのもの。
    /// 結果で FaceTrackingStateBehaviorWriter の探し方が合っているかを確かめる
    /// </summary>
    internal static class StateBehaviorDumpMenu
    {
        [MenuItem("Tools/Kx VRC NativeFT Manager/Dump Face Tracking State Behavior")]
        private static void Dump()
        {
            Debug.Log("[NativeFTManager] Face Tracking State Behavior\n" + FaceTrackingStateBehaviorWriter.Describe());
        }
    }
}
