using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// 分割しなかったSetの理由
    /// </summary>
    internal enum SetKeepReason
    {
        /// <summary>インスペクタの「分割しないSet」に入っている</summary>
        Excluded,

        /// <summary>既存のParameter BehaviorかState Behaviorが名前を参照している（W-10）</summary>
        ReferencedByBehavior,

        /// <summary>本製品の接頭辞を持つ（分割済み）</summary>
        AlreadySplit,
    }

    internal sealed class KeptSet
    {
        public KeptSet(string name, SetKeepReason reason)
        {
            Name = name;
            Reason = reason;
        }

        public string Name { get; }

        public SetKeepReason Reason { get; }
    }

    /// <summary>
    /// 分割で作ったSet 1件
    /// </summary>
    internal sealed class SplitSet
    {
        public SplitSet(FaceTrackingGroup group, FaceTrackingSetData data)
        {
            Group = group;
            Data = data;
        }

        public FaceTrackingGroup Group { get; }

        public FaceTrackingSetData Data { get; }
    }

    internal sealed class SetSplitResult
    {
        public SetSplitResult(
            IReadOnlyList<FaceTrackingSetData> sets,
            IReadOnlyList<SplitSet> created,
            IReadOnlyList<KeptSet> kept)
        {
            Sets = sets;
            Created = created;
            Kept = kept;
        }

        /// <summary>Descriptorへ書き戻すSetの全体（分割しなかったSetを先に、分割したSetを後に並べる）</summary>
        public IReadOnlyList<FaceTrackingSetData> Sets { get; }

        /// <summary>分割で作ったSet</summary>
        public IReadOnlyList<SplitSet> Created { get; }

        /// <summary>分割しなかったSet</summary>
        public IReadOnlyList<KeptSet> Kept { get; }
    }

    /// <summary>
    /// DescriptorのSetをグループごとに分割する（仕様書5.2、F-09）
    ///
    /// 対応を移すだけなので、キーとシェイプの対応の総数は変わらない。
    /// 元のSetが複数あるときは、元のSetごとに分ける。同じキーに別のシェイプを割り当てたSetを、
    /// 1つにまとめられないためである
    /// </summary>
    internal static class SetSplitPlanner
    {
        public static SetSplitResult Split(
            IReadOnlyList<FaceTrackingSetData> sets,
            ICollection<string> excludedNames,
            ICollection<string> namesReferencedByBehaviors,
            Func<FaceTrackingGroup, float> defaultWeight)
        {
            if (defaultWeight == null)
            {
                throw new ArgumentNullException(nameof(defaultWeight));
            }

            var keptSets = new List<FaceTrackingSetData>();
            var kept = new List<KeptSet>();
            var sources = new List<FaceTrackingSetData>();

            foreach (var set in sets ?? Array.Empty<FaceTrackingSetData>())
            {
                if (set == null)
                {
                    continue;
                }

                var reason = GetKeepReason(set.Name, excludedNames, namesReferencedByBehaviors);
                if (reason.HasValue)
                {
                    keptSets.Add(set);
                    kept.Add(new KeptSet(set.Name, reason.Value));
                    continue;
                }

                sources.Add(set);
            }

            // 残したSetの名前は使用済みとして扱う。分割済みのSetを残したまま未分割のSetを分けると、
            // 同じ名前ができ、Behaviorが保持したSetと新しいSetを名前で区別できなくなるため
            var usedNames = new HashSet<string>(kept.Select(set => set.Name).Where(name => name != null));
            var created = new List<SplitSet>();
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                foreach (var group in FaceTrackingGroupTable.Order)
                {
                    var data = Extract(sources[sourceIndex], group, string.Empty, defaultWeight(group));
                    if (data != null)
                    {
                        data.Name = ReserveSetName(group, sources.Count == 1 ? (int?)null : sourceIndex + 1, usedNames);
                        created.Add(new SplitSet(group, data));
                    }
                }
            }

            var result = keptSets.Concat(created.Select(split => split.Data)).ToArray();
            return new SetSplitResult(result, created, kept);
        }

        /// <summary>
        /// 分割後のSetの名前。元のSetが複数あるときは、何番目の元のSetか（1始まり）を後ろに付ける
        /// </summary>
        public static string GetSetName(FaceTrackingGroup group, int? sourceNumber = null)
        {
            var name = NativeFTManagerParameterNames.SetPrefix + group;
            return sourceNumber.HasValue ? name + "/" + sourceNumber.Value : name;
        }

        /// <summary>
        /// 使われていない名前を選んで予約する。望む名前が使用済みなら、番号を2から順に付けて空きを探す
        /// </summary>
        private static string ReserveSetName(FaceTrackingGroup group, int? sourceNumber, HashSet<string> usedNames)
        {
            var name = GetSetName(group, sourceNumber);
            for (int number = Math.Max(2, (sourceNumber ?? 0) + 1); !usedNames.Add(name); number++)
            {
                name = GetSetName(group, number);
            }

            return name;
        }

        private static SetKeepReason? GetKeepReason(
            string name,
            ICollection<string> excludedNames,
            ICollection<string> namesReferencedByBehaviors)
        {
            if (name != null && excludedNames != null && excludedNames.Contains(name))
            {
                return SetKeepReason.Excluded;
            }

            if (name != null && name.StartsWith(NativeFTManagerParameterNames.SetPrefix, StringComparison.Ordinal))
            {
                return SetKeepReason.AlreadySplit;
            }

            if (name != null && namesReferencedByBehaviors != null && namesReferencedByBehaviors.Contains(name))
            {
                return SetKeepReason.ReferencedByBehavior;
            }

            return null;
        }

        /// <summary>
        /// 元のSetから1グループ分の対応を写す。対応が1つも無ければnull（空のグループのSetは作らない）
        /// </summary>
        private static FaceTrackingSetData Extract(
            FaceTrackingSetData source,
            FaceTrackingGroup group,
            string name,
            float weight)
        {
            var data = FaceTrackingSetData.CreateEmpty(name, weight);
            bool any = false;

            for (int key = 0; key < FaceTrackingGroupTable.KeyCount; key++)
            {
                if (FaceTrackingGroupTable.GetGroup(key) != group)
                {
                    continue;
                }

                var shape = source.GetBlendShapeName(key);
                if (shape == null)
                {
                    continue;
                }

                data.BlendShapeNames[key] = shape;
                data.BlendShapeMultipliers[key] = FaceTrackingSetData.GetMultiplier(source.BlendShapeMultipliers, key);
                any = true;
            }

            if (group == FaceTrackingGroupTable.VisemeGroup)
            {
                for (int viseme = 0; viseme < FaceTrackingGroupTable.VisemeCount; viseme++)
                {
                    var shape = source.GetVisemeBlendShapeName(viseme);
                    if (shape == null)
                    {
                        continue;
                    }

                    data.VisemeBlendShapeNames[viseme] = shape;
                    data.VisemeBlendShapeMultipliers[viseme] =
                        FaceTrackingSetData.GetMultiplier(source.VisemeBlendShapeMultipliers, viseme);
                    any = true;
                }
            }

            return any ? data : null;
        }
    }
}
