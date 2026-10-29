using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using nadena.dev.ndmf;
using NativeFTManager.Domain;
using NativeFTManager.Infra;
using Object = UnityEngine.Object;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// FaceEmoの抑制（仕様書10.3、9章の手順2）
    ///
    /// Generating Phaseで、Modular Avatarのマージより前に動く。FE-JsT BridgeのGenerateBridgeUseCaseを移したもの。
    /// FaceEmoのアセットは読まず、マージ後のFXでパラメーター名が一致すればよい。
    /// 環境が揃っていなくても生成は続ける。聞き手のいないパラメーターを駆動するDriverは実行時に無害で、
    /// 中断するとFaceEmoを後から追加する導入順に対して不必要に厳しくなる。
    /// FaceEmoが見つからなくてもFaceEmoを止めるレイヤーは作る（I-04で知らせるだけにする）。
    /// FaceEmoの検出はMerge Animatorのパラメーター名によるので、ビルド中に生成される構成では見落としうるため
    /// （FE-JsT Bridgeと同じ扱い）
    /// </summary>
    internal static class FaceEmoSuppressionUseCase
    {
        /// <summary>
        /// 抑制のコントローラを作り、指定のFXレイヤーを取り除く
        /// </summary>
        /// <returns>Merge Animatorで入れるコントローラ。作るものが無ければnull</returns>
        public static AnimatorController Prepare(
            BuildContext context,
            NativeFTManagerComponent component,
            ToggleSet toggles,
            AvatarFacts facts,
            Action<Object> saveAsset)
        {
            var settings = component.faceEmoSuppression;
            if (settings == null || !settings.enabled)
            {
                return null;
            }

            // 発動条件が決して成り立たないときは何も書かない（W-16はAvatarDiagnosticsが出す）。
            // FXレイヤーの除去は発動条件に依らないので、続けて行う
            AnimatorController controller = null;
            if (!facts.FaceEmoSuppressionNeverEngaged)
            {
                controller = WriteController(context, component, settings, toggles, facts, saveAsset);
            }

            RemoveConflictingFxLayers(context, settings.removeFxLayers);
            return controller;
        }

        private static AnimatorController WriteController(
            BuildContext context,
            NativeFTManagerComponent component,
            FaceEmoSuppressionSettings settings,
            ToggleSet toggles,
            AvatarFacts facts,
            Action<Object> saveAsset)
        {
            var avatarRoot = context.AvatarRootObject;
            var faceEmo = FaceEmoParameterNames.Raw;

            // FaceEmoの制御パラメーターは設定次第で名前が変わる。
            // 名前を必要とするのは表情制御方式だけなので、解決もそのときだけ行う。
            // FaceEmoが見つからないアバターでは解決できないのが当然なので、W-17は出さない
            if (settings.controlMethod == FaceEmoControlMethod.ExpressionControl)
            {
                faceEmo = FaceEmoParameterResolver.Resolve(avatarRoot);
                if (!faceEmo.Resolved && facts.HasFaceEmo)
                {
                    DiagnosticReporter.Report(new Diagnostic(DiagnosticCodes.FaceEmoParametersUnresolved), component);
                }
            }

            var plan = SuppressionControllerPlanner.Build(
                new SuppressionSettings(
                    settings.controlMethod,
                    settings.trigger,
                    settings.faceEmoteIndex,
                    extraParameters: ResolveExtraParameters(context, settings.extraParameters)),
                toggles,
                faceEmo);

            return plan.IsEmpty ? null : SuppressionControllerWriter.Write(plan, saveAsset);
        }

        /// <summary>
        /// 追加パラメーターの名前と値を解決し、解決できなかった項目を報告する
        ///
        /// 解決できなかった項目は生成から外すだけで、ほかの項目の生成は続ける。
        /// 名前を決められないパラメーターへ書いても何も起きないが、黙って外すと
        /// 利用者は設定が効いていない理由を知る手段がない
        /// </summary>
        private static IReadOnlyList<ExtraParameterTarget> ResolveExtraParameters(
            BuildContext context, IReadOnlyList<ExtraParameterEntry> entries)
        {
            var result = ExtraParameterResolver.Resolve(context, context.AvatarRootObject, entries);

            foreach (var issue in result.Issues)
            {
                if (issue.Reference != null)
                {
                    ErrorReport.ReportError(
                        Localization.Localizer,
                        ErrorSeverity.NonFatal,
                        issue.MessageKey,
                        issue.EntryNumber,
                        issue.Reference);
                }
                else
                {
                    ErrorReport.ReportError(
                        Localization.Localizer, ErrorSeverity.NonFatal, issue.MessageKey, issue.EntryNumber);
                }
            }

            return result.Targets;
        }

        /// <summary>
        /// 指定されたFXレイヤーを取り除く（F-19）
        ///
        /// FaceEmoが書き込みを止めている間だけ表に出てくる素体の表情レイヤーが対象になる。
        /// 取り除く相手はビルド中のFXであり、素体のアセットには触れない
        /// </summary>
        private static void RemoveConflictingFxLayers(BuildContext context, IReadOnlyList<string> requestedNames)
        {
            var avatarRoot = context.AvatarRootObject;

            // 空行だけの一覧でFXを探すと、FXの無いアバターで意味のない警告が出る
            if (!FxLayerRemovalPlan.HasRequestedName(requestedNames))
            {
                return;
            }

            var controller = FxLayerRemover.FindFxController(avatarRoot);
            if (controller == null)
            {
                ErrorReport.ReportError(Localization.Localizer, ErrorSeverity.NonFatal, "warning.fx_not_found");
                return;
            }

            var plan = FxLayerRemovalPlan.Resolve(FxLayerRemover.GetLayerNames(controller), requestedNames);

            foreach (var missing in plan.MissingNames)
            {
                ErrorReport.ReportError(Localization.Localizer, ErrorSeverity.NonFatal, "warning.layer_not_found", missing);
            }

            if (plan.IsEmpty)
            {
                return;
            }

            // ビルド中のFXはModular Avatarが複製したものである。
            // 複製はビルド用のコンテナへ保存されるため永続アセットになる。
            // プロジェクトの元アセットと区別できるのはNDMFだけなので、その判定を使う
            if (!context.IsTemporaryAsset(controller))
            {
                ErrorReport.ReportError(Localization.Localizer, ErrorSeverity.NonFatal, "warning.fx_not_editable");
                return;
            }

            var result = FxLayerRemover.Remove(controller, plan.LayerIndices);

            ErrorReport.ReportError(
                Localization.Localizer,
                ErrorSeverity.Information,
                "info.layers_removed",
                string.Join(", ", result.RemovedLayerNames));

            foreach (var detached in result.DetachedSyncedLayerNames)
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.synced_layer_detached", detached);
            }

            RemapFxLayerControls(context, result.NewLayerIndices);
        }

        /// <summary>
        /// FXのレイヤーを索引で指すVRCAnimatorLayerControlを、除去後の索引へ付け替える
        /// 付け替えないと、除去でずれた分だけ別のレイヤーを操作してしまう
        /// </summary>
        private static void RemapFxLayerControls(BuildContext context, IReadOnlyList<int> newLayerIndices)
        {
            var remap = FxLayerRemover.RemapFxLayerControls(
                FxLayerRemover.CollectAvatarControllers(context.AvatarRootObject),
                newLayerIndices,
                context.IsTemporaryAsset);

            foreach (var owner in remap.DetachedOwners.Distinct())
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.layer_control_detached", owner);
            }

            foreach (var skipped in remap.SkippedControllers.Distinct())
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.layer_control_not_editable", skipped);
            }
        }
    }
}
