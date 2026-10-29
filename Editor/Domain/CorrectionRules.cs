using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// 表情補正の種類（仕様書5.4）。ゲートSetの名前に使うので変えない
    /// </summary>
    internal enum CorrectionFeature
    {
        Emulation,
        Limits,
        EyeSync,
    }

    /// <summary>
    /// Parameter Behaviorの対象の種類。SDKのParameterBehaviorTargetModeのうち本製品が使うもの
    /// </summary>
    internal enum CorrectionMode
    {
        /// <summary>式の結果をシェイプへそのまま書く</summary>
        ApplyToBlendshape,

        /// <summary>シェイプの最終値の上限を式の結果にする</summary>
        LimitMax,

        /// <summary>シェイプの最終値の下限を式の結果にする</summary>
        LimitMin,
    }

    /// <summary>
    /// 規則が書き込む先1つ。シェイプ名はビルド時にSetから引く
    /// </summary>
    internal sealed class CorrectionTarget
    {
        public CorrectionTarget(int key, string expression)
        {
            Key = key;
            Expression = expression;
        }

        /// <summary>対象のUEキーの番号</summary>
        public int Key { get; }

        /// <summary>Custom式。入力はUEキーの名前で書く</summary>
        public string Expression { get; }
    }

    /// <summary>
    /// 表情補正の規則1つ。左右の対など、まとめて有効・無効を切り替える単位で持つ
    /// </summary>
    internal sealed class CorrectionRule
    {
        public CorrectionRule(
            string id,
            CorrectionFeature feature,
            CorrectionMode mode,
            string source,
            IReadOnlyList<CorrectionTarget> targets)
        {
            Id = id;
            Feature = feature;
            Mode = mode;
            Source = source;
            Targets = targets;
        }

        /// <summary>インスペクタで個別に外すときのid。保存されるので変えない</summary>
        public string Id { get; }

        public CorrectionFeature Feature { get; }

        public CorrectionMode Mode { get; }

        /// <summary>移植元（Jerry's 7.0.5のブレンドツリー名）。インスペクタとドキュメントで出典を示す</summary>
        public string Source { get; }

        public IReadOnlyList<CorrectionTarget> Targets { get; }
    }

    /// <summary>
    /// 表情補正の規則表（仕様書5.4）
    ///
    /// 係数はJerry's Face Tracking Templates 7.0.5（MIT License、Copyright (c) 2023 Adjerry91）の
    /// UE版とARKit版のFXから、ブレンドツリーの閾値とアニメーションの値を読み出して式にした。
    /// Jerry'sは「Jerry'sの合成パラメーター」（SmileFrown、MouthX、EyeLid、LipFunnelなど）で書かれているので、
    /// UEキーで次のように置き換えている。
    ///
    /// - SmileFrownの笑い側 → MouthCornerPull、しかめ側 → MouthFrown
    /// - MouthX → (MouthUpperRight + MouthLowerRight - MouthUpperLeft - MouthLowerLeft) / 2
    /// - EyeLid（0 閉じ、0.75 ふつう、1 見開き）の0.75より上 → EyeWide × 0.25
    /// - LipFunnel、LipPucker、LipSuckUpperなど上下左右をまとめた値 → 構成するキーの平均
    ///
    /// エミュレーションはLimitMin（追跡した値を下回らせない）で書く。Jerry'sのダイレクトブレンドツリーは
    /// 追跡した値に足し込むが、ApplyToBlendshapeとSetの出力の合成は公開されていないので、
    /// 追跡した値を消さない下限の形にした。値が1を超えうる式はmin(…, 1)で抑える。
    ///
    /// 並びは優先度の順で、Custom式の命令数が上限を超えたときは後ろから落とす（W-12）。
    /// Jerry's 7.0.5の配線から外れている木（Limit Mouth Closed (JawOpen)、Limit Jaw Open (MouthClosed)、
    /// Limit Lip Funnel (LipPucker)）は、Jerry's版でも効いていないので移さない
    /// </summary>
    internal static class CorrectionRuleTable
    {
        private const string FunnelAverage =
            "(LipFunnelUpperRight + LipFunnelUpperLeft + LipFunnelLowerRight + LipFunnelLowerLeft)";

        private const string MouthX = "(MouthUpperRight + MouthLowerRight - MouthUpperLeft - MouthLowerLeft)";

        private const string MouthXMirrored = "(MouthUpperLeft + MouthLowerLeft - MouthUpperRight - MouthLowerRight)";

        private static IReadOnlyList<CorrectionRule> _all;

        public static IReadOnlyList<CorrectionRule> All => _all ?? (_all = Build());

        public static CorrectionRule Find(string id)
        {
            return All.FirstOrDefault(rule => rule.Id == id);
        }

        private static IReadOnlyList<CorrectionRule> Build()
        {
            var rules = new List<CorrectionRule>
            {
                // Eye Sync: EyeClosedのSetの重みを0にし、左右に平均を書く（仕様書5.4のEye Sync）
                Rule("EyeSync", CorrectionFeature.EyeSync, CorrectionMode.ApplyToBlendshape, "Eye Sync",
                    Pair("EyeClosed{0}", "(EyeClosedRight + EyeClosedLeft) * 0.5")),

                // 制限。Jerry'sの「Limits」の木
                Rule("JawOpenHelper", CorrectionFeature.Limits, CorrectionMode.LimitMin, "Jaw Open Helper",
                    // 舌を出すと口を少し開ける（TongueOut 4で全開 → TongueOut 1で0.25）
                    One("JawOpen", "TongueOut * 0.25")),
                Rule("MouthUpperUpLimitFunnel", CorrectionFeature.Limits, CorrectionMode.LimitMax,
                    "Limit Mouth Upper Up (Lip Funnel)",
                    Pair("MouthUpperUp{0}", "1 - " + FunnelAverage + " * 0.25")),
                Rule("MouthLowerDownLimitFunnel", CorrectionFeature.Limits, CorrectionMode.LimitMax,
                    "Limit Mouth Lower Down (Lip Funnel)",
                    Pair("MouthLowerDown{0}", "1 - " + FunnelAverage + " * 0.25")),
                Rule("LipPuckerLimitFunnel", CorrectionFeature.Limits, CorrectionMode.LimitMax,
                    "Limit Lip Pucker (Lip Funnel)",
                    Pair("LipPuckerUpper{0}", "1 - " + FunnelAverage + " * 0.25")
                        .Concat(Pair("LipPuckerLower{0}", "1 - " + FunnelAverage + " * 0.25"))
                        .ToArray()),
                Rule("LipSuckLimitMouthClosed", CorrectionFeature.Limits, CorrectionMode.LimitMax,
                    "Limit Lip Suck Upper / Lower (MouthClosed)",
                    // MouthClosed 3で0になる直線。MouthClosedは1までなので、閉じきっても2/3は残る
                    Pair("LipSuckUpper{0}", "1 - MouthClosed * 0.333")
                        .Concat(Pair("LipSuckLower{0}", "1 - MouthClosed * 0.333"))
                        .ToArray()),
                Rule("MouthRaiserLimitMouthClosed", CorrectionFeature.Limits, CorrectionMode.LimitMax,
                    "Limit Mouth Raiser Upper / Lower (MouthClosed)",
                    // UE版のMouth_Raiser_Upperはアニメーションが空で、Upperに効くのはARKit版（mouthShrugUpper）だけ
                    One("MouthRaiserUpper", "1 - MouthClosed").Concat(One("MouthRaiserLower", "1 - MouthClosed")).ToArray()),
                Rule("JawXLimitMouthX", CorrectionFeature.Limits, CorrectionMode.LimitMax,
                    "Limit Jaw X (MouthX)",
                    // |MouthX| 2で0になる直線
                    Pair("Jaw{0}", "1 - abs" + MouthX + " * 0.25")),
                Rule("FrownLimitMouthX", CorrectionFeature.Limits, CorrectionMode.LimitMax,
                    "Limit Frown (MouthX)",
                    // 口が同じ側へ寄ると、その側のしかめを弱める。MouthX 3で0になる直線
                    One("MouthFrownRight", "1 - max(" + MouthX + ", 0) * 0.1667")
                        .Concat(One("MouthFrownLeft", "1 - max(" + MouthXMirrored + ", 0) * 0.1667"))
                        .ToArray()),
                Rule("TongueOutLimitFunnel", CorrectionFeature.Limits, CorrectionMode.LimitMax,
                    "Tongue Out Limit (LipFunnel)",
                    // LipFunnel 0.7で0になる直線
                    One("TongueOut", "1 - " + FunnelAverage + " * 0.357")),

                // エミュレーション。Jerry'sの「Face Tracking Emulation」の木
                Rule("CheekSquintFromSmile", CorrectionFeature.Emulation, CorrectionMode.LimitMin,
                    "Cheek Squint Blend (MouthSmile)",
                    // 笑い2で全開 → 笑い1で0.5
                    Pair("CheekSquint{0}", "MouthCornerPull{0} * 0.5")),
                Rule("MouthDimpleFromSmile", CorrectionFeature.Emulation, CorrectionMode.LimitMin,
                    "Mouth Dimple Blend (MouthSmile)",
                    Pair("MouthDimple{0}", "MouthCornerPull{0} * 0.5")),
                Rule("BrowAngry", CorrectionFeature.Emulation, CorrectionMode.LimitMin,
                    "Brow Angry (NoseSneer / MouthRaiserLower)",
                    // Jerry'sは2本の木を足すので、どちらかが1に達すると眉は下がりきる。最大値で近似する
                    Pair("BrowLowerer{0}", "max(NoseSneer{0}, MouthRaiserLower)")),
                Rule("BrowSad", CorrectionFeature.Emulation, CorrectionMode.LimitMin,
                    "Brow Sad / Limit Brow Sad (MouthClosed)",
                    // 両側のしかめ0.7で全開。Jerry'sは制限が有効なときMouthClosed 2で0へ弱めるので、
                    // 制限の既定（有効）に合わせて式へ含める
                    Pair("BrowInnerUp{0}",
                        "min((MouthFrownRight + MouthFrownLeft) * 0.714, 1) * (1 - MouthClosed * 0.5)")),
                Rule("BrowWide", CorrectionFeature.Emulation, CorrectionMode.LimitMin,
                    "Brow Wide",
                    // EyeLid 0.75〜1.5で0〜1。EyeLidは1までなので、見開ききって1/3
                    Pair("BrowOuterUp{0}", "EyeWide{0} * 0.333")),
                Rule("EyeWidePupil", CorrectionFeature.Emulation, CorrectionMode.LimitMin,
                    "Eye Wide",
                    // 見開くと瞳孔を縮める（UEのみ。ARKitに瞳孔のシェイプは無い）
                    Pair("EyeConstrict{0}", "EyeWide{0}")),
                Rule("EyeSadPupil", CorrectionFeature.Emulation, CorrectionMode.LimitMin,
                    "Eye Sad",
                    // 目がふつうに開いた状態でしかめると瞳孔を広げる。見開くほど弱める
                    Pair("EyeDilation{0}", "(MouthFrownRight + MouthFrownLeft) * 0.5 * (1 - EyeWide{0})")),
            };

            return rules;
        }

        private static CorrectionRule Rule(
            string id,
            CorrectionFeature feature,
            CorrectionMode mode,
            string source,
            CorrectionTarget[] targets)
        {
            return new CorrectionRule(id, feature, mode, source, targets);
        }

        private static CorrectionTarget[] One(string key, string expression)
        {
            return new[] { new CorrectionTarget(KeyIndex(key), expression) };
        }

        /// <summary>
        /// 右と左の2つ。キー名と式の {0} にRight / Leftが入る
        /// </summary>
        private static CorrectionTarget[] Pair(string keyFormat, string expressionFormat)
        {
            return new[] { "Right", "Left" }
                .Select(side => new CorrectionTarget(
                    KeyIndex(string.Format(keyFormat, side)),
                    expressionFormat.Replace("{0}", side)))
                .ToArray();
        }

        private static int KeyIndex(string name)
        {
            var names = FaceTrackingGroupTable.GetKeyNames();
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                {
                    return i;
                }
            }

            throw new ArgumentException($"unknown key {name}", nameof(name));
        }
    }
}
