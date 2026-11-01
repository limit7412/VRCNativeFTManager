using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// Kx VRC ARKit BlendShape Generatorとの連携（仕様書10.1、F-12）
    ///
    /// Generatorは必須の依存だが、型は直接参照せずリフレクションで引く。
    /// booth版はVPMの依存解決を通らないので、同梱したGeneratorのunitypackageを取り込み忘れることがある。
    /// asmdefでGeneratorを参照すると、そのときEditorアセンブリ全体がコンパイルできず、警告すら出せない。
    /// Generatorが無い（または必要なフィールドを持たない古い版の）ときは<see cref="IsInstalled"/>がfalseになり、
    /// アバターにGeneratorが無いのと同じに振る舞う。
    ///
    /// Generatorの既定（Set書き込みはオフ）は変えず、Managerがビルド用の複製の上でだけ有効にする。
    /// Jerry'sを使い続ける利用者を壊さないため
    /// </summary>
    internal static class GeneratorAccess
    {
        /// <summary>Generatorのコンポーネントの型名</summary>
        internal const string ComponentTypeName = "ARKitBlendShapeGenerator.ARKitBlendShapeGeneratorComponent";

        /// <summary>GeneratorのSet名の定数を持つ型名</summary>
        internal const string SetNamesTypeName = "ARKitBlendShapeGenerator.FaceTrackingSetNames";

        /// <summary>Set書き込みのフィールド名</summary>
        internal const string SetWriteFieldName = "writeFaceTrackingBlendshapesSet";

        /// <summary>
        /// GeneratorがDescriptorへ書くSetの名前の既定値。
        /// Generatorが無くても見積もりの判定に使うため、Generatorの<c>FaceTrackingSetNames.Generator</c>を複製して持つ。
        /// 一致はテストで確かめる
        /// </summary>
        internal const string DefaultSetName = "Kx VRC ARKit BlendShape Generator";

        private static bool _resolved;
        private static Type _componentType;
        private static FieldInfo _setWriteField;
        private static string _setName = DefaultSetName;

        /// <summary>
        /// Generatorがプロジェクトに導入され、連携に使えるか。
        /// コンポーネントの型と、Set書き込みのフィールドの両方が見つかったときだけtrue
        /// </summary>
        public static bool IsInstalled
        {
            get
            {
                Resolve();
                return _componentType != null && _setWriteField != null;
            }
        }

        /// <summary>GeneratorがDescriptorへ書くSetの名前</summary>
        public static string SetName
        {
            get
            {
                Resolve();
                return _setName;
            }
        }

        /// <summary>アバターにGeneratorのコンポーネントがあるか。Generatorが導入されていなければ常にfalse</summary>
        public static bool IsPresent(GameObject avatarRoot)
        {
            return Find(avatarRoot) != null;
        }

        /// <summary>アバターのGeneratorでSet書き込みが有効か</summary>
        public static bool IsSetWriteEnabled(GameObject avatarRoot)
        {
            var generator = Find(avatarRoot);
            return generator != null && (bool)_setWriteField.GetValue(generator);
        }

        /// <summary>アバターのGeneratorのSet書き込みを有効にする。ビルド用の複製にだけ使う</summary>
        public static void EnableSetWrite(GameObject avatarRoot)
        {
            var generator = Find(avatarRoot);
            if (generator != null)
            {
                _setWriteField.SetValue(generator, true);
            }
        }

        /// <summary>
        /// アバター内のGenerator。Generator自身の重複排除で1つに絞られている前提で、最初の1つを使う
        /// </summary>
        private static Component Find(GameObject avatarRoot)
        {
            if (avatarRoot == null || !IsInstalled)
            {
                return null;
            }

            return avatarRoot.GetComponentsInChildren(_componentType, true)
                .FirstOrDefault(component => component != null);
        }

        /// <summary>
        /// 型とフィールドを1度だけ引く。アセンブリの増減はドメインのリロードを伴うので、
        /// 静的フィールドに持った結果はリロードで捨てられ、取り込み後に引き直される
        /// </summary>
        private static void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _componentType = FindType(ComponentTypeName);
            if (_componentType == null || !typeof(Component).IsAssignableFrom(_componentType))
            {
                _componentType = null;
                return;
            }

            var field = _componentType.GetField(SetWriteFieldName, BindingFlags.Public | BindingFlags.Instance);
            _setWriteField = field != null && field.FieldType == typeof(bool) ? field : null;

            var setNames = FindType(SetNamesTypeName);
            var constant = setNames?.GetField("Generator", BindingFlags.Public | BindingFlags.Static);
            if (constant != null && constant.FieldType == typeof(string)
                && constant.GetValue(null) is string name && !string.IsNullOrEmpty(name))
            {
                _setName = name;
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
