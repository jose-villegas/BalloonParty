using UnityEngine;
using VContainer;
using BalloonParty.Balloon.Spawner;

namespace BalloonParty.Slots
{
    public class SlotGridController : MonoBehaviour
    {
        [SerializeField] private int _initialLines = 3;

        [Inject] private SlotGrid _grid;
        [Inject] private BalloonSpawner _spawner;

        private void Start()
        {
            PopulateGrid();
        }

        private void PopulateGrid()
        {
            for (int row = 0; row < _initialLines; row++)
            {
                for (int col = 0; col < _grid.Columns; col++)
                {
                    _spawner.SpawnBalloon(_grid.RandomColorName(), new Vector2Int(col, row));
                }
            }
        }
    }
}
