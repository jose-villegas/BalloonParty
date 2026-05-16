using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using UnityEngine;
using VContainer;

namespace BalloonParty.Slots
{
    public class SlotGridView : MonoBehaviour
    {
#if UNITY_EDITOR
        private static readonly Color LimitsOutlineColor = new(1f, 0.6f, 0.2f, 0.9f);
        private static readonly Color LimitsFillColor = new(1f, 0.6f, 0.2f, 0.06f);
        private static readonly Color EmptySlotColor = new(1f, 1f, 1f, 0.2f);
        private static readonly Color OccupiedSlotColor = new(0f, 1f, 0f, 0.4f);

        [Inject] private SlotGrid _grid;
        [Inject] private IGameConfiguration _config;
        [Inject] private BalloonsConfiguration _balloonsConfig;

        private float _cachedRadius;

        private void OnDrawGizmos()
        {
            if (_config != null)
            {
                var limits = _config.LimitsClockwise;
                GizmoDrawingHelper.DrawWorldRectFromLimits(
                    limits.x, limits.y, limits.z, limits.w,
                    LimitsOutlineColor, LimitsFillColor);
            }

            if (_grid == null)
            {
                return;
            }

            if (_cachedRadius <= 0f)
            {
                _cachedRadius = ResolveBalloonRadius();
            }

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var position = _grid.IndexToWorldPosition(new Vector2Int(col, row));
                    var color = _grid.IsEmpty(col, row) ? EmptySlotColor : OccupiedSlotColor;
                    GizmoDrawingHelper.DrawWireSphere(position, _cachedRadius, color);
                }
            }
        }

        private float ResolveBalloonRadius()
        {
            if (_balloonsConfig != null && _balloonsConfig.Entries.Length > 0)
            {
                var prefab = _balloonsConfig.Entries[0].Prefab;
                if (prefab != null)
                {
                    var circleCollider = prefab.GetComponent<CircleCollider2D>();
                    if (circleCollider != null)
                    {
                        return circleCollider.radius * prefab.transform.localScale.x;
                    }
                }
            }

            return 0.2f;
        }
#endif
    }
}
