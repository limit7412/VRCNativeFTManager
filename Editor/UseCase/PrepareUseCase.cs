using nadena.dev.ndmf;
using UnityEngine;
using NativeFTManager.Domain;
using NativeFTManager.Infra;
using Object = UnityEngine.Object;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// Generating Phaseの処理（仕様書9章の手順1）
    ///
    /// Modular Avatarのマージより前に、FXのトグルレイヤー、同期パラメーター、メニューを用意する。
    /// Generatorと連携するときは、ビルド用の複製の上でGeneratorのSet書き込みを有効にする。
    /// レイヤーのステートにSetの重みを書くBehaviorは、Setを分割した後のApplyで付ける
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

            var toggles = ComponentSettings.ResolveToggles(primary);
            var facts = AvatarScanner.Scan(avatarRoot, primary, toggles.Dynamic);
            DiagnosticReporter.Report(AvatarDiagnostics.Evaluate(facts), primary);

            var state = context.GetState<BuildState>();
            var integration = GeneratorIntegrationRegistry.Current;
            if (facts.Generator == GeneratorPresence.Supported && integration != null)
            {
                if (primary.generatorIntegration == GeneratorIntegration.Auto)
                {
                    // ビルド用の複製の上だけで有効にする。シーンの設定は変えない（仕様書10.1）
                    integration.EnableSetWrite(avatarRoot);
                }

                state.GeneratorSetExpected = integration.IsSetWriteEnabled(avatarRoot);
                state.GeneratorSetName = integration.SetName;
            }

            // ゲートSetのレイヤーも、グループと同じくここで作る。どのゲートが要るかはSetの中身で決まるので、
            // 使う可能性のあるものをすべて作り、使わなかったものはApplyで取り除く
            var gates = CorrectionPlanner.GetPossibleGates(toggles, ComponentSettings.GetExcludedRules(primary));
            var plan = ToggleControllerPlanner.Build(toggles, gates);
            state.Plan = plan;
            state.Toggles = toggles;

            var controller = plan.Layers.Count > 0 || plan.VisemeLayer != null
                ? ToggleControllerWriter.Write(plan, asset => SaveAsset(context, asset))
                : null;

            GeneratedObjectsInstaller.Install(
                avatarRoot,
                controller,
                toggles.Dynamic,
                MenuPlan.Build(toggles),
                asset => SaveAsset(context, asset));
        }

        private static void SaveAsset(BuildContext context, Object asset)
        {
            context.AssetSaver.SaveAsset(asset);
        }
    }
}
