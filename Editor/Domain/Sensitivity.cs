using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// 感度の系統（仕様書5.6）。Jerry'sの7系統を、同期bitを抑えるために目と口の2つへまとめた
    /// </summary>
    internal enum SensitivityChannel
    {
        Eye = 0,
        Lip = 1,
    }

    /// <summary>
    /// 感度の段階1つ。Radialの値が (Lower, Upper) にある間、このステートにいる
    /// </summary>
    internal sealed class SensitivityStage
    {
        public SensitivityStage(int index, float lower, float upper, float multiplier, float weight)
        {
            Index = index;
            Lower = lower;
            Upper = upper;
            Multiplier = multiplier;
            Weight = weight;
        }

        public int Index { get; }

        /// <summary>段階の下端。最初の段階はnull（下限なし）</summary>
        public float? LowerBound => Index == 0 ? (float?)null : Lower;

        /// <summary>段階の上端。最後の段階はnull（上限なし）</summary>
        public float? UpperBound => Index == SensitivityConfig.StageCount - 1 ? (float?)null : Upper;

        public float Lower { get; }

        public float Upper { get; }

        /// <summary>この段階での実効の倍率（Setの元の倍率に対する比）</summary>
        public float Multiplier { get; }

        /// <summary>Stateで書くSetの重み（Multiplier / 倍率の上限）</summary>
        public float Weight { get; }

        public string StateName => ToggleLayerPlan.OnState + " " + Index;
    }

    /// <summary>
    /// 感度の設定の解決結果（仕様書5.6）
    ///
    /// Custom式はアニメーターのパラメーターを読めないので、Setの重みで近似する。
    /// ビルド時に対象のSetの倍率を上限の倍率kで掛けておき、Radialの段階ごとのステートで
    /// 重みを w = 段階の倍率 / k に書く。実効の倍率は k × w になる
    /// </summary>
    internal sealed class SensitivityConfig
    {
        /// <summary>段階の数</summary>
        public const int StageCount = 8;

        /// <summary>Radialの既定値。ここを等倍にする</summary>
        public const float DefaultValue = 0.5f;

        /// <summary>
        /// 段階の境目を少し下げる量。Animatorの条件はGreater / Lessの厳密な比較しかないので、
        /// 境目ちょうどの値（既定値の0.5など）が上側の段階に入るように、境目をずらす
        /// </summary>
        public const float BoundaryOffset = 0.001f;

        /// <summary>段階どうしの遷移の長さ。短すぎるステートでBehaviorが落ちるのを避ける（仕様書5.6）</summary>
        public const float StageTransitionDuration = 0.02f;

        public SensitivityConfig(bool eye, bool lip, float maxMultiplier, bool saved)
        {
            Eye = eye;
            Lip = lip;
            MaxMultiplier = Math.Max(1f, maxMultiplier);
            Saved = saved;
            Stages = BuildStages(MaxMultiplier);
        }

        public static SensitivityConfig None => new SensitivityConfig(false, false, 1f, true);

        public bool Eye { get; }

        public bool Lip { get; }

        /// <summary>Radialの上端での倍率k。対象のSetの倍率をビルド時にこの値で掛ける</summary>
        public float MaxMultiplier { get; }

        public bool Saved { get; }

        public IReadOnlyList<SensitivityStage> Stages { get; }

        public bool IsEnabled(SensitivityChannel channel)
        {
            return channel == SensitivityChannel.Eye ? Eye : Lip;
        }

        /// <summary>有効な系統（列挙体の順）</summary>
        public IReadOnlyList<SensitivityChannel> Channels =>
            new[] { SensitivityChannel.Eye, SensitivityChannel.Lip }.Where(IsEnabled).ToArray();

        /// <summary>既定値（0.5）を含む段階</summary>
        public SensitivityStage DefaultStage => FindStage(DefaultValue);

        /// <summary>
        /// グループの感度の系統。目の側（Eye、EyeClosed、Pupil、Brow）と口（Lip）に分け、
        /// NeckFlexのOtherは対象にしない
        /// </summary>
        public static SensitivityChannel? GetChannel(FaceTrackingGroup group)
        {
            switch (group)
            {
                case FaceTrackingGroup.Eye:
                case FaceTrackingGroup.EyeClosed:
                case FaceTrackingGroup.Pupil:
                case FaceTrackingGroup.Brow:
                    return SensitivityChannel.Eye;
                case FaceTrackingGroup.Lip:
                    return SensitivityChannel.Lip;
                default:
                    return null;
            }
        }

        /// <summary>グループに感度を掛けるならその系統、掛けないならnull</summary>
        public SensitivityChannel? GetEnabledChannel(FaceTrackingGroup group)
        {
            var channel = GetChannel(group);
            return channel.HasValue && IsEnabled(channel.Value) ? channel : null;
        }

        public static string GetParameterName(SensitivityChannel channel)
        {
            switch (channel)
            {
                case SensitivityChannel.Eye:
                    return NativeFTManagerParameterNames.SensitivityEye;
                case SensitivityChannel.Lip:
                    return NativeFTManagerParameterNames.SensitivityLip;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        public static string GetLabel(SensitivityChannel channel)
        {
            return channel == SensitivityChannel.Eye ? "Eye" : "Lip";
        }

        /// <summary>
        /// 分割で作ったSetのうち、感度を掛けるものの倍率を上限の倍率kまで上げる。
        /// 段階のステートは重みを k で割った値で書くので、既定の段階で元の倍率に戻る。
        /// ビゼームの倍率も同じく上げる（ビゼームの対応もSetの重みを受ける前提。確認項目11）
        /// </summary>
        /// <param name="created">分割で作ったSet</param>
        /// <param name="getLayerGroup">Setの重みを切り替えるレイヤーのグループ（Eye SyncがなければEyeClosedはEye）</param>
        /// <returns>倍率を上げたSetの数</returns>
        public int ScaleMultipliers(IEnumerable<SplitSet> created, Func<FaceTrackingGroup, FaceTrackingGroup> getLayerGroup)
        {
            if (Math.Abs(MaxMultiplier - 1f) < 1e-6f)
            {
                return 0;
            }

            int count = 0;
            foreach (var split in created)
            {
                if (!GetEnabledChannel(getLayerGroup(split.Group)).HasValue)
                {
                    continue;
                }

                Scale(split.Data.BlendShapeMultipliers);
                Scale(split.Data.VisemeBlendShapeMultipliers);
                count++;
            }

            return count;
        }

        private void Scale(float[] multipliers)
        {
            if (multipliers == null)
            {
                return;
            }

            for (int i = 0; i < multipliers.Length; i++)
            {
                multipliers[i] *= MaxMultiplier;
            }
        }

        /// <summary>Radialの値を含む段階</summary>
        public SensitivityStage FindStage(float value)
        {
            foreach (var stage in Stages)
            {
                if (stage.UpperBound == null || value < stage.UpperBound.Value)
                {
                    return stage;
                }
            }

            return Stages[Stages.Count - 1];
        }

        /// <summary>
        /// 段階の倍率。下半分は0.2刻みで0.2〜0.8、中央（既定値を含む段階）を等倍、
        /// 上半分は等倍からkまでを3等分する。0にはしない（止めたいときはトグルを切る）
        /// </summary>
        internal static float GetStageMultiplier(int index, float maxMultiplier)
        {
            const int center = StageCount / 2;
            if (index < center)
            {
                return 0.2f * (index + 1);
            }

            return 1f + (maxMultiplier - 1f) * (index - center) / (StageCount - 1 - center);
        }

        private static IReadOnlyList<SensitivityStage> BuildStages(float maxMultiplier)
        {
            var stages = new SensitivityStage[StageCount];
            for (int i = 0; i < StageCount; i++)
            {
                float lower = (float)i / StageCount - BoundaryOffset;
                float upper = (float)(i + 1) / StageCount - BoundaryOffset;
                float multiplier = GetStageMultiplier(i, maxMultiplier);
                stages[i] = new SensitivityStage(i, lower, upper, multiplier, multiplier / maxMultiplier);
            }

            return stages;
        }
    }
}
