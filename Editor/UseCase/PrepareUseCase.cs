using nadena.dev.ndmf;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// Generating Phaseの処理（仕様書9章の手順1）
    ///
    /// Modular Avatarのマージより前に、FXのトグルレイヤーとメニュー項目を用意する段階である。
    /// 現時点では処理対象の選定と重複の報告だけを行い、生成はM1で実装する。
    /// </summary>
    internal static class PrepareUseCase
    {
        public static void Execute(BuildContext context)
        {
            var avatarRoot = context.AvatarRootObject;
            var components = ComponentSelection.CollectComponents(avatarRoot);

            var primary = ComponentSelection.SelectPrimaryComponent(avatarRoot, components);
            if (primary == null)
            {
                return;
            }

            if (components.Length > 1)
            {
                ErrorReport.ReportError(
                    Localization.Localizer,
                    ErrorSeverity.NonFatal,
                    "warning.duplicate_component",
                    primary);
            }
        }
    }
}
