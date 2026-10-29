using NativeFTManager.Domain;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// GeneratingのPrepareからTransformingのApplyへ渡す情報（BuildContext.GetStateで共有する）
    ///
    /// Generatorのコンポーネントは自分のパスで削除されるので、Generatorを読むのはPrepareだけにし、
    /// Applyに要る結果はここへ残す（仕様書9章）
    /// </summary>
    internal sealed class BuildState
    {
        /// <summary>Prepareで生成したトグルのレイヤーの計画。Prepareが何もしなかったらnull</summary>
        public ToggleControllerPlan Plan;

        /// <summary>Prepareで解決したトグル。Prepareが何もしなかったらnull</summary>
        public ToggleSet Toggles;

        /// <summary>Generatorと連携し、ビルド用の複製でSet書き込みが有効になっているか</summary>
        public bool GeneratorSetExpected;

        /// <summary>GeneratorがDescriptorへ書くSetの名前（連携できるときだけ）</summary>
        public string GeneratorSetName;
    }
}
