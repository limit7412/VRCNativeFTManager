using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// トグル1つの解決結果
    ///
    /// メニューに出すトグルは同期パラメーターになる（動的）。
    /// 出さないトグルはパラメーターを作らず、条件では既定値の定数として扱う（仕様書8.3）
    /// </summary>
    internal sealed class ResolvedToggle
    {
        public ResolvedToggle(NativeFTToggle toggle, string parameterName, bool isDynamic, bool defaultValue, bool saved)
        {
            Toggle = toggle;
            ParameterName = parameterName;
            IsDynamic = isDynamic;
            DefaultValue = defaultValue;
            Saved = saved;
        }

        public NativeFTToggle Toggle { get; }

        public string ParameterName { get; }

        /// <summary>メニューに出し、同期パラメーターを作るか</summary>
        public bool IsDynamic { get; }

        public bool DefaultValue { get; }

        public bool Saved { get; }

        /// <summary>
        /// トグルとパラメーター名の対応
        /// </summary>
        public static string GetParameterName(NativeFTToggle toggle)
        {
            switch (toggle)
            {
                case NativeFTToggle.FaceTracking:
                    return NativeFTManagerParameterNames.FaceTracking;
                case NativeFTToggle.EyeTracking:
                    return NativeFTManagerParameterNames.EyeTracking;
                case NativeFTToggle.LipTracking:
                    return NativeFTManagerParameterNames.LipTracking;
                default:
                    throw new ArgumentOutOfRangeException(nameof(toggle), toggle, null);
            }
        }
    }

    /// <summary>
    /// 全トグルの解決結果
    /// </summary>
    internal sealed class ToggleSet
    {
        private readonly Dictionary<NativeFTToggle, ResolvedToggle> _toggles;

        public ToggleSet(IEnumerable<ResolvedToggle> toggles)
        {
            _toggles = toggles.ToDictionary(toggle => toggle.Toggle);
            foreach (NativeFTToggle toggle in Enum.GetValues(typeof(NativeFTToggle)))
            {
                if (!_toggles.ContainsKey(toggle))
                {
                    throw new ArgumentException($"Toggle {toggle} is missing", nameof(toggles));
                }
            }
        }

        /// <summary>
        /// コンポーネントの設定から解決する。同じトグルが複数あれば先のものを使う
        /// </summary>
        public static ToggleSet Resolve(Func<NativeFTToggle, ToggleSettings> getSettings)
        {
            var resolved = new List<ResolvedToggle>();
            foreach (NativeFTToggle toggle in Enum.GetValues(typeof(NativeFTToggle)))
            {
                var settings = getSettings(toggle) ?? new ToggleSettings(toggle) { showInMenu = false };
                resolved.Add(new ResolvedToggle(
                    toggle,
                    ResolvedToggle.GetParameterName(toggle),
                    settings.showInMenu,
                    settings.defaultValue,
                    settings.saved));
            }

            return new ToggleSet(resolved);
        }

        public ResolvedToggle this[NativeFTToggle toggle] => _toggles[toggle];

        /// <summary>同期パラメーターを作るトグル（列挙体の順）</summary>
        public IReadOnlyList<ResolvedToggle> Dynamic =>
            _toggles.Values.Where(toggle => toggle.IsDynamic).OrderBy(toggle => (int)toggle.Toggle).ToArray();
    }

    /// <summary>
    /// 「トグルがこの値のとき」という条件1つ
    /// </summary>
    internal readonly struct ToggleLiteral : IEquatable<ToggleLiteral>
    {
        public ToggleLiteral(NativeFTToggle toggle, bool expected)
        {
            Toggle = toggle;
            Expected = expected;
        }

        public NativeFTToggle Toggle { get; }

        public bool Expected { get; }

        public ToggleLiteral Negate()
        {
            return new ToggleLiteral(Toggle, !Expected);
        }

        public bool Equals(ToggleLiteral other)
        {
            return Toggle == other.Toggle && Expected == other.Expected;
        }

        public override bool Equals(object obj)
        {
            return obj is ToggleLiteral other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Toggle * 2) + (Expected ? 1 : 0);
        }

        public override string ToString()
        {
            return (Expected ? "" : "!") + Toggle;
        }
    }

    /// <summary>
    /// Setの重みを1にする条件。トグルの条件の論理積で表す（仕様書5.2の「重みを決める条件」）
    /// </summary>
    internal sealed class GroupCondition
    {
        public GroupCondition(IReadOnlyList<ToggleLiteral> literals)
        {
            Literals = literals;
        }

        public IReadOnlyList<ToggleLiteral> Literals { get; }

        /// <summary>
        /// グループごとの条件
        ///
        /// 仕様書の条件のうち、M1でトグルを持たないもの（Pupil、EyeSync）は既定値で畳んである。
        /// Pupilは常に有効、EyeSyncは常に無効なので、PupilとEyeClosedはEyeと同じ条件になる。
        /// M2でトグルを足すときに、ここへ戻す
        /// </summary>
        public static GroupCondition For(FaceTrackingGroup group)
        {
            switch (group)
            {
                case FaceTrackingGroup.Eye:
                case FaceTrackingGroup.EyeClosed:
                case FaceTrackingGroup.Pupil:
                case FaceTrackingGroup.Brow:
                    return Of(NativeFTToggle.FaceTracking, NativeFTToggle.EyeTracking);
                case FaceTrackingGroup.Lip:
                    return Of(NativeFTToggle.FaceTracking, NativeFTToggle.LipTracking);
                case FaceTrackingGroup.Other:
                    return Of(NativeFTToggle.FaceTracking);
                default:
                    throw new ArgumentOutOfRangeException(nameof(group), group, null);
            }
        }

        private static GroupCondition Of(params NativeFTToggle[] toggles)
        {
            return new GroupCondition(toggles.Select(toggle => new ToggleLiteral(toggle, true)).ToArray());
        }

        /// <summary>
        /// トグルの既定値で評価した値
        /// </summary>
        public bool EvaluateDefaults(ToggleSet toggles)
        {
            return Literals.All(literal => toggles[literal.Toggle].DefaultValue == literal.Expected);
        }

        /// <summary>
        /// メニューに出さないトグルを既定値で畳んだ条件。
        /// 畳んだ結果が常に偽ならnull（どのステートでも重みは0）を返し、
        /// 常に真なら空の条件を返す
        /// </summary>
        public GroupCondition Simplify(ToggleSet toggles)
        {
            var remaining = new List<ToggleLiteral>();
            foreach (var literal in Literals)
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

            return new GroupCondition(remaining);
        }

        /// <summary>条件の中身が同じか（レイヤーをまとめるのに使う）</summary>
        public bool SameAs(GroupCondition other)
        {
            if (other == null || other.Literals.Count != Literals.Count)
            {
                return false;
            }

            return Literals.All(other.Literals.Contains);
        }
    }
}
