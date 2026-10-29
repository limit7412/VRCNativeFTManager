using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class SetSplitPlannerTests
    {
        private static FaceTrackingSetData CreateSet(string name, params (int key, string shape)[] assignments)
        {
            var set = FaceTrackingSetData.CreateEmpty(name, 1f);
            foreach (var (key, shape) in assignments)
            {
                set.BlendShapeNames[key] = shape;
            }

            return set;
        }

        private static SetSplitResult Split(params FaceTrackingSetData[] sets)
        {
            return SetSplitPlanner.Split(sets, new string[0], new string[0], group => 1f);
        }

        [Test]
        public void Split_SingleSource_NamesSetsByGroup()
        {
            var result = Split(CreateSet("Source", (0, "eyeLookOutRight"), (9, "eyeBlinkLeft"), (36, "jawOpen")));

            Assert.That(
                result.Created.Select(split => split.Data.Name),
                Is.EqualTo(new[] { "KxNFTM/Eye", "KxNFTM/EyeClosed", "KxNFTM/Lip" }));
            Assert.That(result.Sets.Any(set => set.Name == "Source"), Is.False, "元のSetは残さない");
        }

        [Test]
        public void Split_MovesEachAssignmentToItsGroupOnly()
        {
            var source = CreateSet("Source", (0, "eyeLookOutRight"), (36, "jawOpen"));
            source.BlendShapeMultipliers[36] = 0.5f;

            var result = Split(source);
            var eye = result.Created.Single(split => split.Group == FaceTrackingGroup.Eye).Data;
            var lip = result.Created.Single(split => split.Group == FaceTrackingGroup.Lip).Data;

            Assert.That(eye.GetBlendShapeName(0), Is.EqualTo("eyeLookOutRight"));
            Assert.That(eye.GetBlendShapeName(36), Is.Null);
            Assert.That(lip.GetBlendShapeName(36), Is.EqualTo("jawOpen"));
            Assert.That(lip.BlendShapeMultipliers[36], Is.EqualTo(0.5f), "倍率は元の値をそのまま写す");
            Assert.That(eye.AssignedKeyCount + lip.AssignedKeyCount, Is.EqualTo(source.AssignedKeyCount));
        }

        [Test]
        public void Split_DoesNotCreateEmptyGroups()
        {
            var result = Split(CreateSet("Source", (36, "jawOpen")));

            Assert.That(result.Created.Select(split => split.Group), Is.EqualTo(new[] { FaceTrackingGroup.Lip }));
        }

        [Test]
        public void Split_MovesVisemesToLip()
        {
            var source = CreateSet("Source", (0, "eyeLookOutRight"));
            source.VisemeBlendShapeNames[1] = "vrc.v_aa";

            var result = Split(source);
            var lip = result.Created.Single(split => split.Group == FaceTrackingGroup.Lip).Data;
            var eye = result.Created.Single(split => split.Group == FaceTrackingGroup.Eye).Data;

            Assert.That(lip.GetVisemeBlendShapeName(1), Is.EqualTo("vrc.v_aa"));
            Assert.That(eye.GetVisemeBlendShapeName(1), Is.Null);
        }

        [Test]
        public void Split_MultipleSources_NumbersSetsBySourceOrder()
        {
            var result = Split(
                CreateSet("A", (36, "jawOpen")),
                CreateSet("B", (36, "JawOpen")));

            Assert.That(
                result.Created.Select(split => split.Data.Name),
                Is.EqualTo(new[] { "KxNFTM/Lip/1", "KxNFTM/Lip/2" }));
        }

        [Test]
        public void Split_KeepsExcludedAndReferencedSets()
        {
            var result = SetSplitPlanner.Split(
                new[]
                {
                    CreateSet("Excluded", (36, "jawOpen")),
                    CreateSet("Referenced", (36, "jawOpen")),
                    CreateSet("Source", (36, "jawOpen")),
                },
                new[] { "Excluded" },
                new[] { "Referenced" },
                group => 1f);

            Assert.That(
                result.Kept.Select(kept => (kept.Name, kept.Reason)),
                Is.EqualTo(new[]
                {
                    ("Excluded", SetKeepReason.Excluded),
                    ("Referenced", SetKeepReason.ReferencedByBehavior),
                }));
            Assert.That(
                result.Sets.Select(set => set.Name),
                Is.EqualTo(new[] { "Excluded", "Referenced", "KxNFTM/Lip" }),
                "分割しなかったSetを先に、分割したSetを後に並べる。分割の元が1つなら番号を付けない");
        }

        [Test]
        public void Split_KeepsSetsAlreadyCarryingThePrefix()
        {
            var result = Split(CreateSet("KxNFTM/Lip", (36, "jawOpen")));

            Assert.That(result.Created, Is.Empty);
            Assert.That(result.Kept.Single().Reason, Is.EqualTo(SetKeepReason.AlreadySplit));
        }

        [Test]
        public void Split_AvoidsNamesOfKeptSets()
        {
            var result = Split(
                CreateSet("KxNFTM/Lip", (36, "jawOpen")),
                CreateSet("KxNFTM/Lip/2", (36, "jawOpen")),
                CreateSet("Source", (0, "eyeLookOutRight"), (36, "jawOpen")));

            Assert.That(
                result.Created.Select(split => split.Data.Name),
                Is.EqualTo(new[] { "KxNFTM/Eye", "KxNFTM/Lip/3" }),
                "残したSetと同じ名前は使わず、空いている番号を付ける");
            Assert.That(result.Sets.Select(set => set.Name), Is.Unique);
        }

        [Test]
        public void Split_MultipleSources_StayUniqueWhenANumberIsTaken()
        {
            var result = Split(
                CreateSet("KxNFTM/Lip/1", (36, "jawOpen")),
                CreateSet("SourceA", (36, "jawOpen")),
                CreateSet("SourceB", (36, "jawOpen")));

            Assert.That(
                result.Created.Select(split => split.Data.Name),
                Is.EqualTo(new[] { "KxNFTM/Lip/2", "KxNFTM/Lip/3" }));
        }

        [Test]
        public void Split_UsesTheDefaultWeightOfEachGroup()
        {
            var result = SetSplitPlanner.Split(
                new[] { CreateSet("Source", (0, "eyeLookOutRight"), (36, "jawOpen")) },
                new string[0],
                new string[0],
                group => group == FaceTrackingGroup.Lip ? 0f : 1f);

            Assert.That(result.Created.Single(split => split.Group == FaceTrackingGroup.Lip).Data.Weight, Is.EqualTo(0f));
            Assert.That(result.Created.Single(split => split.Group == FaceTrackingGroup.Eye).Data.Weight, Is.EqualTo(1f));
        }

        [Test]
        public void Split_TreatsMissingMultipliersAsOne()
        {
            var source = CreateSet("Source", (36, "jawOpen"));
            source.BlendShapeMultipliers = null;

            var lip = Split(source).Created.Single().Data;

            Assert.That(lip.BlendShapeMultipliers[36], Is.EqualTo(1f));
        }

        [Test]
        public void CorrectionSources_LeaveOutKeptSets()
        {
            // 既存のBehaviorが重みを制御するSetの対応へ補正を足すと、重み0でも補正だけが動くので含めない
            var referenced = CreateSet("Expression", (18, "browPinchRight"));
            var source = CreateSet("Source", (36, "jawOpen"));
            var result = SetSplitPlanner.Split(new[] { referenced, source }, new string[0], new[] { "Expression" }, group => 1f);

            Assert.That(result.Sets.Select(set => set.Name), Has.Member("Expression"));
            Assert.That(result.CorrectionSources.Select(set => set.Name), Is.EqualTo(new[] { "KxNFTM/Lip" }));
        }
    }
}
