using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// ステートへVRC Animator Face Tracking Blendshapes Set（State Behavior）を付ける
    ///
    /// このState BehaviorはSDKのランタイムDLLにあり、型名とフィールド名をまだ確かめていない
    /// （仕様書14.1の確認項目6）。そのため型を直接参照せず、SDKのStateMachineBehaviourの中から
    /// 名前で探してリフレクションで値を書く。見つからなければ書かずに理由を返すので、
    /// ビルドはSetの分割までは進み、トグルだけが効かない状態になる。
    /// 確認項目6で型が確定したら、ここを型の直接参照へ置き換える
    /// </summary>
    internal static class FaceTrackingStateBehaviorWriter
    {
        private sealed class Binding
        {
            public Type Type;
            public FieldInfo SetName;
            public FieldInfo Weight;
            public FieldInfo LerpSpeed;
            public FieldInfo Mode;
            public object FixedValueMode;
        }

        private static Binding _binding;
        private static string _failure;
        private static bool _resolved;

        /// <summary>
        /// 型とフィールドが見つかったか。見つからないときは、その理由（見つかった候補とフィールドの一覧）を返す
        /// </summary>
        public static bool TryResolve(out string failure)
        {
            if (!_resolved)
            {
                _binding = Resolve(out _failure);
                _resolved = true;
            }

            failure = _failure;
            return _binding != null;
        }

        /// <summary>
        /// ステートに入ったときSetの重みをweightへ書くBehaviorを付ける（Type = Value）
        /// </summary>
        public static StateMachineBehaviour Add(AnimatorState state, string setName, float weight, float lerpSpeed)
        {
            if (!TryResolve(out _))
            {
                return null;
            }

            var behaviour = state.AddStateMachineBehaviour(_binding.Type);
            _binding.SetName.SetValue(behaviour, setName);
            _binding.Weight.SetValue(behaviour, weight);
            _binding.LerpSpeed?.SetValue(behaviour, lerpSpeed);
            if (_binding.Mode != null)
            {
                _binding.Mode.SetValue(behaviour, _binding.FixedValueMode);
            }

            return behaviour;
        }

        /// <summary>
        /// コントローラにすでにあるこのBehaviorが、名前で参照しているSetを集める。
        /// 型が見つからなければ何も返さない（このBehavior自体を作れないので、既存のものも見分けられない）
        /// </summary>
        public static HashSet<string> CollectReferencedSetNames(IEnumerable<AnimatorController> controllers)
        {
            var names = new HashSet<string>();
            if (!TryResolve(out _))
            {
                return names;
            }

            foreach (var controller in controllers.Where(controller => controller != null).Distinct())
            {
                foreach (var layer in controller.layers)
                {
                    Collect(layer.stateMachine, names, new HashSet<AnimatorStateMachine>());
                }
            }

            return names;
        }

        private static void Collect(
            AnimatorStateMachine stateMachine,
            HashSet<string> names,
            HashSet<AnimatorStateMachine> visited)
        {
            if (stateMachine == null || !visited.Add(stateMachine))
            {
                return;
            }

            AddNames(stateMachine.behaviours, names);
            foreach (var child in stateMachine.states)
            {
                AddNames(child.state != null ? child.state.behaviours : null, names);
            }

            foreach (var child in stateMachine.stateMachines)
            {
                Collect(child.stateMachine, names, visited);
            }
        }

        private static void AddNames(StateMachineBehaviour[] behaviours, HashSet<string> names)
        {
            if (behaviours == null)
            {
                return;
            }

            foreach (var behaviour in behaviours)
            {
                if (behaviour != null && _binding.Type.IsInstanceOfType(behaviour)
                    && _binding.SetName.GetValue(behaviour) is string name && !string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }
        }

        /// <summary>
        /// 候補の型とフィールドを文字列にする（確認項目6のダンプ用）
        /// </summary>
        public static string Describe()
        {
            var builder = new StringBuilder();
            var candidates = FindCandidates().ToArray();
            if (candidates.Length == 0)
            {
                builder.AppendLine("No StateMachineBehaviour with \"FaceTracking\" in its name was found.");
            }

            foreach (var type in candidates)
            {
                builder.AppendLine($"type: {type.FullName} ({type.Assembly.GetName().Name})"
                                   + (type.IsAbstract ? " abstract" : "")
                                   + $" base={type.BaseType?.FullName}");
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    builder.AppendLine($"  field: {field.FieldType.FullName} {field.Name}");
                    if (field.FieldType.IsEnum)
                    {
                        builder.AppendLine("    values: " + string.Join(", ", Enum.GetNames(field.FieldType)));
                    }
                }
            }

            TryResolve(out var failure);
            builder.AppendLine(_binding != null
                ? $"resolved: setName={_binding.SetName.Name}, weight={_binding.Weight.Name}, "
                  + $"lerpSpeed={_binding.LerpSpeed?.Name ?? "-"}, mode={_binding.Mode?.Name ?? "-"}"
                : "not resolved: " + failure);

            return builder.ToString();
        }

        private static IEnumerable<Type> FindCandidates()
        {
            return TypeCache.GetTypesDerivedFrom<StateMachineBehaviour>()
                .Where(type => type.Name.IndexOf("FaceTracking", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(type => type.FullName, StringComparer.Ordinal);
        }

        private static Binding Resolve(out string failure)
        {
            // 抽象の基底（SDKBaseのVRC_〜）と、SDK3Aの具象型（VRC〜）に分かれていることがあるので、具象型だけを選ぶ。
            // Blendshape / BlendShape / Set を名前に含むものを優先する
            var candidates = FindCandidates().Where(type => !type.IsAbstract).ToArray();
            var behaviourType = candidates
                            .FirstOrDefault(type => type.Name.IndexOf("Blendshape", StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? candidates.FirstOrDefault(type => type.Name.EndsWith("Set", StringComparison.Ordinal));

            if (behaviourType == null)
            {
                failure = "type not found. " + DescribeTypes(candidates);
                return null;
            }

            var fields = behaviourType.GetFields(BindingFlags.Public | BindingFlags.Instance);

            var setName = PickField(fields, typeof(string),
                new[] { "setName", "blendshapesSetName", "blendShapeSetName", "blendshapeSetName", "targetSetName" },
                name => Contains(name, "set") && !Contains(name, "param"));
            var weight = PickField(fields, typeof(float),
                new[] { "weight", "setWeight", "value", "targetWeight", "blendWeight" },
                name => Contains(name, "weight") && !Contains(name, "lerp"));
            var lerp = PickField(fields, typeof(float),
                new[] { "lerpSpeed" },
                name => Contains(name, "lerp"));

            if (setName == null || weight == null)
            {
                failure = $"fields not found in {behaviourType.FullName}: "
                          + string.Join(", ", fields.Select(field => $"{field.FieldType.Name} {field.Name}"));
                return null;
            }

            // 固定値と「突入時のパラメーター値」を切り替える列挙体があれば、固定値の側へ合わせる
            FieldInfo mode = null;
            object fixedValue = null;
            foreach (var field in fields.Where(field => field.FieldType.IsEnum))
            {
                var value = new[] { "Value", "Constant", "Fixed", "Set" }
                    .FirstOrDefault(name => Enum.GetNames(field.FieldType).Contains(name));
                if (value != null)
                {
                    mode = field;
                    fixedValue = Enum.Parse(field.FieldType, value);
                    break;
                }
            }

            failure = null;
            return new Binding
            {
                Type = behaviourType,
                SetName = setName,
                Weight = weight,
                LerpSpeed = lerp,
                Mode = mode,
                FixedValueMode = fixedValue,
            };
        }

        private static FieldInfo PickField(
            FieldInfo[] fields,
            Type fieldType,
            string[] exactNames,
            Func<string, bool> fallback)
        {
            var typed = fields.Where(field => field.FieldType == fieldType).ToArray();
            foreach (var exact in exactNames)
            {
                var match = typed.FirstOrDefault(field =>
                    string.Equals(field.Name, exact, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            return typed.FirstOrDefault(field => fallback(field.Name));
        }

        private static bool Contains(string name, string part)
        {
            return name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeTypes(IEnumerable<Type> types)
        {
            var names = types.Select(type => type.FullName).ToArray();
            return names.Length == 0 ? "candidates: none" : "candidates: " + string.Join(", ", names);
        }
    }
}
