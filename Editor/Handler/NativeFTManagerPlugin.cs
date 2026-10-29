using nadena.dev.ndmf;
using NativeFTManager.UseCase;

[assembly: ExportsPlugin(typeof(NativeFTManager.Handler.NativeFTManagerPlugin))]

namespace NativeFTManager.Handler
{
    /// <summary>
    /// NDMFビルドのエントリポイント
    /// 処理はUseCase層へ委譲する。パスの並びは仕様書の9章に従う
    /// </summary>
    public class NativeFTManagerPlugin : Plugin<NativeFTManagerPlugin>
    {
        /// <summary>
        /// 順序を指定する相手のQualifiedName。
        /// 型を参照すると相手を必須依存にすることになるため、文字列で持つ。
        /// NDMFは存在しないプラグインへの順序指定を無視するので、相手が未導入でもビルドは通る
        /// </summary>
        internal const string ModularAvatar = "nadena.dev.modular-avatar";
        internal const string ARKitBlendShapeGenerator = "com.qazx7412.kx-vrc-arkit-blendshape-generator";

        public override string QualifiedName => "com.qazx7412.kx-vrc-native-ft-manager";
        public override string DisplayName => "Kx VRC NativeFT Manager";

        protected override void Configure()
        {
            // 生成したMerge Animator、パラメーター、メニューをModular Avatarがマージする前に置き終える。
            // FE-JsT Bridgeとは連携しない（仕様書10.2）ので、順序も指定しない
            InPhase(BuildPhase.Generating)
                .BeforePlugin(ModularAvatar)
                .Run("Prepare native face tracking", PrepareUseCase.Execute);

            // GeneratorがDescriptorへ書いたSetと、MAのマージ結果を前提にする
            InPhase(BuildPhase.Transforming)
                .AfterPlugin(ARKitBlendShapeGenerator)
                .AfterPlugin(ModularAvatar)
                .Run("Apply native face tracking", ApplyUseCase.Execute);
        }
    }
}
