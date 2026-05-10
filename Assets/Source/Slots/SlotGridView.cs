#region

using UnityEngine;
using VContainer;

#endregion

namespace BalloonParty.Slots
{
    public class SlotGridView : MonoBehaviour
    {
        [Inject] private SlotGrid _grid;

        private void OnDrawGizmos()
        {
            if (_grid == null)
            {
                return;
            }

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var position = _grid.IndexToWorldPosition(new Vector2Int(col, row));
                    Gizmos.color = _grid.IsEmpty(col, row)
                        ? new Color(1f, 1f, 1f, 0.2f)
                        : new Color(0f, 1f, 0f, 0.4f);
                    Gizmos.DrawWireSphere(position, 0.2f);
                }
            }
        }
    }
}
