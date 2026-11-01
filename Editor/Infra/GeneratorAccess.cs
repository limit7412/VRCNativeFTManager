using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NativeFTManager.Domain;

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
        /// キーとシェイプの対応表を公開する型名。Generator 0.2.2以降にある。
        /// アバターにGeneratorのコンポーネントが無いときに、ARKit名のシェイプからSetを作るのに使う（仕様書5.1）
        /// </summary>
        internal const string SetMappingTypeName = "ARKitBlendShapeGenerator.FaceTrackingSetMapping";

        /// <summary>対応表の型の、シェイプ名からキーごとの割り当てを返すメソッド名</summary>
        internal const string FindBlendShapeNamesMethodName = "FindBlendShapeNames";

        /// <summary>対応表の型の、キーの名前を番号順に返すメソッド名</summary>
        internal const string GetKeyNamesMethodName = "GetKeyNames";

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
        private static MethodInfo _findBlendShapeNames;

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

        /// <summary>
        /// Generatorの対応表で、メッシュのシェイプ名からSetの割り当てを作れるか。
        /// Generatorが導入されていても、対応表を公開する前の版（0.2.1以前）ならfalse。
        /// 対応表のキーの並びがManagerの持つ並びと食い違うときもfalseにする。
        /// Setはキーの番号を添字にした配列なので、並びが違うと別のキーへ割り当ててしまうためである
        /// </summary>
        public static bool CanMapShapes
        {
            get
            {
                Resolve();
                return _findBlendShapeNames != null;
            }
        }

        /// <summary>
        /// Generatorの対応表で、シェイプ名からキーごとに割り当てるシェイプ名を引く。
        /// 戻り値はキーの番号を添字にした配列で、割り当てが無いキーは空文字。使えないときはnull
        /// </summary>
        public static string[] FindBlendShapeNames(IEnumerable<string> blendShapeNames)
        {
            if (!CanMapShapes)
            {
                return null;
            }

            return _findBlendShapeNames.Invoke(null, new object[] { blendShapeNames }) as string[];
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

            _findBlendShapeNames = ResolveSetMapping();
        }

        /// <summary>
        /// 対応表の型を引き、キーの並びがManagerの対応表（FaceTrackingGroupTable）と一致するときだけメソッドを返す
        /// </summary>
        private static MethodInfo ResolveSetMapping()
        {
            var mapping = FindType(SetMappingTypeName);
            if (mapping == null)
            {
                return null;
            }

            var find = mapping.GetMethod(
                FindBlendShapeNamesMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(IEnumerable<string>) },
                null);
            var getKeyNames = mapping.GetMethod(
                GetKeyNamesMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (find == null || find.ReturnType != typeof(string[])
                || getKeyNames == null || getKeyNames.ReturnType != typeof(string[]))
            {
                return null;
            }

            var keyNames = getKeyNames.Invoke(null, null) as string[];
            if (keyNames == null || !keyNames.SequenceEqual(FaceTrackingGroupTable.GetKeyNames()))
            {
                return null;
            }

            return find;
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
