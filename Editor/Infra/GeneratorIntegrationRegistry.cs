using UnityEngine;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// ARKit BlendShape Generatorとの連携の窓口（仕様書10.1）
    ///
    /// Generatorの型に触るコードは、Generatorが入っているときだけコンパイルされる
    /// NativeFTManager.Editor.GeneratorCompatに置く。本体からはこのインターフェース越しに呼び、
    /// 連携用アセンブリが読み込み時に実装を登録する。登録が無ければGeneratorとは連携しない
    /// </summary>
    internal interface IGeneratorIntegration
    {
        /// <summary>GeneratorがDescriptorへ書くSetの名前</summary>
        string SetName { get; }

        /// <summary>アバターにGeneratorのコンポーネントがあるか</summary>
        bool IsPresent(GameObject avatarRoot);

        /// <summary>アバターのGeneratorでSet書き込みが有効か</summary>
        bool IsSetWriteEnabled(GameObject avatarRoot);

        /// <summary>アバターのGeneratorのSet書き込みを有効にする。ビルド用の複製にだけ使う</summary>
        void EnableSetWrite(GameObject avatarRoot);
    }

    internal static class GeneratorIntegrationRegistry
    {
        public static IGeneratorIntegration Current { get; set; }
    }
}
