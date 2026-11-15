using System;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.ClothRibbon
{
    /// <summary>
    ///     Writes a line on both faces of a <see cref="RibbonCloth" />'s ribbon, each glyph corner carried on the cloth
    ///     where it sits, so the writing rides the folds. The back face's copy is laid along the ribbon mirrored: seen
    ///     from behind, once the ribbon has swung round, it reads the right way. Each glyph shows on the face turned to
    ///     the screen where it sits and fades out as that part of the ribbon turns edge on. Both texts are laid out flat
    ///     first (<see cref="Layout" />), centred on this object; the cloth is simulated in their local space.
    /// </summary>
    public class ClothRibbonWriting : MonoBehaviour
    {
        [SerializeField] private TMP_Text _front;

        [Tooltip("The same line as the front, written on the ribbon's back.")]
        [SerializeField] private TMP_Text _back;

        [Tooltip("Room left and right of the writing, inside the notches, in canvas units.")]
        [SerializeField] [Min(0f)] private float _padding = 6f;

        [Tooltip("How far a face must turn toward the screen before its writing shows in full, as the share of its " +
                 "normal pointing out of the screen.")]
        [SerializeField] [Range(0.01f, 1f)] private float _fade = 0.25f;

        private readonly Face _frontFace = new(false);
        private readonly Face _backFace = new(true);

        private float _length;
        private float _height;

        /// <summary>
        ///     Lays both copies of the line out flat on a ribbon <paramref name="height" /> tall and returns the length
        ///     the ribbon needs to carry it.
        /// </summary>
        internal float Layout(float height)
        {
            _back.SetText(_front.text);
            _front.ForceMeshUpdate();
            _height = height;
            _length = _front.preferredWidth + 2f * _padding + height;
            foreach (var text in new[] { _front, _back })
            {
                var rect = text.rectTransform;
                rect.sizeDelta = new Vector2(_length, height);
                rect.anchoredPosition = Vector2.zero;
                text.ForceMeshUpdate();
            }

            _frontFace.Capture(_front);
            _backFace.Capture(_back);
            return _length;
        }

        /// <summary>Carries both copies of the line on <paramref name="cloth" /> as it is now.</summary>
        internal void Bend(RibbonCloth cloth)
        {
            _frontFace.Bend(_front, cloth, _length, _height, _fade);
            _backFace.Bend(_back, cloth, _length, _height, _fade);
        }

        /// <summary>
        ///     Where a point <paramref name="x" /> from the middle of the flat writing lands along a ribbon
        ///     <paramref name="length" /> long, 0 head to 1 tail; the back's copy runs the other way.
        /// </summary>
        internal static float AlongFor(float x, float length, bool back)
        {
            var along = length > 0f ? x / length + 0.5f : 0.5f;
            return back ? 1f - along : along;
        }

        /// <summary>How much of a glyph shows where the cloth's front faces the screen by <paramref name="facing" />.</summary>
        internal static float ShownFor(float facing, bool back, float fade)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((back ? -facing : facing) / fade));
        }

        // One face's writing: its glyphs as laid out flat, carried onto the cloth each frame.
        private sealed class Face
        {
            private readonly bool _back;

            private Vector3[][] _vertices = Array.Empty<Vector3[]>();
            private Color32[][] _colors = Array.Empty<Color32[]>();

            internal Face(bool back)
            {
                _back = back;
            }

            internal void Capture(TMP_Text text)
            {
                var meshes = text.textInfo.meshInfo;
                _vertices = new Vector3[meshes.Length][];
                _colors = new Color32[meshes.Length][];
                for (var m = 0; m < meshes.Length; m++)
                {
                    _vertices[m] = (Vector3[])(meshes[m].vertices?.Clone() ?? Array.Empty<Vector3>());
                    _colors[m] = (Color32[])(meshes[m].colors32?.Clone() ?? Array.Empty<Color32>());
                }
            }

            internal void Bend(TMP_Text text, RibbonCloth cloth, float length, float height, float fade)
            {
                var info = text.textInfo;
                var half = height * 0.5f;
                for (var i = 0; i < info.characterCount; i++)
                {
                    ref var character = ref info.characterInfo[i];
                    var mesh = character.materialReferenceIndex;
                    var first = character.vertexIndex;
                    if (!character.isVisible || mesh >= _vertices.Length || first + 3 >= _vertices[mesh].Length)
                    {
                        continue;
                    }

                    var source = _vertices[mesh];
                    var middle = (source[first].x + source[first + 2].x) * 0.5f;
                    var shown = ShownFor(cloth.Facing(AlongFor(middle, length, _back)), _back, fade);
                    var vertices = info.meshInfo[mesh].vertices;
                    var colors = info.meshInfo[mesh].colors32;
                    for (var corner = 0; corner < 4; corner++)
                    {
                        var index = first + corner;
                        var flat = source[index];
                        var point = cloth.Surface(AlongFor(flat.x, length, _back), half > 0f ? flat.y / half : 0f);
                        vertices[index] = new Vector3(point.x, point.y, 0f);
                        if (index < _colors[mesh].Length && index < colors.Length)
                        {
                            var tint = _colors[mesh][index];
                            tint.a = (byte)(tint.a * shown);
                            colors[index] = tint;
                        }
                    }
                }

                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            }
        }
    }
}
