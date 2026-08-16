using BalloonParty.Shared.Rendering;
using UnityEngine;
using VContainer;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Scene-view overlay: the bounding sphere of every in-flight BigScore formation, drawn as a flat
    ///     circle (the board is 2D) at its current world center and radius — <see cref="ShapeFormationTicker"/>
    ///     already tracks both per formation, so this is a direct read, no extra bookkeeping.
    /// </summary>
    public class BigScoreFormationGizmos : MonoBehaviour
    {
#if UNITY_EDITOR
        private static readonly Color BoundsColor = new(1f, 0.9f, 0.2f, 0.8f);

        [SerializeField] private bool _showBounds = true;

        [Inject] private ShapeFormationTicker _ticker;

        private void OnDrawGizmos()
        {
            if (!_showBounds || _ticker == null)
            {
                return;
            }

            _ticker.ForEachActiveBounds(DrawBounds);
        }

        private static void DrawBounds(Vector3 center, float radius)
        {
            GizmoDrawingHelper.DrawWorldCircle(center, radius, BoundsColor);
        }
#endif
    }
}
