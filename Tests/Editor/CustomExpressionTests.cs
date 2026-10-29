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
        public void Analyze_RejectsTooManyInstructionsBeforeFolding()
        {
            // 入力33個と加算32個で65命令
            var expression = string.Join(" + ", Enumerable.Repeat("JawOpen", 33));

            Assert.That(CustomExpression.Analyze(expression).Error, Does.Contain("instructions"));
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
