using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Shared.Disturbance
{
    /// <summary>
    ///     World↔field-UV geometry for the disturbance RT: derives the field's world-space bounds and
    ///     texel dimensions from the camera framing, and converts world positions/radii into the
    ///     normalised UV space the stamp shaders expect.
    /// </summary>
    internal class DisturbanceFieldCoordinates
    {
        public Rect Bounds { get; }
        public int Width { get; }
        public int Height { get; }

        public DisturbanceFieldCoordinates(IGameDisplayConfiguration displayConfig, float texelsPerUnit)
        {
            var orthoSize = displayConfig.GetOrthogonalSize();
            var aspect = (float)Screen.width / Screen.height;
            var worldHeight = orthoSize * 2f;
            var worldWidth = worldHeight * aspect;

            Bounds = new Rect(-worldWidth * 0.5f, -worldHeight * 0.5f, worldWidth, worldHeight);
            Width = Mathf.Max(4, Mathf.RoundToInt(worldWidth * texelsPerUnit));
            Height = Mathf.Max(4, Mathf.RoundToInt(worldHeight * texelsPerUnit));
        }

        public Vector2 WorldToUV(Vector3 worldPos)
        {
            return new Vector2(
                (worldPos.x - Bounds.xMin) / Bounds.width,
                (worldPos.y - Bounds.yMin) / Bounds.height);
        }

        public float WorldRadiusToUV(float worldRadius)
        {
            var avgSize = (Bounds.width + Bounds.height) * 0.5f;
            return avgSize > 0.001f ? worldRadius / avgSize : 0.1f;
        }
    }
}
