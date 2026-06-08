using System;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    /// Reusable texture preview box for editor windows. Draws a HelpBox container
    /// with a toolbar (background toggle + custom actions) and a centred texture
    /// preview with selectable background (checkerboard, black, white).
    ///
    /// Usage:
    /// <code>
    /// // Create once (field on your window):
    /// private readonly TexturePreviewBox _preview = new("Branch Preview");
    ///
    /// // Draw each frame:
    /// _preview.Draw(rect, _myTexture, extraActions: DrawMyButtons);
    /// </code>
    /// </summary>
    internal sealed class TexturePreviewBox
    {
        internal enum BackgroundMode
        {
            Checkerboard = 0,
            Black = 1,
            White = 2
        }

        private static readonly string[] BgLabels = { "▦", "■", "□" };
        private const float ToolbarButtonWidth = 26f;
        private const float Padding = 6f;

        private readonly string _title;

        internal BackgroundMode Background { get; set; }

        internal TexturePreviewBox(string title)
        {
            _title = title;
        }

        /// <summary>
        /// Draws the full preview box at the given rect. Calls <paramref name="drawToolbarExtras"/>
        /// to let the caller add extra buttons to the toolbar. The callback receives the remaining
        /// rect to the left of the background button, and should return the updated xMax
        /// (i.e. how much space was consumed from the right).
        /// </summary>
        /// <param name="rect">The full rect for the preview box.</param>
        /// <param name="texture">The texture to display (may be null).</param>
        /// <param name="drawToolbarExtras">
        /// Optional callback to draw extra toolbar buttons. Receives the box rect and the
        /// current right-edge X of the next button slot. Return the updated right-edge X
        /// after drawing your buttons (draw right-to-left).
        /// </param>
        internal void Draw(Rect rect, Texture2D texture, Func<Rect, float, float> drawToolbarExtras = null)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            var toolbarY = rect.y + 2f;
            var rightEdge = rect.xMax - 4f;

            // Background mode button (rightmost)
            rightEdge -= ToolbarButtonWidth;
            var bgRect = new Rect(rightEdge, toolbarY, ToolbarButtonWidth, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(bgRect, BgLabels[(int)Background], EditorStyles.miniButton))
            {
                Background = (BackgroundMode)(((int)Background + 1) % 3);
            }

            // Extra toolbar buttons (drawn right-to-left by the caller)
            if (drawToolbarExtras != null)
            {
                rightEdge -= 2f;
                rightEdge = drawToolbarExtras(rect, rightEdge);
            }

            // Title label fills the remaining left space
            var labelRect = new Rect(rect.x + 4f, toolbarY, rightEdge - rect.x - 8f, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, _title, EditorStyles.centeredGreyMiniLabel);

            // Texture drawing area
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

        /// <summary>
        /// Helper to draw a standard toolbar button (right-to-left). Returns the updated right edge.
        /// </summary>
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

