using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using NativeFTManager.Domain;
using Object = UnityEngine.Object;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// マージ後のFXで、トグルのレイヤーのOn / OffステートにSetの重みを書くBehaviorを付ける
    /// </summary>
    internal static class ToggleBehaviorAttacher
    {
        internal enum Outcome
        {
            /// <summary>Behaviorを付けた</summary>
            Attached,

            /// <summary>グループにSetが無いので、レイヤーを取り除いた</summary>
            LayerRemoved,

            /// <summary>FXにレイヤーが見つからなかった</summary>
            LayerMissing,

            /// <summary>State Behaviorの型が見つからず、付けられなかった</summary>
            BehaviorUnavailable,
        }

        /// <summary>
        /// レイヤー1つ分を処理する。
        /// setNamesが空なら、そのレイヤーは何も切り替えないので取り除く
        /// </summary>
        public static Outcome Attach(
            AnimatorController fx,
            ToggleLayerPlan layerPlan,
            IReadOnlyList<string> setNames,
            float lerpSpeed,
            System.Action<Object> saveAsset)
        {
            var layers = fx.layers;
            int index = System.Array.FindIndex(layers, layer => layer.name == layerPlan.Name);
            if (index < 0)
            {
                return Outcome.LayerMissing;
            }

            if (setNames.Count == 0)
            {
                fx.RemoveLayer(index);
                return Outcome.LayerRemoved;
            }

            if (!FaceTrackingStateBehaviorWriter.TryResolve(out _))
            {
                return Outcome.BehaviorUnavailable;
            }

            var stateMachine = layers[index].stateMachine;
            var off = FindState(stateMachine, ToggleLayerPlan.OffState);
            var on = layerPlan.OnStates
                .Select(planned => (planned.Weight, State: FindState(stateMachine, planned.Name)))
                .ToArray();
            if (off == null || on.Any(entry => entry.State == null))
            {
                return Outcome.LayerMissing;
            }

            // 感度の段階では重みが1より小さい。Setの倍率はApplyで上限の倍率まで上げてある（仕様書5.6）
            foreach (var (weight, state) in on)
            {
                Save(FaceTrackingStateBehaviorWriter.Add(
                    state, setNames.Select(setName => (setName, weight)).ToArray(), lerpSpeed), saveAsset);
            }

            Save(FaceTrackingStateBehaviorWriter.Add(
                off, setNames.Select(setName => (setName, 0f)).ToArray(), lerpSpeed), saveAsset);

            return Outcome.Attached;
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            return stateMachine.states.Select(child => child.state).FirstOrDefault(state => state.name == name);
        }

        private static void Save(IEnumerable<StateMachineBehaviour> behaviours, System.Action<Object> saveAsset)
        {
            // 永続化済みのコントローラへ付けたBehaviorは、Unityが同じアセットへ自動で入れる。
            // そうでなければ、ビルドの一時アセットへ登録する
            foreach (var behaviour in behaviours)
            {
                if (behaviour != null && !EditorUtility.IsPersistent(behaviour))
                {
                    saveAsset?.Invoke(behaviour);
                }
            }
        }
    }
}
