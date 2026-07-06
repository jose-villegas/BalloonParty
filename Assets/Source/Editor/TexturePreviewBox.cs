using System;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>Reusable texture preview box: toolbar + centred texture with selectable background.</summary>
    internal sealed class TexturePreviewBox
    {
        internal enum BackgroundMode
        {
            Checkerboard = 0,
            Black = 1,
            White = 2
        }

        private const float ToolbarButtonWidth = 26f;
        private const float Padding = 6f;

        private static readonly string[] BgLabels = { "▦", "■", "□" };

        private readonly string _title;

        internal BackgroundMode Background { get; set; }

        internal TexturePreviewBox(string title)
        {
            _title = title;
        }

        /// <summary>Draws the preview box; <paramref name="drawToolbarExtras"/> draws right-to-left and returns the updated right edge.</summary>
        internal void Draw(Rect rect, Texture2D texture, Func<Rect, float, float> drawToolbarExtras = null)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            var toolbarY = rect.y + 2f;
            var rightEdge = rect.xMax - 4f;

            rightEdge -= ToolbarButtonWidth;
            var bgRect = new Rect(rightEdge, toolbarY, ToolbarButtonWidth, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(bgRect, BgLabels[(int)Background], EditorStyles.miniButton))
            {
                Background = (BackgroundMode)(((int)Background + 1) % 3);
            }

            if (drawToolbarExtras != null)
            {
                rightEdge -= 2f;
                rightEdge = drawToolbarExtras(rect, rightEdge);
            }

            var labelRect = new Rect(rect.x + 4f, toolbarY, rightEdge - rect.x - 8f, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, _title, EditorStyles.centeredGreyMiniLabel);

            if (texture != null)
            {
                var inner = new Rect(
                    rect.x + Padding,
                    rect.y + EditorGUIUtility.singleLineHeight + Padding,
                    rect.width - Padding * 2f,
                    rect.height - EditorGUIUtility.singleLineHeight - Padding * 2f);

                var size = Mathf.Min(inner.width, inner.height);
                var centred = new Rect(
                    inner.x + (inner.width - size) * 0.5f,
                    inner.y + (inner.height - size) * 0.5f,
                    size, size);

                DrawTextureWithBackground(centred, texture);
            }
        }

        private void DrawTextureWithBackground(Rect rect, Texture2D texture)
        {
            switch (Background)
            {
                case BackgroundMode.Checkerboard:
                    EditorGUI.DrawTextureTransparent(rect, texture, ScaleMode.ScaleToFit);
                    break;
                case BackgroundMode.Black:
                    EditorGUI.DrawRect(rect, Color.black);
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                    break;
                case BackgroundMode.White:
                    EditorGUI.DrawRect(rect, Color.white);
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                    break;
            }
        }

        /// <summary>Draws a toolbar button; returns the updated right edge.</summary>
        internal static float DrawToolbarButton(
            float rightEdge, float y, string label, float width, Action onClick)
        {
            rightEdge -= width;
            var btnRect = new Rect(rightEdge, y, width, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(btnRect, label, EditorStyles.miniButton))
            {
                onClick?.Invoke();
            }

            return rightEdge - 2f;
        }
    }
}
