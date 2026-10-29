using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// アバターにJerry's Face Tracking Templatesが残っているかの判定（仕様書5.8、E-04）
    ///
    /// Jerry'sの型は参照せず、載っているものの名前で判定する。
    /// Use All Tracking Keysのアバターでは、VRCFTが従来のパラメーターを送らないのでJerry'sは止まり、
    /// 同期bitとFXレイヤーだけが残る。さらに保存されたEyeTrackingActiveでMA_EyeRotationが目をAnimationへ切り替え、
    /// ネイティブの視線を止める
    /// </summary>
    internal static class JerrysDetection
    {
        /// <summary>Jerry'sのFXが宣言するパラメーター（FE-JsT Bridgeの判定と同じ組）</summary>
        internal const string FacialExpressionsDisabled = "FacialExpressionsDisabled";
        internal const string EyeTrackingActive = "EyeTrackingActive";

        /// <summary>VRCFTの従来パラメーターの接頭辞</summary>
        internal const string VrcftParameterPrefix = "FT/v2/";

        /// <summary>
        /// Jerry'sのMA版プレハブと、その一部として置かれるオブジェクトの名前（前方一致）
        /// </summary>
        private static readonly string[] ObjectNamePrefixes =
        {
            "MA_UE_VRCFT",
            "MA_ARKit_VRCFT",
            "MA_EyeRotation",
        };

        /// <summary>
        /// 判定の根拠を返す。見つからなければ空
        /// </summary>
        /// <param name="controllerParameterNames">アバターのMerge Animatorごとのパラメーター名</param>
        /// <param name="maParameterNames">アバターのMA Parametersに並ぶパラメーター名</param>
        /// <param name="objectNames">アバター内のGameObjectの名前</param>
        public static IReadOnlyList<string> Detect(
            IEnumerable<IReadOnlyCollection<string>> controllerParameterNames,
            IEnumerable<string> maParameterNames,
            IEnumerable<string> objectNames)
        {
            var evidence = new List<string>();

            foreach (var names in controllerParameterNames ?? Array.Empty<IReadOnlyCollection<string>>())
            {
                if (names != null && names.Contains(FacialExpressionsDisabled) && names.Contains(EyeTrackingActive))
                {
                    evidence.Add(FacialExpressionsDisabled);
                    break;
                }
            }

            var vrcft = (maParameterNames ?? Array.Empty<string>())
                .FirstOrDefault(name => name != null && name.StartsWith(VrcftParameterPrefix, StringComparison.Ordinal));
            if (vrcft != null)
            {
                evidence.Add(vrcft);
            }

            foreach (var name in objectNames ?? Array.Empty<string>())
            {
                if (name == null)
                {
                    continue;
                }

                if (ObjectNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))
                    && !evidence.Contains(name))
                {
                    evidence.Add(name);
                }
            }

            return evidence;
        }
    }
}
