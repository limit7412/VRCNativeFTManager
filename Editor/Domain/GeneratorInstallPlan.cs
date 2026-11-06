using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// booth版で、Kx VRC ARKit BlendShape Generatorを取得して取り込むときの規則。
    ///
    /// booth版はVPMの依存解決を通らないので、同梱したGeneratorのunitypackageを取り込み忘れることがある。
    /// インスペクタのボタンからGeneratorのリリースを取りに行けるようにする。
    /// どの置かれ方でボタンを出すか、どのアセットを取るか、取り込んでよい中身か、をここに集めて検証できるようにする。
    /// 取得元とアセットの名前は、booth用zipへ同梱するときの`.github/scripts/fetch_generator.py`と同じにそろえる
    /// </summary>
    internal static class GeneratorInstallPlan
    {
        /// <summary>GeneratorのVPMのパッケージ名。package.jsonのvpmDependenciesで下限を引くのに使う</summary>
        public const string PackageName = "com.qazx7412.kx-vrc-arkit-blendshape-generator";

        /// <summary>
        /// 取り込むGeneratorの版の下限。package.jsonのvpmDependenciesにある下限と同じにする。
        /// 一致はテストで確かめる
        /// </summary>
        public const string MinimumVersion = "0.2.3";

        /// <summary>Generatorのunitypackageが持つ取り込み先</summary>
        public const string InstallRoot = "Assets/AtelierKairox/VRCARKitBlendShapeGenerator";

        private const string AssetPrefix = "VRCARKitBlendShapeGenerator_";
        private const string ZipExtension = ".zip";
        private const string UnityPackageExtension = ".unitypackage";

        /// <summary>
        /// Generatorのunitypackageなら必ず持つ取り込み先。
        /// 別のパッケージを受け入れると、Generatorが入らないまま利用者のプロジェクトへ余計なファイルを置いてしまう
        /// </summary>
        private static readonly string[] RequiredPathnames =
        {
            InstallRoot,
            InstallRoot + "/package.json",
            InstallRoot + "/Editor/ARKitBlendShapeGenerator.Editor.asmdef",
            InstallRoot + "/Runtime/ARKitBlendShapeGenerator.Runtime.asmdef",
        };

        /// <summary>
        /// 取得のボタンを出すか。booth版で、Generatorがまだ取り込まれていないときだけ出す。
        ///
        /// VPM版はVCC/ALCOMが依存を解決するので出さない。手で取り込むと同じアセンブリが二組になる。
        /// Generatorが取り込み済みなら、古い版でも出さない。別の場所へ動かされていると、取り込みで二組になるためである。
        /// 古い版の更新はW-20で案内する
        /// </summary>
        public static bool ShouldOffer(InstallLocation location, bool generatorImported)
        {
            return location == InstallLocation.Booth && !generatorImported;
        }

        /// <summary>そのタグのGeneratorのリリースに添付されるbooth用zipの名前</summary>
        public static string BoothAssetName(string tag)
        {
            return AssetPrefix + SelfUpdatePlan.AssetVersion(tag) + ZipExtension;
        }

        /// <summary>booth用zipの中にあるGeneratorのunitypackageの名前</summary>
        public static string UnityPackageName(string tag)
        {
            return AssetPrefix + SelfUpdatePlan.AssetVersion(tag) + UnityPackageExtension;
        }

        /// <summary>
        /// リリースのタグが、Managerの求める下限以上の安定版か。
        /// 下限より古い版を入れても連携に必要な機能が無く、W-20が消えない
        /// </summary>
        public static bool SatisfiesMinimum(string tag)
        {
            return PackageVersion.TryParse(tag, out var version)
                && PackageVersion.TryParse(MinimumVersion, out var minimum)
                && version.CompareTo(minimum) >= 0;
        }

        /// <summary>
        /// 取得すべきアセットを名前で選ぶ。見つからなければ偽を返す。
        /// 同じリリースにはVPM用zipも並ぶため、拡張子だけでは選べない
        /// </summary>
        public static bool TrySelectBoothAsset(IEnumerable<ReleaseAsset> assets, string tag, out ReleaseAsset selected)
        {
            selected = default;

            if (assets == null || string.IsNullOrEmpty(tag))
            {
                return false;
            }

            var expected = BoothAssetName(tag);
            foreach (var asset in assets)
            {
                if (!string.Equals(asset.Name, expected, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(asset.DownloadUrl))
                {
                    return false;
                }

                selected = asset;
                return true;
            }

            return false;
        }

        /// <summary>取り込もうとしている中身が、Generatorのunitypackageかどうか</summary>
        public static bool IsExpectedPackage(IEnumerable<string> packagedPathnames)
        {
            if (packagedPathnames == null)
            {
                return false;
            }

            var pathnames = new HashSet<string>(packagedPathnames, StringComparer.Ordinal);
            return RequiredPathnames.All(pathnames.Contains);
        }
    }
}
