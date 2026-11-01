using UnityEditor;
using UnityEngine;
using NativeFTManager.Domain;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// 同梱の既定アイコン（Runtime/Icons）をTexture2Dとして読む（仕様書8.3）。
    ///
    /// パスではなく`.meta`のGUIDで引く。VPM版はPackages配下、booth版はAssets配下へ入り、置き場所が変わるため。
    /// 画像はアバターのメニューから参照されるので、エディタ専用の特別なフォルダであるEditorではなくRuntime配下に置く。
    /// GUIDは配布後に変えない。利用者のMenu Itemがこの画像を参照している。
    /// 読めなければnullを返し、アイコンの無い項目になる
    /// </summary>
    internal static class MenuIconAssets
    {
        public static Texture2D Load(MenuIcon icon)
        {
            var guid = GetGuid(icon);
            if (guid == null)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static string GetGuid(MenuIcon icon)
        {
            switch (icon)
            {
                case MenuIcon.FaceTrackingMenu:
                    return "cce544459f024c5ba23b09ddd4782e70"; // face_tracking_menu.png
                case MenuIcon.FaceTracking:
                    return "4354084a93d84866ad8d6afbbd48ca39"; // face_tracking.png
                case MenuIcon.EyeTracking:
                    return "8379a40c19be43a2897f65c140ce63d4"; // eye_tracking.png
                case MenuIcon.LipTracking:
                    return "f6ee1d1803fe4f80a08751b411d6b234"; // lip_tracking.png
                case MenuIcon.Pupil:
                    return "67592400d08c4f9fabee88cdfbd9b9cd"; // pupil.png
                case MenuIcon.Visemes:
                    return "74b7bfad66864aa38f910c7c1314a746"; // visemes.png
                case MenuIcon.Settings:
                    return "b0d6ed15411b45ac8606a9856456c3fb"; // settings.png
                case MenuIcon.Emulation:
                    return "ade070aa496447dd99d73de72fc2669a"; // emulation.png
                case MenuIcon.Limits:
                    return "714c7ca36a694e01804d35d7f2b06feb"; // limits.png
                case MenuIcon.EyeSync:
                    return "542178a869094e25b9ed83c6f676fd6c"; // eye_sync.png
                case MenuIcon.Sensitivity:
                    return "7962751811694ef8b868f4d3fbcd5668"; // sensitivity.png
                case MenuIcon.SensitivityEye:
                    return "abb2334297df4e1d86e42daed4a8cdfb"; // sensitivity_eye.png
                case MenuIcon.SensitivityLip:
                    return "f765ea07cec047cd8ee1f3ef57aac5cd"; // sensitivity_lip.png
                default:
                    return null;
            }
        }
    }
}
