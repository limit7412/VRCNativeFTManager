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

        public const string ARKitObjectName = "Kx NativeFT Manager (ARKit)";

        // 10から49の範囲はヒエラルキーの右クリックにも出る
        private const int Priority = 10;

        /// <summary>汎用版。トグルはUEプリセットで、Generatorとは連携しない</summary>
        [MenuItem(MenuRoot + ObjectName, false, Priority)]
        private static void Create(MenuCommand command)
        {
            CreateObject(command, ObjectName, NativeFTPreset.UE, GeneratorIntegration.Off);
        }

        /// <summary>ARKit版。Kx VRC ARKit BlendShape GeneratorのSet書き込みをビルド時に有効にする</summary>
        [MenuItem(MenuRoot + ARKitObjectName, false, Priority + 1)]
        private static void CreateARKit(MenuCommand command)
        {
            CreateObject(command, ARKitObjectName, NativeFTPreset.ARKit, GeneratorIntegration.Auto);
        }

        internal static GameObject CreateObject(
            MenuCommand command,
            string name,
            NativeFTPreset preset,
            GeneratorIntegration generatorIntegration)
        {
            var gameObject = new GameObject(name);

            // 右クリックしたオブジェクトの子にする。複数選択では選択ごとに呼ばれ、それぞれの子に作られる
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);

            // AddComponentでResetが走り、UEプリセットの値が入る。ARKit版はその上からプリセットを当て直す
            var component = gameObject.AddComponent<NativeFTManagerComponent>();
            NativeFTPresets.Apply(component, preset);
            component.generatorIntegration = generatorIntegration;

            // プリセットのトグルの構成をそのままMenu Itemにし、以後はMenu Itemの側を正にする
            var toggles = ComponentSettings.ResolveToggles(component);
            component.menuMode = NativeFTMenuMode.Hierarchy;
            MenuHierarchyBuilder.Expand(component, toggles, "Create " + name);

            Selection.activeGameObject = gameObject;
            return gameObject;
        }
    }
}
