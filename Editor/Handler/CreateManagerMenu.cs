using System.Linq;
using UnityEditor;
using UnityEngine;
using NativeFTManager.Infra;
using NativeFTManager.UseCase;

namespace NativeFTManager.Handler
{
    /// <summary>
    /// ヒエラルキーの右クリック（GameObjectメニュー）から、NativeFTManagerComponentを付けたオブジェクトを作る
    ///
    /// プレハブは配らない。中身はコンポーネント1つだけなので、プレハブにすると
    /// 版を上げるたびに既定値をプレハブへ書き直す手間が増えるうえ、利用者の設定がオーバーライドとして残る。
    /// ここで作れば、既定値はコンポーネントのResetとプリセットの1か所で決まる。
    ///
    /// EXメニューは、子にMA Menu InstallerとMA Menu Itemの階層として作る（仕様書6.1、8.3）。
    /// 利用者がJerry'sと同じ感覚で並べ替えや表示名の変更をできるようにするため
    /// </summary>
    internal static class CreateManagerMenu
    {
        private const string MenuRoot = "GameObject/Kx VRC NativeFT Manager/";

        public const string ObjectName = "Kx NativeFT Manager";

        // 10から49の範囲はヒエラルキーの右クリックにも出る
        private const int Priority = 10;

        /// <summary>
        /// Managerを作る。Generatorは必須の依存なので、汎用版とARKit版を分けず1つにした。
        /// 「Generatorコンポーネントを使う」は常にオンで作り、アバターにGeneratorが無ければ何もしない。
        /// プリセットは、アバターにGeneratorがあればARKit、無ければUEにする
        /// </summary>
        [MenuItem(MenuRoot + ObjectName, false, Priority)]
        private static void Create(MenuCommand command)
        {
            CreateObject(command, ObjectName);
        }

        internal static GameObject CreateObject(MenuCommand command, string name)
        {
            var gameObject = new GameObject(name);

            // 右クリックしたオブジェクトの子にする。複数選択では選択ごとに呼ばれ、それぞれの子に作られる
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);

            // AddComponentでResetが走り、UEプリセットの値が入る。Generatorがあればその上からARKitを当て直す。
            // Generatorはアバター全体から探す。右クリックした位置がアバターの直下とは限らないため
            var component = gameObject.AddComponent<NativeFTManagerComponent>();
            var searchRoot = MenuHierarchyScanner.GetSearchRoot(component);
            if (GeneratorAccess.IsPresent(searchRoot))
            {
                NativeFTPresets.Apply(component, NativeFTPreset.ARKit);
            }

            component.useGeneratorComponent = true;

            // プリセットのトグルの構成をそのままMenu Itemにし、以後はMenu Itemの側を正にする。
            // アバターにすでにManagerがあり、こちらが重複として消される側なら作らない。
            // コンポーネントだけが消え、Menu Installerが残ってメニューが二重になるため
            var duplicates = ComponentSelection.SelectDuplicateComponents(
                searchRoot.transform, ComponentSelection.CollectComponents(searchRoot));
            if (!duplicates.Contains(component))
            {
                var toggles = ComponentSettings.ResolveToggles(component);
                component.menuMode = NativeFTMenuMode.Hierarchy;
                MenuHierarchyBuilder.Expand(component, toggles, "Create " + name);
            }

            Selection.activeGameObject = gameObject;
            return gameObject;
        }
    }
}
