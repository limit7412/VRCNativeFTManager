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
    /// このState BehaviorはSDKのランタイムDLLにあり、ベータの間に形が変わりうる。
    /// そのため型を直接参照せず、SDKのStateMachineBehaviourの中から名前で探してリフレクションで値を書く。
    /// 見つからなければ書かずに理由を返すので、ビルドはSetの分割までは進み、トグルだけが効かない状態になる。
    ///
    /// SDK 3.10.6-beta.2のBehaviorは、Set名と重みを直下に持たず、要素の一覧（blendSets）に持つ。
    /// 要素1つが「Set 1つの重みを、固定値かパラメーターの値で書く」指定で、1つのBehaviorで複数のSetを書ける。
    /// 2026-10-30の実機確認（fttest）でこの形を確かめた。直下に持つ形も、念のため残して両方を探す
    /// </summary>
    internal static class FaceTrackingStateBehaviorWriter
    {
        /// <summary>Set 1つ分の指定を書くフィールドの組。Behavior直下か、一覧の要素の型に属する</summary>
        private sealed class EntryFields
        {
            public Type Type;
            public FieldInfo SetName;
            public FieldInfo Weight;
            public FieldInfo LerpSpeed;
            public FieldInfo Mode;
            public object FixedValueMode;
        }

        private sealed class Binding
        {
            public Type Type;

            /// <summary>指定の一覧（blendSets）。nullならBehavior直下にSet 1つ分を持つ形</summary>
            public FieldInfo List;

            public EntryFields Entry;
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
        /// ステートに入ったとき、各Setの重みを固定値で書くBehaviorを付ける（Type = Value）。
        /// 一覧を持つ形なら1つのBehaviorにまとめ、直下に持つ形ならSetごとに1つ付ける
        /// </summary>
        public static IReadOnlyList<StateMachineBehaviour> Add(
            AnimatorState state,
            IReadOnlyList<(string SetName, float Weight)> sets,
            float lerpSpeed)
        {
            var added = new List<StateMachineBehaviour>();
            if (!TryResolve(out _) || sets.Count == 0)
            {
                return added;
            }

            if (_binding.List == null)
            {
                foreach (var (setName, weight) in sets)
                {
                    var single = state.AddStateMachineBehaviour(_binding.Type);
                    WriteEntry(single, setName, weight, lerpSpeed);
                    added.Add(single);
                }

                return added;
            }

            var behaviour = state.AddStateMachineBehaviour(_binding.Type);
            var list = _binding.List.GetValue(behaviour) as System.Collections.IList
                       ?? (System.Collections.IList)Activator.CreateInstance(_binding.List.FieldType);
            foreach (var (setName, weight) in sets)
            {
                var entry = Activator.CreateInstance(_binding.Entry.Type);
                WriteEntry(entry, setName, weight, lerpSpeed);
                list.Add(entry);
            }

            _binding.List.SetValue(behaviour, list);
            added.Add(behaviour);
            return added;
        }

        private static void WriteEntry(object target, string setName, float weight, float lerpSpeed)
        {
            var fields = _binding.Entry;
            fields.SetName.SetValue(target, setName);
            fields.Weight.SetValue(target, weight);
            fields.LerpSpeed?.SetValue(target, lerpSpeed);
            if (fields.Mode != null)
            {
                fields.Mode.SetValue(target, fields.FixedValueMode);
            }
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
                if (behaviour == null || !_binding.Type.IsInstanceOfType(behaviour))
                {
                    continue;
                }

                var entries = _binding.List == null
                    ? new object[] { behaviour }
                    : (_binding.List.GetValue(behaviour) as System.Collections.IEnumerable)?.Cast<object>()
                      ?? Enumerable.Empty<object>();
                foreach (var entry in entries)
                {
                    if (entry != null && _binding.Entry.SetName.GetValue(entry) is string name && !string.IsNullOrEmpty(name))
                    {
                        names.Add(name);
                    }
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
                DescribeFields(builder, type, "  ");
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var element = GetElementType(field.FieldType);
                    if (element != null && !element.IsPrimitive && element != typeof(string))
                    {
                        builder.AppendLine($"  element of {field.Name}: {element.FullName}");
                        DescribeFields(builder, element, "    ");
                    }
                }
            }

            TryResolve(out var failure);
            builder.AppendLine(_binding != null
                ? $"resolved: list={_binding.List?.Name ?? "-"}, setName={_binding.Entry.SetName.Name}, "
                  + $"weight={_binding.Entry.Weight.Name}, lerpSpeed={_binding.Entry.LerpSpeed?.Name ?? "-"}, "
                  + $"mode={_binding.Entry.Mode?.Name ?? "-"}"
                : "not resolved: " + failure);

            return builder.ToString();
        }

        private static void DescribeFields(StringBuilder builder, Type type, string indent)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                builder.AppendLine($"{indent}field: {field.FieldType.FullName} {field.Name}");
                if (field.FieldType.IsEnum)
                {
                    builder.AppendLine($"{indent}  values: " + string.Join(", ", Enum.GetNames(field.FieldType)));
                }
            }
        }

        /// <summary>List&lt;T&gt;か配列の要素の型。どちらでもなければnull</summary>
        private static Type GetElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
                ? type.GetGenericArguments()[0]
                : null;
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

            // 一覧の形を先に探す。直下にもSet名らしいフィールドがある形へ変わっても、一覧があればそちらが本体である。
            // 一覧はList<T>だけを相手にする。配列は要素を足すたびに作り直しが要り、SDKもこの形を使っていない
            foreach (var field in fields)
            {
                var element = field.FieldType.IsGenericType
                              && field.FieldType.GetGenericTypeDefinition() == typeof(List<>)
                    ? field.FieldType.GetGenericArguments()[0]
                    : null;
                if (element == null || element.IsPrimitive || element == typeof(string) || element.IsAbstract
                    || element.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                var entry = ResolveEntry(element);
                if (entry != null)
                {
                    failure = null;
                    return new Binding { Type = behaviourType, List = field, Entry = entry };
                }
            }

            var direct = ResolveEntry(behaviourType);
            if (direct == null)
            {
                failure = $"fields not found in {behaviourType.FullName}: "
                          + string.Join(", ", fields.Select(field => $"{field.FieldType.Name} {field.Name}"));
                return null;
            }

            failure = null;
            return new Binding { Type = behaviourType, Entry = direct };
        }

        /// <summary>
        /// Set 1つ分の指定（Set名、重み、追従の速さ、固定値か否か）を書くフィールドを型から探す。
        /// Set名と重みが無ければnull
        /// </summary>
        private static EntryFields ResolveEntry(Type type)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            var setName = PickField(fields, typeof(string),
                new[] { "blendshapesSetName", "setName", "blendShapeSetName", "blendshapeSetName", "targetSetName" },
                name => Contains(name, "set") && !Contains(name, "param"));
            var weight = PickField(fields, typeof(float),
                new[] { "weight", "setWeight", "value", "targetWeight", "blendWeight" },
                name => Contains(name, "weight") && !Contains(name, "lerp"));
            var lerp = PickField(fields, typeof(float),
                new[] { "lerpSpeed" },
                name => Contains(name, "lerp"));

            if (setName == null || weight == null)
            {
                return null;
            }

            // 固定値と「パラメーターの値」を切り替える列挙体があれば、固定値の側へ合わせる
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

            return new EntryFields
            {
                Type = type,
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
