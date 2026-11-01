using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// DescriptorのEye Gaze Mode（VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeModeと同じ値）
    /// </summary>
    internal enum EyeGazeMode
    {
        BoneRotation = 0,
        FaceTracking = 1,
        Both = 2,
    }

    /// <summary>
    /// DescriptorのEyelids Type（VRCAvatarDescriptor.EyelidTypeと同じ値）
    /// </summary>
    internal enum EyelidType
    {
        None = 0,
        Bones = 1,
        Blendshapes = 2,
        FaceTracking = 3,
    }

    /// <summary>
    /// DescriptorのEye Lookの設定のうち、ネイティブFTで目を動かすかに関わるもの
    /// </summary>
    internal sealed class EyeLookSettings
    {
        public bool Enabled;
        public EyeGazeMode GazeMode;
        public EyelidType EyelidType;

        /// <summary>左右どちらかの目のボーンが割り当てられているか</summary>
        public bool HasEyeBones;
    }

    /// <summary>
    /// Eye Lookの設定をネイティブFT向けに補正した結果
    /// </summary>
    internal sealed class EyeLookCorrection
    {
        public EyeLookCorrection(EyeLookSettings before, EyeLookSettings after)
        {
            Before = before;
            After = after;
        }

        public EyeLookSettings Before { get; }

        public EyeLookSettings After { get; }

        public bool EnablesEyeLook => !Before.Enabled && After.Enabled;

        public bool ChangesGazeMode => Before.GazeMode != After.GazeMode;

        public bool ChangesEyelidType => Before.EyelidType != After.EyelidType;
    }

    /// <summary>
    /// SetにEyeLookやEyeClosedのキーがあるのに、DescriptorのEye LookがそれをFTで使わない設定のときに、
    /// ビルド用の複製で使う設定へ直す（仕様書5.7、I-07）
    ///
    /// Eye Gaze ModeがBone Rotationだと、EyeLookのキーのシェイプは動かない。
    /// Eyelids TypeがFace Trackingでないと、EyeClosedのキーはまばたきに使われない。
    /// Generatorを付けていないARKitのアバターでは、FT以前のEye Lookの設定が残っていることが多い。
    ///
    /// 利用者が選んだ動かし方を壊さないよう、直すのは次の場合に限る。
    /// - Eye Gaze Mode: Bone Rotationで、目のボーンが割り当てられていないとき（視線がどこからも動かない）だけFace Trackingにする。
    ///   ボーンがあればボーンで視線は動くので変えない（Bothにするとシェイプと二重に動きうる）
    /// - Eyelids Type: NoneかBlendshapesのときだけFace Trackingにする。Bonesはまぶたのボーンを使う設定なので変えない
    /// - Eye Look自体が無効なら、上のどちらかでFTの目を使うときに有効にする
    /// </summary>
    internal static class EyeLookSetup
    {
        /// <summary>Setの中でEyeLookのキー（0〜7）</summary>
        public static readonly IReadOnlyList<int> EyeLookKeys = Enumerable.Range(0, 8).ToArray();

        /// <summary>Setの中でEyeClosedのキー（8、9）</summary>
        public static readonly IReadOnlyList<int> EyeClosedKeys = new[] { 8, 9 };

        /// <summary>
        /// 直す必要があれば補正の結果を、無ければnullを返す
        /// </summary>
        public static EyeLookCorrection Plan(EyeLookSettings current, IEnumerable<FaceTrackingSetData> sets)
        {
            if (current == null || sets == null)
            {
                return null;
            }

            var setList = sets.Where(set => set != null).ToList();
            bool hasEyeLook = HasAnyKey(setList, EyeLookKeys);
            bool hasEyeClosed = HasAnyKey(setList, EyeClosedKeys);

            var after = new EyeLookSettings
            {
                Enabled = current.Enabled,
                GazeMode = current.GazeMode,
                EyelidType = current.EyelidType,
                HasEyeBones = current.HasEyeBones,
            };

            if (hasEyeLook && !current.HasEyeBones)
            {
                if (after.GazeMode == EyeGazeMode.BoneRotation)
                {
                    after.GazeMode = EyeGazeMode.FaceTracking;
                }

                after.Enabled = true;
            }

            if (hasEyeClosed && after.EyelidType != EyelidType.Bones)
            {
                after.EyelidType = EyelidType.FaceTracking;
                after.Enabled = true;
            }

            var correction = new EyeLookCorrection(current, after);
            return correction.EnablesEyeLook || correction.ChangesGazeMode || correction.ChangesEyelidType
                ? correction
                : null;
        }

        /// <summary>
        /// 補正の内容を通知の引数にする文字列（例: "Eye Gaze Mode: BoneRotation → FaceTracking"）
        /// </summary>
        public static string Describe(EyeLookCorrection correction)
        {
            var parts = new List<string>();
            if (correction.EnablesEyeLook)
            {
                parts.Add("Eye Look: Off → On");
            }

            if (correction.ChangesGazeMode)
            {
                parts.Add($"Eye Gaze Mode: {correction.Before.GazeMode} → {correction.After.GazeMode}");
            }

            if (correction.ChangesEyelidType)
            {
                parts.Add($"Eyelids Type: {correction.Before.EyelidType} → {correction.After.EyelidType}");
            }

            return string.Join(", ", parts);
        }

        private static bool HasAnyKey(IEnumerable<FaceTrackingSetData> sets, IReadOnlyList<int> keys)
        {
            return sets.Any(set => keys.Any(key => set.GetBlendShapeName(key) != null));
        }
    }
}
