using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;

namespace BalloonParty.Slots
{
    public class SlotGridController : MonoBehaviour
    {
        [SerializeField] private GameObject _balloonPrefab;
        [SerializeField] private int _initialLines = 3;

        [Inject] private SlotGrid _grid;
        [Inject] private IObjectResolver _resolver;
        [Inject] private IGameConfiguration _config;

        private void Start()
        {
            PopulateGrid();
        }

        private void PopulateGrid()
        {
            if (_balloonPrefab == null) return;

            var colorIndex = 0;
            for (int row = 0; row < _initialLines; row++)
            {
                for (int col = 0; col < _grid.Columns; col++)
                {
                    var color = _config.BalloonColors[colorIndex % _config.BalloonColors.Length].Name;
                    SpawnBalloon(color, new Vector2Int(col, row));
                    colorIndex++;
                }
            }
        }

        private void SpawnBalloon(string color, Vector2Int slotIndex)
        {
            var position = _grid.IndexToWorldPosition(slotIndex);
            var instance = _resolver.Instantiate(_balloonPrefab, position, Quaternion.identity);
            var view = instance.GetComponent<BalloonView>();

            var model = new BalloonModel();
            model.Color.Value = color;

            var controller = new BalloonController(model, view);
            controller.Start();

            _grid.Place(model, slotIndex);
        }
    }
}
