using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class ShapeMappedSetTests
    {
        [Test]
        public void Create_CopiesTheAssignments_IntoASetWithAllArrays()
        {
            var names = Enumerable.Repeat(string.Empty, FaceTrackingGroupTable.KeyCount).ToArray();
            int jawOpen = FaceTrackingGroupTable.GetKeyNames().ToList().IndexOf("JawOpen");
            names[jawOpen] = "jawOpen";

            var set = ShapeMappedSet.Create(names);

            Assert.That(set, Is.Not.Null);
            Assert.That(set.Name, Is.EqualTo(ShapeMappedSet.SetName));
            Assert.That(set.Weight, Is.EqualTo(1f));
            Assert.That(set.AssignedKeyCount, Is.EqualTo(1));
            Assert.That(set.GetBlendShapeName(jawOpen), Is.EqualTo("jawOpen"));
            Assert.That(set.BlendShapeMultipliers.Length, Is.EqualTo(FaceTrackingGroupTable.KeyCount));
            Assert.That(set.VisemeBlendShapeNames.Length, Is.EqualTo(FaceTrackingGroupTable.VisemeCount));
        }

        [Test]
        public void Create_ReturnsNull_WhenNothingIsAssigned()
        {
            var names = Enumerable.Repeat(string.Empty, FaceTrackingGroupTable.KeyCount).ToArray();

            Assert.That(ShapeMappedSet.Create(names), Is.Null);
        }

        [Test]
        public void Create_ReturnsNull_ForAnArrayOfTheWrongLength()
        {
            Assert.That(ShapeMappedSet.Create(null), Is.Null);
            Assert.That(ShapeMappedSet.Create(new[] { "jawOpen" }), Is.Null);
        }

        [Test]
        public void SetName_IsNotTreatedAsAlreadySplit()
        {
            // 接頭辞を持つSetは分割済みとして残されるので、作ったSetが分割されなくなる
            Assert.That(ShapeMappedSet.SetName.StartsWith(NativeFTManagerParameterNames.SetPrefix), Is.False);
        }
    }
}
