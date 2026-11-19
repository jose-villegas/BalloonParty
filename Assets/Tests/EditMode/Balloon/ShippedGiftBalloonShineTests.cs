using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The golden balloon, the disco ball and the nuke draw their own highlight in the body's shader, where it
    ///     breaks along the plates, the mirrors and the dents. Their prefabs are variants of the plain balloon's,
    ///     which lays two smooth shine sprites over the body: left on, those hide the relief, and on the nuke the
    ///     counter when the light comes from below. These read the shipped prefabs, so a variant that gets the
    ///     sprites back fails here.
    /// </summary>
    [TestFixture]
    public class ShippedGiftBalloonShineTests
    {
        private const string Folder = "Assets/Prefabs/Balloon/";
        private const string ShinePivot = "ShinePivot";
        private const string LayerList = "_spriteLayerRenderers";

        [TestCase("Golden")]
        [TestCase("Disco")]
        [TestCase("Nuke")]
        public void GiftBalloon_DrawsNoSmoothShineOverItsBody(string name)
        {
            var pivot = Load(name).transform.Find(ShinePivot);

            Assert.IsNotNull(pivot, $"{name} keeps the base balloon's {ShinePivot}");
            foreach (Transform shine in pivot)
            {
                Assert.IsFalse(shine.gameObject.activeSelf, $"{name}'s {shine.name} would lie over the body's relief");
            }
        }

        // The view enables, hides and sorts every renderer in its layer list, so a switched-off one has no place there.
        [TestCase("Golden")]
        [TestCase("Disco")]
        [TestCase("Nuke")]
        public void GiftBalloon_ListsNoSwitchedOffRendererAsALayer(string name)
        {
            var prefab = Load(name);
            var layers = LayersOf(prefab);

            Assert.Greater(layers.arraySize, 0, $"{name} lists its sprite layers");
            for (var i = 0; i < layers.arraySize; i++)
            {
                var renderer = layers.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                Assert.IsNotNull(renderer, $"{name}'s layer {i} is a renderer");
                Assert.IsFalse(
                    IsUnder(renderer.transform, ShinePivot),
                    $"{name}'s layer {i} ({renderer.name}) is a switched-off shine");
            }
        }

        [TestCase("Balloon")]
        [TestCase("ToughBalloon")]
        [TestCase("Unbreakable")]
        public void OtherRoundBalloon_KeepsItsShine(string name)
        {
            var pivot = Load(name).transform.Find(ShinePivot);

            Assert.IsNotNull(pivot, $"{name} has a {ShinePivot}");
            Assert.Greater(pivot.childCount, 0, $"{name} has shine sprites");
            foreach (Transform shine in pivot)
            {
                Assert.IsTrue(shine.gameObject.activeSelf, $"{name}'s {shine.name} is on");
            }
        }

        [Test]
        public void Heart_KeepsItsOwnShine()
        {
            var shine = Load("Heart").transform.Find("Ballon/Shine");

            Assert.IsNotNull(shine, "the heart has its Shine child");
            Assert.IsTrue(shine.gameObject.activeSelf, "the heart's shine is on");
            Assert.IsTrue(shine.GetComponent<SpriteRenderer>().enabled, "the heart's shine draws");
        }

        private static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            Assert.IsNotNull(prefab, $"{Folder}{name}.prefab should load");
            return prefab;
        }

        private static SerializedProperty LayersOf(GameObject prefab)
        {
            foreach (var component in prefab.GetComponents<MonoBehaviour>())
            {
                var layers = new SerializedObject(component).FindProperty(LayerList);
                if (layers != null)
                {
                    return layers;
                }
            }

            Assert.Fail($"{prefab.name} has no component with {LayerList}");
            return null;
        }

        private static bool IsUnder(Transform transform, string ancestor)
        {
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                if (parent.name == ancestor)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
