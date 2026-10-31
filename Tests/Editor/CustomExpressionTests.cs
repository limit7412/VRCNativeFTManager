using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class CustomExpressionTests
    {
        [Test]
        public void Analyze_CountsInputsConstantsAndOperators()
        {
            var info = CustomExpression.Analyze("JawOpen * 0.5 + TongueOut");

            Assert.That(info.IsValid, Is.True, info.Error);
            Assert.That(info.InstructionsBeforeFolding, Is.EqualTo(5));
            Assert.That(info.Instructions, Is.EqualTo(5));
            Assert.That(info.Operations, Is.EqualTo(4), "数値定数はFace Tracking Operationsに数えない");
            Assert.That(info.Inputs, Is.EqualTo(new[] { "JawOpen", "TongueOut" }));
        }

        [Test]
        public void Analyze_FoldsConstantSubexpressions()
        {
            // VRChatのドキュメントの例。JawOpen * 5 と同じ3命令になる
            var info = CustomExpression.Analyze("JawOpen * (2 + 3)");

            Assert.That(info.InstructionsBeforeFolding, Is.EqualTo(5));
            Assert.That(info.Instructions, Is.EqualTo(3));
        }

        [Test]
        public void Analyze_CountsFunctionsAndUnaryMinus()
        {
            var info = CustomExpression.Analyze("clamp(-EyeWideLeft, 0, max(Voice, Viseme_aa))");

            // clamp、単項マイナス、EyeWideLeft、0、max、Voice、Viseme_aa
            Assert.That(info.IsValid, Is.True, info.Error);
            Assert.That(info.Instructions, Is.EqualTo(7));
            Assert.That(info.Inputs, Is.EqualTo(new[] { "EyeWideLeft", "Voice", "Viseme_aa" }));
        }

        [Test]
        public void Analyze_VisemeInputsAreLowerCase()
        {
            // VRChatのドキュメントの名前（Viseme_pp、Viseme_e など）。大文字の綴りは別の名前として扱われる
            Assert.That(CustomExpression.Analyze("Viseme_pp + Viseme_e + Viseme_rr").IsValid, Is.True);
            Assert.That(CustomExpression.Analyze("Viseme_PP").Error, Does.Contain("unknown input"));
        }

        [TestCase("", "empty")]
        [TestCase("JawOpen +", "end")]
        [TestCase("(JawOpen", "')'")]
        [TestCase("jawopen", "unknown input")]
        [TestCase("pow(JawOpen, 2)", "unknown function")]
        [TestCase("min(JawOpen)", "arguments")]
        [TestCase("JawOpen % 2", "unexpected character")]
        public void Analyze_RejectsInvalidExpressions(string expression, string error)
        {
            var info = CustomExpression.Analyze(expression);

            Assert.That(info.IsValid, Is.False);
            Assert.That(info.Error, Does.Contain(error));
        }

        [Test]
        public void Limits_MatchVrcSdk3106()
        {
            // SDK 3.10.6のFaceTrackingExpressionValidatorの値
            Assert.That(CustomExpression.MaxLength, Is.EqualTo(1024));
            Assert.That(CustomExpression.MaxTokens, Is.EqualTo(256));
            Assert.That(CustomExpression.MaxInstructionsBeforeFolding, Is.EqualTo(256));
            Assert.That(CustomExpression.AvatarInstructionLimit, Is.EqualTo(2048));
            Assert.That(CustomExpression.MaxNestingDepth, Is.EqualTo(32));
            Assert.That(CustomExpression.MaxStackDepth, Is.EqualTo(32));
        }

        [Test]
        public void Analyze_AcceptsExpressionsAboveTheBeta2Limits()
        {
            // 入力60個と加算59個で119命令。3.10.6-beta.2の上限（64命令）は超えるが、3.10.6では通る
            var expression = string.Join(" + ", Enumerable.Repeat("JawOpen", 60));

            var info = CustomExpression.Analyze(expression);

            Assert.That(info.IsValid, Is.True, info.Error);
            Assert.That(info.InstructionsBeforeFolding, Is.EqualTo(119));
        }

        [Test]
        public void Analyze_TokenLimitIsReachedBeforeTheInstructionLimit()
        {
            // 命令になるトークン（数値・入力・演算子・関数名）は1つで1命令、括弧とカンマは0命令なので、
            // 命令数はトークン数を超えない。上限がどちらも256なので、トークン数の上限が先に掛かる。
            // 入力128個と加算127個で255トークン・255命令は通る（長さの上限に掛からないよう短い入力名を空白なしで並べる）
            var expression = string.Join("+", Enumerable.Repeat("Voice", 128));

            var info = CustomExpression.Analyze(expression);

            Assert.That(info.IsValid, Is.True, info.Error);
            Assert.That(info.InstructionsBeforeFolding, Is.EqualTo(255));
        }

        [Test]
        public void Analyze_RejectsTooManyTokens()
        {
            // 入力129個と加算128個で257トークン（773文字で長さの上限には掛からない）
            var expression = string.Join("+", Enumerable.Repeat("Voice", 129));

            Assert.That(CustomExpression.Analyze(expression).Error, Does.Contain("tokens"));
        }

        [Test]
        public void Analyze_RejectsTooLongExpressions()
        {
            var expression = "JawOpen" + new string(' ', CustomExpression.MaxLength);

            Assert.That(CustomExpression.Analyze(expression).Error, Does.Contain("characters"));
        }

        [TestCase(32, true)]
        [TestCase(33, false)]
        public void Analyze_LimitsNestingDepth(int depth, bool valid)
        {
            var expression = new string('(', depth) + "JawOpen" + new string(')', depth);

            var info = CustomExpression.Analyze(expression);

            Assert.That(info.IsValid, Is.EqualTo(valid), info.Error);
            if (!valid)
            {
                Assert.That(info.Error, Does.Contain("levels deep"));
            }
        }

        [TestCase(32, true)]
        [TestCase(33, false)]
        public void Analyze_LimitsStackDepth(int depth, bool valid)
        {
            // JawOpen + (JawOpen + (JawOpen + ...)) は右辺を積み続けるので、入力の数だけスタックが深くなる。
            // 括弧の入れ子は depth - 1 段で、入れ子の上限には掛からない
            var expression = string.Join(" + (", Enumerable.Repeat("JawOpen", depth)) + new string(')', depth - 1);

            var info = CustomExpression.Analyze(expression);

            Assert.That(info.IsValid, Is.EqualTo(valid), info.Error);
            if (!valid)
            {
                Assert.That(info.Error, Does.Contain("stack"));
            }
        }

        [Test]
        public void Analyze_LeftAssociativeChainsKeepTheStackShallow()
        {
            // 左結合の加算は積んだ分をすぐ畳むので、長くてもスタックは2段で済む
            var expression = string.Join(" + ", Enumerable.Repeat("JawOpen", 100));

            Assert.That(CustomExpression.Analyze(expression).IsValid, Is.True);
        }

        [TestCase("JawOpen * 10000", true)]
        [TestCase("JawOpen * 10001", false)]
        [TestCase("JawOpen * 0.000000000000000001", true)]
        [TestCase("JawOpen * 0.0000000000000000001", false)]
        public void Analyze_LimitsNumericLiterals(string expression, bool valid)
        {
            var info = CustomExpression.Analyze(expression);

            Assert.That(info.IsValid, Is.EqualTo(valid), info.Error);
            if (!valid)
            {
                Assert.That(info.Error, Does.Contain("out of range"));
            }
        }

        [Test]
        public void RuleTable_EveryExpressionIsValidAndWithinLimits()
        {
            foreach (var rule in CorrectionRuleTable.All)
            {
                foreach (var target in rule.Targets)
                {
                    var info = CustomExpression.Analyze(target.Expression);
                    Assert.That(info.IsValid, Is.True, $"{rule.Id}: {info.Error}");
                    Assert.That(target.Expression.Length, Is.LessThanOrEqualTo(CustomExpression.MaxLength));
                    Assert.That(info.TokenCount, Is.LessThanOrEqualTo(CustomExpression.MaxTokens));
                }
            }
        }

        [Test]
        public void RuleTable_IdsAreUnique()
        {
            var ids = CorrectionRuleTable.All.Select(rule => rule.Id).ToArray();

            Assert.That(ids, Is.Unique);
        }

        [Test]
        public void RuleTable_FitsTheAvatarBudgetWhenEveryShapeExists()
        {
            // 全規則を全シェイプに出しても、上限に収まる（W-12は既存のCustom式があるときだけ出る）
            int total = CorrectionRuleTable.All
                .SelectMany(rule => rule.Targets)
                .Sum(target => CustomExpression.Analyze(target.Expression).Instructions);

            Assert.That(total, Is.LessThanOrEqualTo(CustomExpression.AvatarInstructionLimit));
        }
    }
}
