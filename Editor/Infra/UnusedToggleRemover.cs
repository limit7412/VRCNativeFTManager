using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

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
        /// アバターのどのコントローラの遷移条件にも出てこない候補を、メニューとExpression Parametersから外す。
        /// 外した名前を返す。FXだけでなく全レイヤーを見るのは、公開パラメーターを利用者のアニメーターが
        /// 名前で読むことがあるため（仕様書7.1）
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

            var used = CollectConditionParameters(controllers);
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

        private static HashSet<string> CollectConditionParameters(IEnumerable<AnimatorController> controllers)
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
            foreach (var child in stateMachine.states)
            {
                if (child.state != null)
                {
                    AddConditions(child.state.transitions, used);
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
