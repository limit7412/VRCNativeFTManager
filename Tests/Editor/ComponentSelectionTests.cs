using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NativeFTManager.UseCase;
using Object = UnityEngine.Object;

namespace NativeFTManager.Tests
{
    public class ComponentSelectionTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }

            _created.Clear();
        }

        private GameObject CreateAvatarRoot()
        {
            var root = new GameObject("Avatar");
            _created.Add(root);
            return root;
        }

        private static NativeFTManagerComponent AddComponentTo(GameObject parent, string childName = null)
        {
            if (childName == null)
            {
                return parent.AddComponent<NativeFTManagerComponent>();
            }

            var child = new GameObject(childName);
            child.transform.SetParent(parent.transform, false);
            return child.AddComponent<NativeFTManagerComponent>();
        }

        [Test]
        public void SelectPrimaryComponent_PrefersComponentOnAvatarRoot()
        {
            var root = CreateAvatarRoot();
            var onChild = AddComponentTo(root, "Child");
            var onRoot = AddComponentTo(root);

            var primary = ComponentSelection.SelectPrimaryComponent(
                root, new[] { onChild, onRoot });

            Assert.That(primary, Is.SameAs(onRoot));
        }

        [Test]
        public void SelectPrimaryComponent_ReturnsFirst_WhenNoneOnAvatarRoot()
        {
            var root = CreateAvatarRoot();
            var first = AddComponentTo(root, "First");
            var second = AddComponentTo(root, "Second");

            var primary = ComponentSelection.SelectPrimaryComponent(root, new[] { first, second });

            Assert.That(primary, Is.SameAs(first));
        }

        [Test]
        public void SelectPrimaryComponent_ReturnsNull_WhenNoComponentExists()
        {
            Assert.That(
                ComponentSelection.SelectPrimaryComponent(CreateAvatarRoot(), new NativeFTManagerComponent[0]),
                Is.Null);
        }

        [Test]
        public void SelectDuplicateComponents_ReturnsEveryComponentExceptPrimary()
        {
            var root = CreateAvatarRoot();
            var onChild = AddComponentTo(root, "Child");
            var onRoot = AddComponentTo(root);

            var duplicates = ComponentSelection.SelectDuplicateComponents(
                root.transform, new[] { onChild, onRoot });

            // ルート側があとから追加された場合でも、漏れた子側が削除対象になる
            Assert.That(duplicates, Is.EqualTo(new[] { onChild }));
        }

        [Test]
        public void SelectDuplicateComponents_ReturnsEmpty_WhenOnlyOneComponentExists()
        {
            var root = CreateAvatarRoot();
            var onRoot = AddComponentTo(root);

            Assert.That(
                ComponentSelection.SelectDuplicateComponents(root.transform, new[] { onRoot }),
                Is.Empty);
        }

        [Test]
        public void RemoveComponents_DestroysAllComponents()
        {
            var root = CreateAvatarRoot();
            var components = new[] { AddComponentTo(root), AddComponentTo(root, "Child") };

            ComponentSelection.RemoveComponents(components);

            Assert.That(root.GetComponentsInChildren<NativeFTManagerComponent>(true), Is.Empty);
        }

        [Test]
        public void CollectComponents_FindsInactiveChildren()
        {
            var root = CreateAvatarRoot();
            var onRoot = AddComponentTo(root);
            var onChild = AddComponentTo(root, "Child");
            onChild.gameObject.SetActive(false);

            Assert.That(
                ComponentSelection.CollectComponents(root),
                Is.EquivalentTo(new[] { onRoot, onChild }));
        }
    }
}
