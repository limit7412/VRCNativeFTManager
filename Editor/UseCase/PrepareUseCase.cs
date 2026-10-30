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
    /// メニューをヒエラルキーのMA Menu Itemで持つときは、Menu Itemからトグルの構成を読み、メニューは作らない。
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

            // ヒエラルキーのMenu ItemはMAのマージで消えるので、MAより前のここで読む
            var menu = ComponentSettings.ReadMenu(primary, avatarRoot);
            var toggles = ComponentSettings.ResolveToggles(primary, menu);
            var facts = AvatarScanner.Scan(avatarRoot, primary, toggles, menu);
            DiagnosticReporter.Report(AvatarDiagnostics.Evaluate(facts), primary);

            var state = context.GetState<BuildState>();
            if (facts.Generator == GeneratorPresence.Present)
            {
                if (primary.generatorIntegration == GeneratorIntegration.Auto)
                {
                    // ビルド用の複製の上だけで有効にする。シーンの設定は変えない（仕様書10.1）
                    GeneratorAccess.EnableSetWrite(avatarRoot);
                }

                state.GeneratorSetExpected = GeneratorAccess.IsSetWriteEnabled(avatarRoot);
                state.GeneratorSetName = GeneratorAccess.SetName;
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

            // FaceEmoの抑制（仕様書9章の手順2）。トグルと同じくMAのマージより前に置き終える
            var suppression = FaceEmoSuppressionUseCase.Prepare(
                context, primary, toggles, facts, asset => SaveAsset(context, asset));

            GeneratedObjectsInstaller.Install(
                avatarRoot,
                controller,
                toggles.Dynamic,
                // ヒエラルキーにメニューを置く形では、メニューはMAがMenu Itemから組むので作らない。
                // 同期パラメーターは、既定値と保存をMenu Itemから読んだ値で宣言する
                menu == null ? MenuPlan.Build(toggles) : null,
                asset => SaveAsset(context, asset),
                suppression,
                plan.FloatParameters);
        }

        private static void SaveAsset(BuildContext context, Object asset)
        {
            context.AssetSaver.SaveAsset(asset);
        }
    }
}
