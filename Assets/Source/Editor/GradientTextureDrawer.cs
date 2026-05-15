using System;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Replaces the default texture slot for any 2D shader property with
    ///     Unity's native gradient picker. On every committed change the gradient
    ///     is baked to a 128×1 <see cref="Texture2D" /> sub-asset embedded in the
    ///     material file and assigned to the property automatically.
    /// </summary>
    /// <remarks>
    ///     Usage in ShaderLab:
    ///     <code>[GradientTexture] _MyTex ("Label", 2D) = "white" {}</code>
    ///     The gradient definition is stored in <c>AssetImporter.userData</c> (the
    ///     material's <c>.meta</c> file) so it persists across domain reloads and
    ///     version control. Multiple <c>[GradientTexture]</c> properties on the same
    ///     material are keyed separately by property name.
    /// </remarks>
    public class GradientTextureDrawer : MaterialPropertyDrawer
    {
        private const int BakeResolution = 128;
        private const string TagPrefix = "[GradientTexture:";
        private const string BakedNamePrefix = "_GradientBaked_";


        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(
            Rect position,
            MaterialProperty prop,
            GUIContent label,
            MaterialEditor editor)
        {
            var material = (Material)editor.target;
            var gradient = LoadGradient(material, prop.name);

            EditorGUI.BeginChangeCheck();
            var updated = EditorGUI.GradientField(position, label, gradient);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material, "Change Gradient " + prop.name);
                SaveGradient(material, prop.name, updated);
                BakeAndApply(material, prop, updated);
            }

            // First open — no texture assigned yet; bake the default.
            if (prop.textureValue == null)
            {
                BakeAndApply(material, prop, gradient);
            }
        }


        private static void BakeAndApply(Material material, MaterialProperty prop, Gradient gradient)
        {
            var tex = GetOrCreateBakedTexture(material, prop.name);
            var pixels = new Color[BakeResolution];

            for (var i = 0; i < BakeResolution; i++)
            {
                pixels[i] = gradient.Evaluate(i / (float)(BakeResolution - 1));
            }

            tex.SetPixels(pixels);
            tex.Apply();

            prop.textureValue = tex;
            EditorUtility.SetDirty(material);

            var path = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        private static Texture2D GetOrCreateBakedTexture(Material material, string propName)
        {
            var texName = BakedNamePrefix + propName;
            var path = AssetDatabase.GetAssetPath(material);

            if (!string.IsNullOrEmpty(path))
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Texture2D tex && asset.name == texName)
                    {
                        return tex;
                    }
                }
            }

            var newTex = new Texture2D(BakeResolution, 1, TextureFormat.RGBA32, false)
            {
                name = texName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.AddObjectToAsset(newTex, material);
            }

            return newTex;
        }


        private static Gradient LoadGradient(Material material, string propName)
        {
            var userData = ReadUserData(material);
            var json = ExtractBlock(userData, propName);

            if (string.IsNullOrEmpty(json))
            {
                return DefaultGradient();
            }

            try
            {
                var storage = JsonUtility.FromJson<GradientStorage>(json);
                return storage?.ToGradient() ?? DefaultGradient();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"GradientTextureDrawer.LoadGradient: failed to deserialize gradient " +
                    $"for \"{propName}\" — {e.Message}. Using default gradient.");
                return DefaultGradient();
            }
        }

        private static void SaveGradient(Material material, string propName, Gradient gradient)
        {
            var json = JsonUtility.ToJson(GradientStorage.From(gradient));
            var userData = ReadUserData(material);
            WriteUserData(material, ReplaceBlock(userData, propName, json));
        }


        private static string ExtractBlock(string userData, string propName)
        {
            var tag = TagPrefix + propName + "]";
            var start = userData.IndexOf(tag, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            var jsonStart = userData.IndexOf('{', start + tag.Length);
            if (jsonStart < 0)
            {
                return null;
            }

            var depth = 0;
            for (var i = jsonStart; i < userData.Length; i++)
            {
                if (userData[i] == '{')
                {
                    depth++;
                }
                else if (userData[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return userData.Substring(jsonStart, i - jsonStart + 1);
                    }
                }
            }

            return null;
        }

        private static string ReplaceBlock(string userData, string propName, string json)
        {
            var tag = TagPrefix + propName + "]";
            var existing = ExtractBlock(userData, propName);

            if (existing != null)
            {
                var full = tag + existing;
                var index = userData.IndexOf(full, StringComparison.Ordinal);
                if (index >= 0)
                {
                    return userData.Remove(index, full.Length).Insert(index, tag + json);
                }
            }

            return userData + tag + json;
        }


        private static string ReadUserData(Material material)
        {
            var importer = GetImporter(material);
            return importer?.userData ?? string.Empty;
        }

        private static void WriteUserData(Material material, string data)
        {
            var importer = GetImporter(material);
            if (importer == null)
            {
                Debug.LogWarning(
                    $"GradientTextureDrawer.WriteUserData: cannot persist gradient for " +
                    $"\"{material.name}\" — material has no asset importer (unsaved asset?).");
                return;
            }

            importer.userData = data;
            importer.SaveAndReimport();
        }

        private static AssetImporter GetImporter(Material material)
        {
            var path = AssetDatabase.GetAssetPath(material);
            return string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path);
        }

        /// <summary>
        ///     Plain-struct wrapper because JsonUtility cannot serialise
        ///     Gradient as a root object directly.
        /// </summary>
        [Serializable]
        private class GradientStorage
        {
            public ColorKeyEntry[] colorKeys = Array.Empty<ColorKeyEntry>();
            public AlphaKeyEntry[] alphaKeys = Array.Empty<AlphaKeyEntry>();
            public int mode;

            [Serializable]
            public class ColorKeyEntry
            {
                public float r, g, b, time;
            }

            [Serializable]
            public class AlphaKeyEntry
            {
                public float a, time;
            }

            public static GradientStorage From(Gradient gradient)
            {
                var s = new GradientStorage { mode = (int)gradient.mode };
                var ck = gradient.colorKeys;
                s.colorKeys = new ColorKeyEntry[ck.Length];
                for (var i = 0; i < ck.Length; i++)
                {
                    s.colorKeys[i] = new ColorKeyEntry
                    {
                        r = ck[i].color.r,
                        g = ck[i].color.g,
                        b = ck[i].color.b,
                        time = ck[i].time
                    };
                }

                var ak = gradient.alphaKeys;
                s.alphaKeys = new AlphaKeyEntry[ak.Length];
                for (var i = 0; i < ak.Length; i++)
                {
                    s.alphaKeys[i] = new AlphaKeyEntry { a = ak[i].alpha, time = ak[i].time };
                }

                return s;
            }

            public Gradient ToGradient()
            {
                var g = new Gradient { mode = (GradientMode)mode };

                var ck = colorKeys ?? Array.Empty<ColorKeyEntry>();
                var outCk = new GradientColorKey[ck.Length];
                for (var i = 0; i < ck.Length; i++)
                {
                    outCk[i] = new GradientColorKey(new Color(ck[i].r, ck[i].g, ck[i].b), ck[i].time);
                }

                var ak = alphaKeys ?? Array.Empty<AlphaKeyEntry>();
                var outAk = new GradientAlphaKey[ak.Length];
                for (var i = 0; i < ak.Length; i++)
                {
                    outAk[i] = new GradientAlphaKey(ak[i].a, ak[i].time);
                }

                g.colorKeys = outCk;
                g.alphaKeys = outAk;
                return g;
            }
        }

        private static Gradient DefaultGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.35f, 0.35f, 0.38f), 0f),
                    new GradientColorKey(new Color(0.55f, 0.55f, 0.60f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }
}
