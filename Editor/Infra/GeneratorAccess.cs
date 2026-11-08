using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
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
        /// ARKit名のシェイプキーをUnified Expressions名へ変換する処理を公開する型名。Generator 0.2.3以降にある。
        /// 「ARKitをUEに変換」で、アバターにGeneratorのコンポーネントが無いときに使う（仕様書5.1）
        /// </summary>
        internal const string ConversionTypeName = "ARKitBlendShapeGenerator.UnifiedExpressionsConversion";

        /// <summary>変換の型の、レンダラーのメッシュへUE名のシェイプキーを書き足すメソッド名</summary>
        internal const string ConvertArkitShapesMethodName = "ConvertArkitShapes";

        /// <summary>
        /// 生成するシェイプキーの出力名のフィールド名。
        /// 「ARKitをUEに変換」で、アバターにGeneratorのコンポーネントがあるときに使う（仕様書5.1）
        /// </summary>
        internal const string OutputNamingFieldName = "outputNaming";

        /// <summary>
        /// Generatorの、対象のメッシュを解決する型名。
        /// アバタールート以外のオブジェクトに付けても、アバタールートの配下から顔のメッシュを探す版は、
        /// この型に<see cref="GetSearchRootsMethodName"/>を持つ
        /// </summary>
        internal const string RendererResolverTypeName = "ARKitBlendShapeGenerator.TargetRendererResolver";

        /// <summary>メッシュを探す起点を返すメソッド名。これがあれば、Generatorを子のオブジェクトに付けられる</summary>
        internal const string GetSearchRootsMethodName = "GetSearchRoots";

        /// <summary>
        /// Generatorを子のオブジェクトに付けるときの、オブジェクトの名前。
        /// Generatorの右クリックのメニューが作るオブジェクトの名前と揃える
        /// </summary>
        internal const string GeneratorObjectName = "Kx ARKit BlendShape Generator";

        /// <summary>出力名の列挙体で、ARKit名だけを出す値の名前</summary>
        internal const string OutputNamingArkit = "ARKit";

        /// <summary>出力名の列挙体で、ARKit名とUnified Expressions名の両方を出す値の名前</summary>
        internal const string OutputNamingBoth = "Both";

        /// <summary>Generatorのアセンブリ名。型が見つからない古い版でも、取り込み済みかどうかはこれで分かる</summary>
        internal static readonly string[] AssemblyNames =
        {
            "ARKitBlendShapeGenerator.Runtime",
            "ARKitBlendShapeGenerator.Editor",
        };

        /// <summary>
        /// GeneratorがDescriptorへ書くSetの名前の既定値。
        /// Generatorが無くても見積もりの判定に使うため、Generatorの<c>FaceTrackingSetNames.Generator</c>を複製して持つ。
        /// 一致はテストで確かめる
        /// </summary>
        internal const string DefaultSetName = "Kx VRC ARKit BlendShape Generator";

        private static bool _resolved;
        private static bool _imported;
        private static Type _componentType;
        private static FieldInfo _setWriteField;
        private static string _setName = DefaultSetName;
        private static MethodInfo _findBlendShapeNames;
        private static MethodInfo _convertArkitShapes;
        private static FieldInfo _outputNamingField;
        private static bool _canAttachToChild;

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

        /// <summary>
        /// Generatorがプロジェクトに取り込まれているか。連携に使えない古い版でも真になる。
        /// booth版でGeneratorを取得するボタンを出すかの判定に使う
        /// </summary>
        public static bool IsImported
        {
            get
            {
                Resolve();
                return _imported;
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

        /// <summary>
        /// GeneratorでARKit名のシェイプキーをUE名へ変換できるか。
        /// Generatorが導入されていても、変換を公開する前の版ならfalse
        /// </summary>
        public static bool CanConvertToUnifiedExpressions
        {
            get
            {
                Resolve();
                return _convertArkitShapes != null;
            }
        }

        /// <summary>
        /// レンダラーのメッシュにあるARKit名のシェイプキーから、UE名のシェイプキーを書き足す。
        /// 書き足したときはメッシュの複製がレンダラーへ差し替わる。ビルド用の複製にだけ使う。
        /// 戻り値は書き足したシェイプキー名で、使えないときはnull
        /// </summary>
        public static string[] ConvertToUnifiedExpressions(SkinnedMeshRenderer renderer)
        {
            if (!CanConvertToUnifiedExpressions || renderer == null)
            {
                return null;
            }

            return _convertArkitShapes.Invoke(null, new object[] { renderer }) as string[];
        }

        /// <summary>
        /// Generatorを、アバタールートの子に作ったオブジェクトへ付けられるか。
        /// アバタールート以外に付けたときにアバタールートの配下から顔のメッシュを探す版ならtrue。
        /// それより前の版は、付けたオブジェクトの配下しか探さないので、ルートに付ける
        /// </summary>
        public static bool CanAttachToChild
        {
            get
            {
                Resolve();
                return IsInstalled && _canAttachToChild;
            }
        }

        /// <summary>
        /// Generatorのコンポーネントの出力名を切り替えられるか。
        /// 出力名のフィールドを持たない版や、列挙体にARKitと「両方」の値が無い版ならfalse
        /// </summary>
        public static bool CanSetOutputNaming
        {
            get
            {
                Resolve();
                return IsInstalled && _outputNamingField != null;
            }
        }

        /// <summary>
        /// アバターのGeneratorの出力名がARKit名だけなら、ARKit名とUE名の「両方」にする。ビルド用の複製にだけ使う。
        /// Generatorは出力名がARKit以外のとき、メッシュに元からあるARKit名のシェイプキーもUE名へ変換し、
        /// 書くSetではUE名を優先する。「両方」にするのは、ARKit名で動かすアニメーションや他のツールを壊さないためである。
        /// 利用者がUE名か「両方」を選んでいれば、その設定に従う。
        /// 戻り値は切り替えたか
        /// </summary>
        public static bool EnableUnifiedExpressionsOutput(GameObject avatarRoot)
        {
            var generator = Find(avatarRoot);
            if (generator == null || !CanSetOutputNaming)
            {
                return false;
            }

            var current = _outputNamingField.GetValue(generator);
            if (current.ToString() != OutputNamingArkit)
            {
                return false;
            }

            _outputNamingField.SetValue(generator, Enum.Parse(_outputNamingField.FieldType, OutputNamingBoth));
            return true;
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
        /// アバターにGeneratorのコンポーネントをUndo付きで付ける。
        ///
        /// <see cref="CanAttachToChild"/>なら、アバタールートの子にオブジェクトを作って付ける。
        /// Generatorの右クリックのメニューで作ったときと同じ形になり、ルートのコンポーネントの並びを変えない。
        /// Generatorは対象のメッシュが空のとき、付けたオブジェクトの配下に無ければアバタールート直下のBody・Face・Headを優先して選ぶので、
        /// 子のオブジェクトに付けても設定せずに使える。
        /// それより前の版ではルートに直接付ける。
        ///
        /// Generatorが導入されていないか、すでに付いているときは何もせずnullを返す
        /// </summary>
        public static Component Attach(GameObject avatarRoot, string undoName)
        {
            if (avatarRoot == null || !IsInstalled || IsPresent(avatarRoot))
            {
                return null;
            }

            var group = Undo.GetCurrentGroup();
            var target = avatarRoot;
            if (CanAttachToChild)
            {
                target = new GameObject(GeneratorObjectName);
                GameObjectUtility.SetParentAndAlign(target, avatarRoot);
                Undo.RegisterCreatedObjectUndo(target, undoName);
            }

            // 親を決めてから付ける。付けたときのResetが、アバタールートを見て対象のメッシュを決めるため
            var generator = Undo.AddComponent(target, _componentType);
            Undo.SetCurrentGroupName(undoName);
            Undo.CollapseUndoOperations(group);
            return generator;
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
            _imported = AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => AssemblyNames.Contains(assembly.GetName().Name));
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
            _convertArkitShapes = ResolveConversion();
            _outputNamingField = ResolveOutputNaming();
            _canAttachToChild = FindType(RendererResolverTypeName)?.GetMethod(
                GetSearchRootsMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Transform) },
                null) != null;
        }

        /// <summary>出力名のフィールドを引き、列挙体にARKitと「両方」の値があるときだけ返す</summary>
        private static FieldInfo ResolveOutputNaming()
        {
            var field = _componentType.GetField(OutputNamingFieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || !field.FieldType.IsEnum)
            {
                return null;
            }

            var names = Enum.GetNames(field.FieldType);
            return names.Contains(OutputNamingArkit) && names.Contains(OutputNamingBoth) ? field : null;
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

        /// <summary>UE変換の型を引き、引数と戻り値の型が合うときだけメソッドを返す</summary>
        private static MethodInfo ResolveConversion()
        {
            var conversion = FindType(ConversionTypeName);
            var convert = conversion?.GetMethod(
                ConvertArkitShapesMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(SkinnedMeshRenderer) },
                null);
            return convert != null && convert.ReturnType == typeof(string[]) ? convert : null;
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
