using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Tests.UI
{
    /// <summary>
    ///     Pins the shipped pencil font by loading the real font asset's material: a font rebuild that dropped
    ///     the shader or its textures would leave every heads-up in plain TMP text with no error.
    /// </summary>
    [TestFixture]
    public class PencilFontAssetTests
    {
        private const string FontAssetPath = "Assets/Fonts/PatrickHand-Regular SDF.asset";
        private const string PencilShaderName = "BalloonParty/Text/Pencil";

        private Material _material;

        [SetUp]
        public void SetUp()
        {
            // The font's material is a sub-asset; the tests assembly doesn't reference TMP, so find it by type.
            _material = AssetDatabase.LoadAllAssetsAtPath(FontAssetPath).OfType<Material>().FirstOrDefault();
            Assert.IsNotNull(_material, $"{FontAssetPath} should hold a material");
        }

        [Test]
        public void Material_UsesThePencilShader()
        {
            Assert.AreEqual(PencilShaderName, _material.shader.name);
        }

        [Test]
        public void Material_HasItsPencilNoise()
        {
            Assert.IsNotNull(_material.GetTexture("_PencilNoiseTex"), "_PencilNoiseTex should be assigned");
        }

        // WriteOnText puts its reveal in uv0.z only for a material with this marker; without it the font would
        // fall back to the vertex-alpha wipe and lose the ragged edge.
        [Test]
        public void Material_TakesTheRevealInUv()
        {
            Assert.IsTrue(_material.HasProperty("_PencilRevealInUv"));
        }
    }
}
