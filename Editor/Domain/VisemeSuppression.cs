using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// Visemesのトグル（仕様書5.5、F-05）。FT中に、発話による口パク（ビゼーム）だけを止める
    ///
    /// Tracking ControlでMouth &amp; JawをAnimationにするとネイティブFTの口まで止まり（確認項目2）、
    /// Setのビゼーム対応はSetの重みを受けない（確認項目11）。そこで、ビゼームが動かすシェイプに
    /// 上限0のLimitBlendshapeMaxを置き、ゲートSetで切り替える。
    /// ゲートの条件は FT ∧ Lip ∧ ¬Visemes。FTを使っていないときや口のトラッキングが無効なときは口パクを残す。
    ///
    /// FTのキーにも割り当てられているシェイプは対象から外す。上限0にするとFTの口も止まるため
    /// </summary>
    internal static class VisemeSuppression
    {
        public const string RuleId = "Visemes";

        public const string GateKey = "Gate/Visemes";

        /// <summary>上限の式。ビゼームが出す値を0に抑える</summary>
        public const string Expression = "0";

        public static GroupCondition Condition => new GroupCondition(new[]
        {
            new ToggleLiteral(NativeFTToggle.FaceTracking, true),
            new ToggleLiteral(NativeFTToggle.LipTracking, true),
            new ToggleLiteral(NativeFTToggle.Visemes, false),
        });

        /// <summary>メニューに出さないトグルを畳んでも、口パクを止めることがあるか</summary>
        public static bool IsPossible(ToggleSet toggles)
        {
            return Condition.Simplify(toggles) != null;
        }

        /// <summary>ゲート。止める条件が常に偽か常に真ならゲートは要らないのでnull</summary>
        public static GateSlot GetGate(ToggleSet toggles)
        {
            var simplified = Condition.Simplify(toggles);
            return simplified == null || simplified.Literals.Count == 0 ? null : new GateSlot(GateKey, Condition);
        }

        /// <summary>
        /// 口パクを止めるシェイプ。Setのビゼーム対応と、DescriptorのLip Syncのビゼームのシェイプを
        /// 重複なく集め、FTのキーに割り当てられているものを除く
        /// </summary>
        /// <param name="sets">分割後のSet</param>
        /// <param name="lipSyncShapes">DescriptorのLip Sync（Viseme Blend Shape）のシェイプ</param>
        public static IReadOnlyList<string> GetTargetShapes(
            IReadOnlyList<FaceTrackingSetData> sets,
            IEnumerable<string> lipSyncShapes)
        {
            var trackingShapes = new HashSet<string>();
            var candidates = new List<string>();
            foreach (var set in sets ?? new FaceTrackingSetData[0])
            {
                if (set == null)
                {
                    continue;
                }

                for (int key = 0; key < FaceTrackingGroupTable.KeyCount; key++)
                {
                    var name = set.GetBlendShapeName(key);
                    if (name != null)
                    {
                        trackingShapes.Add(name);
                    }
                }

                for (int viseme = 0; viseme < FaceTrackingGroupTable.VisemeCount; viseme++)
                {
                    var name = set.GetVisemeBlendShapeName(viseme);
                    if (name != null)
                    {
                        candidates.Add(name);
                    }
                }
            }

            candidates.AddRange((lipSyncShapes ?? new string[0]).Where(name => !string.IsNullOrEmpty(name)));
            return candidates
                .Distinct()
                .Where(name => !trackingShapes.Contains(name))
                .ToArray();
        }
    }
}
