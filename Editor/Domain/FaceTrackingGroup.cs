using System;
using System.Collections.Generic;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// 本製品がBlendshapes Setを分ける単位（仕様書5.2）
    ///
    /// 名前はSet名とFXレイヤー名に使われるので変えない
    /// </summary>
    internal enum FaceTrackingGroup
    {
        Eye,
        EyeClosed,
        Pupil,
        Brow,
        Lip,
        Other,
    }

    /// <summary>
    /// UEキー（VRCAvatarDescriptor.FaceTrackingKey）とグループの対応（仕様書の付録B）
    ///
    /// 規則はキーの番号で持ち、シェイプ名はSetの中身から引く。
    /// そのため出力名の種別（ARKit / UE / 両方）やSetの出どころに左右されない。
    /// キーの番号と名前がSDKの列挙体と食い違っていないかは、Infra側でSDKの列挙体と突き合わせる
    /// </summary>
    internal static class FaceTrackingGroupTable
    {
        /// <summary>SDK 3.10.6-beta.2 の FaceTrackingKey.Max</summary>
        public const int KeyCount = 102;

        /// <summary>SDKのビゼームの数（VRC_AvatarDescriptor.Viseme.Count）</summary>
        public const int VisemeCount = 15;

        /// <summary>
        /// キーの名前。添字がキーの番号で、SDKの列挙体の並びと同じ
        /// </summary>
        private static readonly string[] KeyNames =
        {
            "EyeLookOutRight", "EyeLookInRight", "EyeLookUpRight", "EyeLookDownRight",
            "EyeLookOutLeft", "EyeLookInLeft", "EyeLookUpLeft", "EyeLookDownLeft",
            "EyeClosedRight", "EyeClosedLeft",
            "EyeDilationRight", "EyeDilationLeft", "EyeConstrictRight", "EyeConstrictLeft",
            "EyeSquintRight", "EyeSquintLeft", "EyeWideRight", "EyeWideLeft",
            "BrowPinchRight", "BrowPinchLeft", "BrowLowererRight", "BrowLowererLeft",
            "BrowInnerUpRight", "BrowInnerUpLeft", "BrowOuterUpRight", "BrowOuterUpLeft",
            "NasalDilationRight", "NasalDilationLeft", "NasalConstrictRight", "NasalConstrictLeft",
            "CheekSquintRight", "CheekSquintLeft", "CheekPuffRight", "CheekPuffLeft",
            "CheekSuckRight", "CheekSuckLeft",
            "JawOpen", "JawRight", "JawLeft", "JawForward", "JawBackward", "JawClench", "JawMandibleRaise",
            "MouthClosed",
            "LipSuckUpperRight", "LipSuckUpperLeft", "LipSuckLowerRight", "LipSuckLowerLeft",
            "LipSuckCornerRight", "LipSuckCornerLeft",
            "LipFunnelUpperRight", "LipFunnelUpperLeft", "LipFunnelLowerRight", "LipFunnelLowerLeft",
            "LipPuckerUpperRight", "LipPuckerUpperLeft", "LipPuckerLowerRight", "LipPuckerLowerLeft",
            "MouthUpperUpRight", "MouthUpperUpLeft", "MouthUpperDeepenRight", "MouthUpperDeepenLeft",
            "NoseSneerRight", "NoseSneerLeft", "MouthLowerDownRight", "MouthLowerDownLeft",
            "MouthUpperRight", "MouthUpperLeft", "MouthLowerRight", "MouthLowerLeft",
            "MouthCornerPullRight", "MouthCornerPullLeft", "MouthCornerSlantRight", "MouthCornerSlantLeft",
            "MouthFrownRight", "MouthFrownLeft", "MouthStretchRight", "MouthStretchLeft",
            "MouthDimpleRight", "MouthDimpleLeft", "MouthRaiserUpper", "MouthRaiserLower",
            "MouthPressRight", "MouthPressLeft", "MouthTightenerRight", "MouthTightenerLeft",
            "TongueOut", "TongueUp", "TongueDown", "TongueRight", "TongueLeft", "TongueRoll",
            "TongueBendDown", "TongueCurlUp", "TongueSquish", "TongueFlat",
            "TongueTwistRight", "TongueTwistLeft",
            "SoftPalateClose", "ThroatSwallow",
            "NeckFlexRight", "NeckFlexLeft",
        };

        public static IReadOnlyList<string> GetKeyNames()
        {
            return KeyNames;
        }

        /// <summary>
        /// キーの番号が属するグループ
        /// </summary>
        public static FaceTrackingGroup GetGroup(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= KeyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(keyIndex), keyIndex, null);
            }

            if (keyIndex <= 7)
            {
                return FaceTrackingGroup.Eye;
            }

            if (keyIndex <= 9)
            {
                return FaceTrackingGroup.EyeClosed;
            }

            if (keyIndex <= 13)
            {
                return FaceTrackingGroup.Pupil;
            }

            if (keyIndex <= 17)
            {
                return FaceTrackingGroup.Eye;
            }

            if (keyIndex <= 25)
            {
                return FaceTrackingGroup.Brow;
            }

            if (keyIndex <= 99)
            {
                return FaceTrackingGroup.Lip;
            }

            return FaceTrackingGroup.Other;
        }

        /// <summary>
        /// Setのビゼーム対応を移すグループ
        /// </summary>
        public const FaceTrackingGroup VisemeGroup = FaceTrackingGroup.Lip;

        /// <summary>
        /// 分割後のSetが並ぶ順。Descriptorへ書く順と、FXレイヤーの並びに使う
        /// </summary>
        public static readonly FaceTrackingGroup[] Order =
        {
            FaceTrackingGroup.Eye,
            FaceTrackingGroup.EyeClosed,
            FaceTrackingGroup.Pupil,
            FaceTrackingGroup.Brow,
            FaceTrackingGroup.Lip,
            FaceTrackingGroup.Other,
        };
    }
}
