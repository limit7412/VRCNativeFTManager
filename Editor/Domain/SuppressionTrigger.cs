using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// FaceEmoの抑制の発動条件（仕様書10.3、F-17）
    ///
    /// 条件は公開パラメーターの論理式で、論理和標準形（項の論理和、項はトグルの条件の論理積）で持つ。
    /// アニメーターの遷移は1本が論理積なので、項1つが遷移1本になる。
    /// 「目か口」は FaceTracking ∧ (EyeTracking ∨ LipTracking) で、項は (FT ∧ Eye) と (FT ∧ Lip) の2つになる。
    /// 解除の条件はその否定で、(¬FT) と (¬Eye ∧ ¬Lip) の2つになる。
    ///
    /// メニューに出さないトグルはパラメーターが無いので、既定値の定数として畳む（GroupCondition.Simplifyと同じ扱い）
    /// </summary>
    internal sealed class SuppressionTrigger
    {
        private SuppressionTrigger(
            IReadOnlyList<IReadOnlyList<ToggleLiteral>> onTerms,
            IReadOnlyList<IReadOnlyList<ToggleLiteral>> offTerms)
        {
            OnTerms = onTerms;
            OffTerms = offTerms;
        }

        /// <summary>発動する条件。空の項は「常に真」を表す</summary>
        public IReadOnlyList<IReadOnlyList<ToggleLiteral>> OnTerms { get; }

        /// <summary>解除する条件。空の項は「常に真」を表す</summary>
        public IReadOnlyList<IReadOnlyList<ToggleLiteral>> OffTerms { get; }

        /// <summary>メニューに出さないトグルを畳んだ結果、決して発動しないか</summary>
        public bool IsNeverEngaged => OnTerms.Count == 0;

        /// <summary>メニューに出さないトグルを畳んだ結果、常に発動しているか</summary>
        public bool IsAlwaysEngaged => OffTerms.Count == 0;

        /// <summary>設定の発動条件を、メニューに出さないトグルを畳んだ形にする</summary>
        public static SuppressionTrigger Build(FaceEmoSuppressionTrigger trigger, ToggleSet toggles)
        {
            var onTerms = Absorb(GetTerms(trigger)
                .Select(term => Simplify(term, toggles))
                .Where(term => term != null));
            return new SuppressionTrigger(onTerms, Negate(onTerms));
        }

        /// <summary>
        /// 発動条件の論理和標準形。FE-JsT Bridgeの発動条件（FacialExpressionsDisabled、LipTrackingOnly）に対応する
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<ToggleLiteral>> GetTerms(FaceEmoSuppressionTrigger trigger)
        {
            var faceTracking = new ToggleLiteral(NativeFTToggle.FaceTracking, true);
            var eye = new ToggleLiteral(NativeFTToggle.EyeTracking, true);
            var lip = new ToggleLiteral(NativeFTToggle.LipTracking, true);

            switch (trigger)
            {
                case FaceEmoSuppressionTrigger.EyeOrLip:
                    return new IReadOnlyList<ToggleLiteral>[] { new[] { faceTracking, eye }, new[] { faceTracking, lip } };
                case FaceEmoSuppressionTrigger.LipOnly:
                    return new IReadOnlyList<ToggleLiteral>[] { new[] { faceTracking, lip } };
                default:
                    throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null);
            }
        }

        /// <summary>
        /// メニューに出さないトグルを既定値で畳む。項が常に偽になればnull
        /// </summary>
        private static IReadOnlyList<ToggleLiteral> Simplify(IReadOnlyList<ToggleLiteral> term, ToggleSet toggles)
        {
            var remaining = new List<ToggleLiteral>();
            foreach (var literal in term)
            {
                var toggle = toggles[literal.Toggle];
                if (toggle.IsDynamic)
                {
                    if (!remaining.Contains(literal))
                    {
                        remaining.Add(literal);
                    }

                    continue;
                }

                if (toggle.DefaultValue != literal.Expected)
                {
                    return null;
                }
            }

            return remaining;
        }

        /// <summary>
        /// 論理和標準形の否定を、ド・モルガンと分配で論理和標準形に直す
        /// </summary>
        private static IReadOnlyList<IReadOnlyList<ToggleLiteral>> Negate(IReadOnlyList<IReadOnlyList<ToggleLiteral>> terms)
        {
            // 空の論理和（常に偽）の否定は常に真で、空の項1つで表す
            IReadOnlyList<IReadOnlyList<ToggleLiteral>> result = new IReadOnlyList<ToggleLiteral>[] { new ToggleLiteral[0] };
            foreach (var term in terms)
            {
                var next = new List<IReadOnlyList<ToggleLiteral>>();
                foreach (var partial in result)
                {
                    foreach (var literal in term)
                    {
                        var negated = literal.Negate();

                        // 同じトグルの肯定と否定を両方含む項は常に偽なので作らない
                        if (partial.Contains(literal))
                        {
                            continue;
                        }

                        next.Add(partial.Contains(negated) ? partial : partial.Concat(new[] { negated }).ToArray());
                    }
                }

                result = Absorb(next);
            }

            return result;
        }

        /// <summary>
        /// ほかの項を含む（より厳しい）項を落とす。A ∨ (A ∧ B) は A と同じ
        /// </summary>
        private static IReadOnlyList<IReadOnlyList<ToggleLiteral>> Absorb(IEnumerable<IReadOnlyList<ToggleLiteral>> terms)
        {
            var list = terms.ToList();
            var kept = new List<IReadOnlyList<ToggleLiteral>>();
            for (int i = 0; i < list.Count; i++)
            {
                bool absorbed = false;
                for (int j = 0; j < list.Count && !absorbed; j++)
                {
                    if (i == j || !list[j].All(list[i].Contains))
                    {
                        continue;
                    }

                    // 同じ中身の項が複数あれば、先のものだけを残す
                    absorbed = list[j].Count < list[i].Count || j < i;
                }

                if (!absorbed)
                {
                    kept.Add(list[i]);
                }
            }

            return kept;
        }
    }
}
