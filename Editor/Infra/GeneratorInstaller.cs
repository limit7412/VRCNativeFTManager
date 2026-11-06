using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using NativeFTManager.Domain;
using static NativeFTManager.Localization;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// booth版で、Kx VRC ARKit BlendShape GeneratorをGitHubのリリースから取得して取り込む。
    ///
    /// 取得元はGeneratorのリポジトリの最新の安定版で、booth用zipの中のunitypackageを取り込む。
    /// 手順はSelfUpdaterと同じく、取得と検証を先に済ませ、プロジェクトのファイルへ触れるのを最後に寄せる。
    /// 自己更新と違って手元のファイルは消さないので、控えは取らない。
    /// 取り込みが終わるとGeneratorのスクリプトがコンパイルされ、ドメインのリロードでGeneratorAccessが引き直す
    /// </summary>
    internal static class GeneratorInstaller
    {
        private const string LatestReleaseApiUrl =
            "https://api.github.com/repos/limit7412/VRCARKitBlendShapeGenerator/releases/latest";

        private const int RequestTimeoutSeconds = 60;
        private const string UnityPackageExtension = ".unitypackage";
        private const string ManifestFileName = "package.json";

        private static bool _isRunning;

        /// <summary>取り込みを頼んだパッケージの名前。完了の合図を選り分けるために控える</summary>
        private static string _requestedPackageName;

        /// <summary>取り込みを頼んだunitypackageの一時ファイル。合図を受けてから消す</summary>
        private static string _requestedPackagePath;

        /// <summary>取得から取り込みまでの実行中。ボタンを二重に押されても始めない</summary>
        public static bool IsRunning
        {
            get { return _isRunning; }
        }

        /// <summary>取得のボタンを出すか。booth版で、Generatorがまだ取り込まれていないときだけ真</summary>
        public static bool IsOffered
        {
            get
            {
                return GeneratorInstallPlan.ShouldOffer(
                    PackageLocation.Location,
                    GeneratorAccess.IsImported || Directory.Exists(GeneratorInstallPlan.InstallRoot));
            }
        }

        /// <summary>
        /// 最新の安定版を取得して取り込む。
        /// 呼ぶ前に利用者の同意を取ること。取り込みはUndoできない
        /// </summary>
        public static void Run()
        {
            if (_isRunning || !IsOffered)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Fail(S("generator_install.error.playing"));
                return;
            }

            _isRunning = true;

            Send(() => UnityWebRequest.Get(LatestReleaseApiUrl), request =>
            {
                if (!UpdateCheck.TryParseRelease(request.downloadHandler?.text, out var tag, out var assets))
                {
                    Fail(S("generator_install.error.release_missing"));
                    return;
                }

                if (!GeneratorInstallPlan.SatisfiesMinimum(tag))
                {
                    Fail(S("generator_install.error.too_old", tag, GeneratorInstallPlan.MinimumVersion));
                    return;
                }

                if (!GeneratorInstallPlan.TrySelectBoothAsset(assets, tag, out var asset))
                {
                    Fail(S("update.error.asset_missing", GeneratorInstallPlan.BoothAssetName(tag)));
                    return;
                }

                Download(asset, tag);
            });
        }

        private static void Download(ReleaseAsset asset, string tag)
        {
            var archivePath = FileUtil.GetUniqueTempPathInProject() + ".zip";

            Send(
                () =>
                {
                    var request = UnityWebRequest.Get(asset.DownloadUrl);
                    request.downloadHandler = new DownloadHandlerFile(archivePath);
                    return request;
                },
                _ =>
                {
                    try
                    {
                        Install(archivePath, asset, tag);
                    }
                    finally
                    {
                        Delete(archivePath);
                    }
                });
        }

        private static void Install(string archivePath, ReleaseAsset asset, string tag)
        {
            if (!SelfUpdater.TryVerifyDigest(archivePath, asset.Digest))
            {
                Fail(S("generator_install.error.digest"));
                return;
            }

            var unityPackagePath = FileUtil.GetUniqueTempPathInProject() + UnityPackageExtension;
            IReadOnlyList<UnityPackageEntry> entries;

            try
            {
                ExtractUnityPackage(archivePath, GeneratorInstallPlan.UnityPackageName(tag), unityPackagePath);

                using (var stream = File.OpenRead(unityPackagePath))
                {
                    entries = UnityPackageContents.Read(stream);
                }
            }
            catch (Exception exception) when (SelfUpdater.IsFileFailure(exception))
            {
                Delete(unityPackagePath);
                Fail(S("update.error.archive", exception.Message));
                return;
            }

            var packagedPathnames = new List<string>(entries.Count);
            foreach (var entry in entries)
            {
                packagedPathnames.Add(entry.Pathname);
            }

            if (!GeneratorInstallPlan.IsExpectedPackage(packagedPathnames) || !IsExpectedVersion(entries, tag))
            {
                Delete(unityPackagePath);
                Fail(S("generator_install.error.contents"));
                return;
            }

            // 開始から取得までの間に再生へ入られることがある。ファイルへ触る直前にもう一度確かめる
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Delete(unityPackagePath);
                Fail(S("generator_install.error.playing"));
                return;
            }

            Import(unityPackagePath);
        }

        /// <summary>
        /// 取り込みを要求し、終わったところで一時ファイルを消す。
        /// 完了の合図はグローバルで、別のunitypackageの取り込みでも鳴るので、名前を控えてから要求する
        /// </summary>
        private static void Import(string unityPackagePath)
        {
            _requestedPackageName = Path.GetFileNameWithoutExtension(unityPackagePath);
            _requestedPackagePath = unityPackagePath;

            AssetDatabase.importPackageCompleted += OnImportCompleted;
            AssetDatabase.importPackageFailed += OnImportFailed;
            AssetDatabase.importPackageCancelled += OnImportCancelled;

            try
            {
                AssetDatabase.ImportPackage(unityPackagePath, false);
            }
            catch (Exception exception) when (SelfUpdater.IsFileFailure(exception))
            {
                // ここで投げられると合図は届かない。待ち続けるとボタンが戻らない
                FinishImport();
                Fail(S("generator_install.error.import", exception.Message));
            }
        }

        private static void OnImportCompleted(string packageName)
        {
            if (!IsRequested(packageName))
            {
                return;
            }

            FinishImport();
            _isRunning = false;
            Debug.Log("[NativeFTManager] " + S("generator_install.completed"));

            // 取り込んだスクリプトをコンパイルさせる。リロードでGeneratorAccessが引き直し、インスペクタが連携の設定を出す
            AssetDatabase.Refresh();
        }

        private static void OnImportFailed(string packageName, string errorMessage)
        {
            if (!IsRequested(packageName))
            {
                return;
            }

            FinishImport();
            Fail(S("generator_install.error.import", errorMessage));
        }

        private static void OnImportCancelled(string packageName)
        {
            if (!IsRequested(packageName))
            {
                return;
            }

            FinishImport();
            Fail(S("generator_install.error.import_cancelled"));
        }

        private static bool IsRequested(string packageName)
        {
            return string.Equals(packageName, _requestedPackageName, StringComparison.Ordinal);
        }

        /// <summary>合図の購読をやめ、一時ファイルを消す</summary>
        private static void FinishImport()
        {
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageFailed -= OnImportFailed;
            AssetDatabase.importPackageCancelled -= OnImportCancelled;

            if (_requestedPackagePath != null)
            {
                Delete(_requestedPackagePath);
            }

            _requestedPackageName = null;
            _requestedPackagePath = null;
        }

        /// <summary>同梱されたpackage.jsonが、取りに行った版のものかどうか</summary>
        private static bool IsExpectedVersion(IReadOnlyList<UnityPackageEntry> entries, string tag)
        {
            var manifestPath = GeneratorInstallPlan.InstallRoot + "/" + ManifestFileName;

            foreach (var entry in entries)
            {
                if (!string.Equals(entry.Pathname, manifestPath, StringComparison.Ordinal) || entry.Asset == null)
                {
                    continue;
                }

                var manifest = Encoding.UTF8.GetString(entry.Asset);
                return PackageLocation.TryParseVersion(manifest, out var version)
                    && PackageVersion.IsSameVersion(version, tag);
            }

            return false;
        }

        /// <summary>booth用zipから、名前を指定してGeneratorのunitypackageを取り出す</summary>
        private static void ExtractUnityPackage(string archivePath, string entryName, string destination)
        {
            using (var stream = File.OpenRead(archivePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var found = archive.GetEntry(entryName);
                if (found == null)
                {
                    throw new InvalidDataException(string.Format(
                        CultureInfo.InvariantCulture, "zipに{0}が入っていません", entryName));
                }

                using (var source = found.Open())
                using (var target = File.Create(destination))
                {
                    source.CopyTo(target);
                }
            }
        }

        /// <summary>
        /// 要求を組み立てて送り、成功した場合だけ続きへ渡す。
        /// 組み立てで失敗した場合もここで止める。投げっぱなしにすると、実行中の印が立ったままになる
        /// </summary>
        private static void Send(Func<UnityWebRequest> create, Action<UnityWebRequest> onSuccess)
        {
            UnityWebRequest request = null;

            try
            {
                request = create();
                request.timeout = RequestTimeoutSeconds;
                request.SetRequestHeader("Accept", "application/vnd.github+json");

                request.SendWebRequest().completed += _ =>
                {
                    try
                    {
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            Fail(S("update.error.download", request.error));
                            return;
                        }

                        onSuccess(request);
                    }
                    finally
                    {
                        request.Dispose();
                    }
                };
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                request?.Dispose();
                Fail(S("update.error.download", exception.Message));
            }
        }

        private static void Fail(string message)
        {
            _isRunning = false;

            Debug.LogWarning("[NativeFTManager] " + message);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(S("dialog.title"), message, S("common.ok"));
            }
        }

        private static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (SelfUpdater.IsFileFailure(exception))
            {
                // 一時ファイルを消せなくても取り込みの成否は変わらない
                Debug.LogWarning(string.Format(
                    CultureInfo.InvariantCulture, "[NativeFTManager] {0}: {1}", path, exception.Message));
            }
        }
    }
}
