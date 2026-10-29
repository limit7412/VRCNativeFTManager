using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using NativeFTManager.Domain;
using Object = UnityEngine.Object;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// トグルの計画をAnimatorControllerへ実体化する
    ///
    /// ステートはモーションもBehaviorも持たない状態で作る。BehaviorはSetを分割した後に
    /// ToggleBehaviorAttacherが付ける（Setの名前がこの時点では決まらないため）
    /// </summary>
    internal static class ToggleControllerWriter
    {
        public const string ControllerName = NativeFTManagerParameterNames.ShortPrefix + " Generated";

        /// <param name="plan">生成計画</param>
        /// <param name="saveAsset">
        /// 生成したアセットをビルドの一時アセットへ登録するコールバック。
        /// コントローラを先に登録すると、以降に生成されるステートマシンやステートは
        /// Unity側が同じアセットへ自動で追加する
        /// </param>
        public static AnimatorController Write(ToggleControllerPlan plan, Action<Object> saveAsset = null)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var controller = new AnimatorController { name = ControllerName };
            saveAsset?.Invoke(controller);

            foreach (var parameter in plan.Parameters)
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = parameter.Name,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = parameter.DefaultValue,
                });
            }

            foreach (var layerPlan in plan.Layers)
            {
                controller.AddLayer(layerPlan.Name);
                var layers = controller.layers;
                WriteLayer(layerPlan, layers[layers.Length - 1].stateMachine);
            }

            // AddLayerで作られるレイヤーの重みは0のため、明示的に1へ揃える。
            // layersはコピーを返すプロパティなので、書き戻すまで反映されない
            var allLayers = controller.layers;
            foreach (var layer in allLayers)
            {
                layer.defaultWeight = 1f;
            }

            controller.layers = allLayers;

            return controller;
        }

        private static void WriteLayer(ToggleLayerPlan layerPlan, AnimatorStateMachine stateMachine)
        {
            var states = new Dictionary<string, AnimatorState>();
            var positions = new Dictionary<string, Vector3>
            {
                { ToggleLayerPlan.InitState, new Vector3(250f, 0f) },
                { ToggleLayerPlan.OnState, new Vector3(500f, -80f) },
                { ToggleLayerPlan.OffState, new Vector3(500f, 80f) },
            };

            foreach (var name in new[] { ToggleLayerPlan.InitState, ToggleLayerPlan.OnState, ToggleLayerPlan.OffState })
            {
                var state = stateMachine.AddState(name, positions[name]);

                // アニメーションを持たないので、Write Defaultsは結果に影響しない。
                // 仕様書5.3に合わせてOFFで作る
                state.writeDefaultValues = false;
                state.motion = null;
                states[name] = state;
            }

            stateMachine.defaultState = states[ToggleLayerPlan.InitState];

            foreach (var transitionPlan in layerPlan.Transitions)
            {
                var transition = states[transitionPlan.From].AddTransition(states[transitionPlan.To]);
                transition.hasExitTime = false;
                transition.exitTime = 0f;
                transition.hasFixedDuration = true;
                transition.duration = 0f;
                transition.offset = 0f;
                transition.interruptionSource = TransitionInterruptionSource.None;
                transition.canTransitionToSelf = false;

                foreach (var condition in transitionPlan.Conditions)
                {
                    transition.AddCondition(
                        condition.Expected ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                        0f,
                        condition.Parameter);
                }
            }
        }
    }
}
