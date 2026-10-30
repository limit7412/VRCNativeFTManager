using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// 翻訳ファイルの検証
    ///
    /// NDMFのLocalizerは、引けなかったキーを既定言語 (ja-jp) から補う。
    /// 実行時の引き当てを見るだけでは英語の訳抜けが表に出ないため、poファイルを直接読む。
    /// </summary>
    public class LocalizationTests
    {
        private static readonly string[] Languages = { "ja-jp", "en-us" };

        /// <summary>
        /// インスペクタとビルド時の警告が使う文言のキー
        /// ツールチップ (.tooltip) は任意のため、ここには含めない
        /// </summary>
        private static readonly string[] RequiredKeys =
        {
            "common.ok",
            "common.cancel",
            "dialog.title",
            "warning.duplicate_component",
            "warning.duplicate_component:description",
            "guard.log.duplicate",
            "guard.dialog.duplicate_removed",
            "inspector.description",
            "update.available.vpm",
            "update.available.booth",
            "update.available.unknown",
            "update.open_releases",
            "update.dismiss",
            "update.settings.enabled",
            "update.settings.description",
            "update.available.booth_updatable",
            "update.run",
            "update.confirm",
            "update.announce.message",
            "update.announce.update",
            "update.announce.later",
            "update.completed",
            "update.incomplete",
            "update.error.unsupported",
            "update.error.release_missing",
            "update.error.asset_missing",
            "update.error.download",
            "update.error.digest",
            "update.error.archive",
            "update.error.contents",
            "update.error.version",
            "update.error.scan",
            "update.error.import",
            "update.error.import_cancelled",
            "update.error.backup",
            "update.error.delete",
            "update.error.playing",
            "common.on",
            "common.off",
            "inspector.section.status",
            "inspector.status.sets",
            "inspector.status.sets_value",
            "inspector.status.use_all_tracking_keys",
            "inspector.status.generator",
            "inspector.status.generator_none",
            "inspector.status.generator_present",
            "inspector.status.generator_set_write",
            "inspector.preset",
            "inspector.use_generator_component",
            "inspector.section.toggles",
            "inspector.menu.generated",
            "inspector.menu.expand",
            "inspector.menu.hierarchy",
            "inspector.menu.mixed_modes",
            "inspector.menu.feature_disabled",
            "inspector.menu.add_radial",
            "inspector.menu.remove_radial",
            "inspector.undo.expand_menu",
            "inspector.undo.menu_item",
            "menu.label.root",
            "menu.label.face_tracking",
            "menu.label.eye_tracking",
            "menu.label.lip_tracking",
            "menu.label.pupil",
            "menu.label.visemes",
            "menu.label.emulation",
            "menu.label.limits",
            "menu.label.eye_sync",
            "menu.label.settings",
            "menu.label.sensitivity",
            "menu.label.sensitivity_eye",
            "menu.label.sensitivity_lip",
            "inspector.section.descriptor",
            "inspector.set_use_all_tracking_keys",
            "inspector.viseme_dampen",
            "inspector.viseme_dampen_amount",
            "inspector.section.details",
            "inspector.lerp_speed",
            "inspector.excluded_set_names",
            "inspector.undo.preset",
            "inspector.undo.rules",
            "inspector.section.corrections",
            "inspector.enable_emulation",
            "inspector.enable_limits",
            "inspector.enable_eye_sync",
            "inspector.corrections.rules",
            "inspector.corrections.no_shape",
            "inspector.section.estimate",
            "inspector.estimate.sets",
            "inspector.estimate.sets_value",
            "inspector.estimate.behaviors",
            "inspector.estimate.behaviors_value",
            "inspector.estimate.instructions",
            "inspector.estimate.instructions_value",
            "inspector.estimate.operations",
            "inspector.estimate.synced_bits",
            "inspector.estimate.synced_bits_value",
            "inspector.estimate.fx_layers",
            "inspector.estimate.generator_note",
            "inspector.section.face_emo",
            "inspector.face_emo.enabled",
            "inspector.section.sensitivity",
            "inspector.sensitivity.eye",
            "inspector.sensitivity.lip",
            "inspector.sensitivity.max_multiplier",
            "inspector.sensitivity.saved",
            "inspector.sensitivity.description",
            "inspector.face_emo.description",
            "inspector.face_emo.lip_only",
            "prop.face_emote_index",
            "inspector.face_emo.expression_control",
            "prop.face_emo_trigger",
            "prop.extra_parameters",
            "inspector.extra_parameters",
            "inspector.extra_parameters.multi_edit",
            "prop.extra_parameter.menu_item",
            "prop.extra_parameter.parameter_name",
            "prop.extra_parameter.parameter_type",
            "prop.extra_parameter.engaged_value",
            "prop.extra_parameter.released_value",
            "inspector.extra_parameter.menu_item_revert",
            "prop.remove_fx_layers",
            "inspector.remove_fx_layers",
            "warning.fx_not_found",
            "warning.layer_not_found",
            "warning.fx_not_editable",
            "warning.synced_layer_detached",
            "warning.layer_control_detached",
            "warning.layer_control_not_editable",
            "info.layers_removed",
            "warning.extra_parameter.empty_name",
            "warning.extra_parameter.missing_menu_item",
            "warning.extra_parameter.outside_avatar",
            "warning.extra_parameter.unsupported_control",
            "warning.extra_parameter.unnamed_parameter",
            "warning.extra_parameter.shared_auto_name",
            "warning.extra_parameter.reserved_name",
            "warning.extra_parameter.duplicate_name",
            "warning.extra_parameter.automatic_value",
            "warning.extra_parameter.shared_type",
        };

        /// <summary>
        /// enumの選択肢に当てる訳語のキー。インスペクタとドロワーの一覧から直接確かめる
        /// </summary>
        private static IEnumerable<string> EnumLabelKeys =>
            Presentation.NativeFTManagerComponentEditor.TriggerLabelKeys
                .Concat(Presentation.ExtraParameterEntryDrawer.SourceLabelKeys)
                .Concat(Presentation.ExtraParameterEntryDrawer.ToggleStateLabelKeys)
                .Concat(Presentation.ExtraParameterEntryDrawer.ReleaseModeLabelKeys)
                .Concat(Presentation.ExtraParameterEntryDrawer.SyncModeLabelKeys)
                .Concat(new[]
                {
                    Presentation.ExtraParameterEntryDrawer.AutoNameNoteKey,
                    Presentation.ExtraParameterEntryDrawer.NoParameterNoteKey,
                });

        /// <summary>
        /// poが取り込まれた結果の型を、名前で確かめる
        /// </summary>
        /// <remarks>
        /// LocalizationAssetを型として書かないのは、このアセンブリから解決できないためである。
        /// NativeFTManager.Editorでは書けるので、テスト側の参照の問題だと分かるが、
        /// 確かめたいのは「poがLocalizationAssetとして取り込まれること」であり、
        /// 型名の一致で足りる。
        /// </remarks>
        [Test]
        public void PoFiles_AreImportedAsLocalizationAssets()
        {
            foreach (var language in Languages)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PathFor(language));

                Assert.That(asset, Is.Not.Null, $"{language}.poが読み込めない");
                Assert.That(
                    asset.GetType().Name,
                    Is.EqualTo("LocalizationAsset"),
                    $"{language}.poがLocalizationAssetとして取り込まれていない");
            }
        }

        [Test]
        public void PoFiles_HaveIdenticalKeySets()
        {
            var japanese = ReadKeys("ja-jp");
            var english = ReadKeys("en-us");

            Assert.That(english.Except(japanese), Is.Empty, "ja-jpに無いキーがen-usにある");
            Assert.That(japanese.Except(english), Is.Empty, "en-usに無いキーがja-jpにある");
        }

        [Test]
        public void PoFiles_ContainEveryRequiredKey()
        {
            foreach (var language in Languages)
            {
                var keys = ReadKeys(language);
                foreach (var required in RequiredKeys)
                {
                    Assert.That(keys, Contains.Item(required), $"{language}.poに{required}が無い");
                }
            }
        }

        [Test]
        public void PoFiles_ContainEveryEnumLabelKey()
        {
            foreach (var language in Languages)
            {
                var keys = ReadKeys(language);
                foreach (var required in EnumLabelKeys)
                {
                    Assert.That(keys, Contains.Item(required), $"{language}.poに{required}が無い");
                }
            }
        }

        /// <summary>
        /// 警告とエラーは、コードから組み立てたキーで引くため、一覧に書き漏らしても気付きにくい。
        /// コードの表から直接確かめる
        /// </summary>
        [Test]
        public void PoFiles_ContainEveryDiagnosticKey()
        {
            foreach (var language in Languages)
            {
                var keys = ReadKeys(language);
                foreach (var code in Domain.DiagnosticCodes.All)
                {
                    var diagnostic = new Domain.Diagnostic(code);
                    Assert.That(keys, Contains.Item(diagnostic.LocalizationKey), $"{language}.poに{code}が無い");
                    Assert.That(
                        keys,
                        Contains.Item(diagnostic.LocalizationKey + ":description"),
                        $"{language}.poに{code}の説明が無い");
                }
            }
        }

        private static string PathFor(string language)
        {
            return $"{Localization.GetLocalizationRoot()}/{language}.po";
        }

        private static IReadOnlyCollection<string> ReadKeys(string language)
        {
            var path = PathFor(language);
            Assert.That(File.Exists(path), Is.True, $"{path}が見つからない");

            var keys = new HashSet<string>();
            foreach (Match match in Regex.Matches(File.ReadAllText(path), "^msgid \"(.*)\"", RegexOptions.Multiline))
            {
                var key = match.Groups[1].Value;

                // 先頭の空msgidはpoのヘッダー
                if (key.Length > 0)
                {
                    keys.Add(key);
                }
            }

            return keys;
        }
    }
}
