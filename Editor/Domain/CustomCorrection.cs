using System.Collections.Generic;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// コンポーネントに書かれたカスタム補正1件を、Unityの型を外して写したもの
    /// </summary>
    internal sealed class CustomCorrectionSource
    {
        public CustomCorrectionSource(bool enabled, string blendShape, CorrectionMode mode, string expression)
        {
            Enabled = enabled;
            BlendShape = blendShape;
            Mode = mode;
            Expression = expression;
        }

        public bool Enabled { get; }

        public string BlendShape { get; }

        public CorrectionMode Mode { get; }

        public string Expression { get; }
    }

    /// <summary>
    /// 規則にできなかったカスタム補正の理由
    /// </summary>
    internal enum CustomCorrectionProblemKind
    {
        /// <summary>シェイプ名が空</summary>
        EmptyShape,

        /// <summary>式が空、構文の誤り、知らない入力、または1式あたりの上限超過</summary>
        InvalidExpression,
    }

    /// <summary>
    /// 規則にできなかったカスタム補正1件（W-23）
    /// </summary>
    internal sealed class CustomCorrectionProblem
    {
        public CustomCorrectionProblem(int index, string ruleId, CustomCorrectionProblemKind kind, string detail)
        {
            Index = index;
            RuleId = ruleId;
            Kind = kind;
            Detail = detail;
        }

        /// <summary>コンポーネントの一覧での位置（0始まり）</summary>
        public int Index { get; }

        public string RuleId { get; }

        public CustomCorrectionProblemKind Kind { get; }

        /// <summary>式の誤りの内容。シェイプ名が空のときはnull</summary>
        public string Detail { get; }

        /// <summary>警告に並べる表記</summary>
        public string Describe()
        {
            return Kind == CustomCorrectionProblemKind.EmptyShape
                ? $"{RuleId} (no blend shape name)"
                : $"{RuleId} ({Detail})";
        }
    }

    /// <summary>
    /// コンポーネントのカスタム補正を規則にした結果
    /// </summary>
    internal sealed class CustomCorrectionSet
    {
        public CustomCorrectionSet(
            IReadOnlyList<CorrectionRule> rules,
            IReadOnlyList<CustomCorrectionProblem> problems)
        {
            Rules = rules;
            Problems = problems;
        }

        /// <summary>生成できる規則（一覧の順）</summary>
        public IReadOnlyList<CorrectionRule> Rules { get; }

        /// <summary>規則にできなかったもの（一覧の順）</summary>
        public IReadOnlyList<CustomCorrectionProblem> Problems { get; }

        public static CustomCorrectionSet Empty =>
            new CustomCorrectionSet(new CorrectionRule[0], new CustomCorrectionProblem[0]);
    }

    /// <summary>
    /// カスタム補正を表情補正の規則へ変換する
    ///
    /// アバターの顔メッシュには、トラッキングキーに1対1で割り当てられないシェイプが入っていることがある。
    /// 2つのキーが同時に立ったときだけ使う組み合わせ補正用のシェイプや、1つのキーの値に応じて順に立てる段階用のシェイプである。
    /// Blendshapes Setはキー1つにシェイプ1つを結ぶだけなので、こうしたシェイプはSetでは動かせない。
    /// カスタム補正は、シェイプ名とCustom式を利用者に書いてもらい、組み込みの規則と同じParameter Behaviorとして生成する。
    ///
    /// 無効にした項目は結果に含めない。シェイプ名か式に誤りのある項目は、規則にせずProblemsへ入れる
    /// </summary>
    internal static class CustomCorrections
    {
        /// <summary>
        /// カスタム補正の規則のidの接頭辞。組み込みの規則のidと重ならない
        /// </summary>
        public const string IdPrefix = "Custom#";

        /// <summary>
        /// 規則のid。一覧での番号（1始まり）とシェイプ名から作る。同じシェイプを動かす項目を番号で区別できる
        /// </summary>
        public static string GetId(int index, string blendShape)
        {
            var number = IdPrefix + (index + 1);
            return string.IsNullOrEmpty(blendShape) ? number : number + ":" + blendShape;
        }

        public static CustomCorrectionSet Build(IReadOnlyList<CustomCorrectionSource> sources)
        {
            var rules = new List<CorrectionRule>();
            var problems = new List<CustomCorrectionProblem>();
            for (int index = 0; index < (sources?.Count ?? 0); index++)
            {
                var source = sources[index];
                if (source == null || !source.Enabled)
                {
                    continue;
                }

                // 名前の前後の空白は、貼り付けで混ざりやすく、見た目で気付けない
                var shape = (source.BlendShape ?? string.Empty).Trim();
                var id = GetId(index, shape);
                if (shape.Length == 0)
                {
                    problems.Add(new CustomCorrectionProblem(
                        index, id, CustomCorrectionProblemKind.EmptyShape, null));
                    continue;
                }

                var expression = source.Expression ?? string.Empty;
                var info = CustomExpression.Analyze(expression);
                if (!info.IsValid)
                {
                    problems.Add(new CustomCorrectionProblem(
                        index, id, CustomCorrectionProblemKind.InvalidExpression, info.Error));
                    continue;
                }

                rules.Add(new CorrectionRule(
                    id,
                    CorrectionFeature.Custom,
                    source.Mode,
                    string.Empty,
                    new[] { new CorrectionTarget(shape, expression) }));
            }

            return new CustomCorrectionSet(rules, problems);
        }
    }
}
