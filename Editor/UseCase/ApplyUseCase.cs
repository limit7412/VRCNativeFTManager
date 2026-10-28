using nadena.dev.ndmf;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// Transforming Phaseの処理（仕様書9章の手順5）
    ///
    /// ARKit BlendShape GeneratorとModular Avatarが終わった後に、
    /// DescriptorのSetを分割し、Parameter Behaviorsを書く段階である。
    /// 現時点ではコンポーネントの除去だけを行い、分割と書き込みはM1で実装する。
    /// </summary>
    internal static class ApplyUseCase
    {
        public static void Execute(BuildContext context)
        {
            var components = ComponentSelection.CollectComponents(context.AvatarRootObject);

            // ビルド時にのみ意味を持つコンポーネントなので、処理の成否にかかわらず取り除く。
            // ビルド終盤で動作するAvatar Optimizer等から「未知のコンポーネント」として
            // 検出されないようにするため、Optimizing Phaseより前のここで削除する。
            ComponentSelection.RemoveComponents(components);
        }
    }
}
