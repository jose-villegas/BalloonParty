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
        [Inject] private IGameConfiguration _config;

        private void Start()
        {
            PopulateGrid();
        }

        private void PopulateGrid()
        {
            var colorIndex = 0;
            for (int row = 0; row < _initialLines; row++)
            {
                for (int col = 0; col < _grid.Columns; col++)
                {
                    var color = _config.BalloonColors[colorIndex % _config.BalloonColors.Length].Name;
                    _spawner.SpawnBalloon(color, new Vector2Int(col, row));
                    colorIndex++;
                }
            }
        }
    }
}
