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
            "inspector.status.generator_unsupported",
            "inspector.status.generator_present",
            "inspector.status.generator_set_write",
            "inspector.preset",
            "inspector.generator_integration",
            "inspector.section.toggles",
            "inspector.toggle.show_in_menu",
            "inspector.toggle.default_value",
            "inspector.toggle.saved",
            "inspector.section.descriptor",
            "inspector.set_use_all_tracking_keys",
            "inspector.viseme_dampen",
            "inspector.viseme_dampen_amount",
            "inspector.section.details",
            "inspector.lerp_speed",
            "inspector.excluded_set_names",
            "inspector.undo.preset",
            "inspector.undo.rules",
            "inspector.undo.toggles",
            "inspector.toggle.missing",
            "inspector.toggle.add_missing",
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
        };

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
