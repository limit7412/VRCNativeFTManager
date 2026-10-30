using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// どのレイヤーからも読まれなくなったトグルを、EXメニューと同期パラメーターから取り除く
    ///
    /// メニューとパラメーターはSetの中身が分かる前（Generating）に作るので、
    /// 切り替える対象のSetが無くてレイヤーを取り除いたトグル（ARKitのシェイプに瞳孔が無いときのPupilなど）も
    /// メニューに残る。押しても何も変わらない項目を残さないよう、MAのマージ後にここで外す
    /// </summary>
    internal static class UnusedToggleRemover
    {
        /// <summary>
        /// アバターのどのコントローラからも読まれない候補を、メニューとExpression Parametersから外す。
        /// 外した名前を返す。FXだけでなく全レイヤーを見るのは、公開パラメーターを利用者のアニメーターが
        /// 名前で読むことがあるため（仕様書7.1）。読み方は遷移条件のほか、ブレンドツリー、
        /// ステートの速度などのパラメーター、Parameter Driverの読み取り元（Copy、Add）も数える
        /// </summary>
        public static IReadOnlyList<string> Remove(
            VRCAvatarDescriptor descriptor,
            IEnumerable<AnimatorController> controllers,
            IEnumerable<string> candidates)
        {
            if (descriptor == null)
            {
                return new string[0];
            }

            var used = CollectReadParameters(controllers);
            var unused = new HashSet<string>(candidates.Where(name => !string.IsNullOrEmpty(name) && !used.Contains(name)));
            if (unused.Count == 0)
            {
                return new string[0];
            }

            // ここに来るメニューとパラメーターは、MAがビルド用に作り直したものなので、書き換えても素体は変わらない
            RemoveControls(descriptor.expressionsMenu, unused, new HashSet<VRCExpressionsMenu>());

            var parameters = descriptor.expressionParameters;
            if (parameters != null && parameters.parameters != null)
            {
                parameters.parameters = parameters.parameters
                    .Where(parameter => parameter == null || !unused.Contains(parameter.name))
                    .ToArray();
            }

            return unused.OrderBy(name => name, System.StringComparer.Ordinal).ToArray();
        }

        private static HashSet<string> CollectReadParameters(IEnumerable<AnimatorController> controllers)
        {
            var used = new HashSet<string>();
            var visited = new HashSet<AnimatorStateMachine>();
            foreach (var layer in controllers.Where(controller => controller != null).SelectMany(controller => controller.layers))
            {
                Collect(layer.stateMachine, used, visited);
            }

            return used;
        }

        private static void Collect(AnimatorStateMachine stateMachine, HashSet<string> used, HashSet<AnimatorStateMachine> visited)
        {
            if (stateMachine == null || !visited.Add(stateMachine))
            {
                return;
            }

            AddConditions(stateMachine.anyStateTransitions, used);
            AddConditions(stateMachine.entryTransitions, used);
            AddDriverSources(stateMachine.behaviours, used);
            foreach (var child in stateMachine.states)
            {
                var state = child.state;
                if (state == null)
                {
                    continue;
                }

                AddConditions(state.transitions, used);
                AddDriverSources(state.behaviours, used);
                AddMotion(state.motion, used, new HashSet<BlendTree>());
                if (state.speedParameterActive)
                {
                    used.Add(state.speedParameter);
                }

                if (state.timeParameterActive)
                {
                    used.Add(state.timeParameter);
                }

                if (state.mirrorParameterActive)
                {
                    used.Add(state.mirrorParameter);
                }

                if (state.cycleOffsetParameterActive)
                {
                    used.Add(state.cycleOffsetParameter);
                }
            }

            foreach (var child in stateMachine.stateMachines)
            {
                AddConditions(stateMachine.GetStateMachineTransitions(child.stateMachine), used);
                Collect(child.stateMachine, used, visited);
            }
        }

        private static void AddConditions(IEnumerable<AnimatorTransitionBase> transitions, HashSet<string> used)
        {
            foreach (var transition in transitions ?? Enumerable.Empty<AnimatorTransitionBase>())
            {
                if (transition == null)
                {
                    continue;
                }

                foreach (var condition in transition.conditions)
                {
                    used.Add(condition.parameter);
                }
            }
        }

        private static void AddMotion(Motion motion, HashSet<string> used, HashSet<BlendTree> visited)
        {
            if (!(motion is BlendTree tree) || !visited.Add(tree))
            {
                return;
            }

            used.Add(tree.blendParameter);
            used.Add(tree.blendParameterY);
            foreach (var child in tree.children)
            {
                used.Add(child.directBlendParameter);
                AddMotion(child.motion, used, visited);
            }
        }

        /// <summary>
        /// Parameter Driverが値を読むパラメーター。Copyの元と、Addで足す相手（自身の値を読む）を数える
        /// </summary>
        private static void AddDriverSources(StateMachineBehaviour[] behaviours, HashSet<string> used)
        {
            foreach (var driver in (behaviours ?? new StateMachineBehaviour[0]).OfType<VRC_AvatarParameterDriver>())
            {
                foreach (var parameter in driver.parameters ?? new List<VRC_AvatarParameterDriver.Parameter>())
                {
                    if (parameter == null)
                    {
                        continue;
                    }

                    if (parameter.type == VRC_AvatarParameterDriver.ChangeType.Copy)
                    {
                        used.Add(parameter.source);
                    }
                    else if (parameter.type == VRC_AvatarParameterDriver.ChangeType.Add)
                    {
                        used.Add(parameter.name);
                    }
                }
            }
        }

        private static void RemoveControls(
            VRCExpressionsMenu menu,
            HashSet<string> unused,
            HashSet<VRCExpressionsMenu> visited)
        {
            if (menu == null || menu.controls == null || !visited.Add(menu))
            {
                return;
            }

            menu.controls.RemoveAll(control =>
                control != null
                && control.type == VRCExpressionsMenu.Control.ControlType.Toggle
                && control.parameter != null
                && unused.Contains(control.parameter.name));

            foreach (var control in menu.controls)
            {
                if (control != null && control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    RemoveControls(control.subMenu, unused, visited);
                }
            }
        }
    }
}
