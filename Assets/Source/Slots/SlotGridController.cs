using BalloonParty.Balloon.Spawner;
using UnityEngine;
using VContainer;

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
            for (var row = 0; row < _initialLines; row++)
            for (var col = 0; col < _grid.Columns; col++)
                _spawner.SpawnBalloon(_grid.RandomColorName(), new Vector2Int(col, row));
        }
    }
}