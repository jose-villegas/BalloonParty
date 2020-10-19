using UnityEngine;

public class ColorProgressBarInstancer : MonoBehaviour
{
    [SerializeField] private ColorProgressBar _colorProgressBarPrefab;

    private IGameConfiguration _configuration;
    private Contexts _contexts;

    private void Start()
    {
        _contexts = Contexts.sharedInstance;
        _configuration = _contexts.configuration.gameConfiguration.value;
        InstanceProgressBars();
    }

    private void InstanceProgressBars()
    {
        for (int i = 0; i < _configuration.BalloonColors.Length; i++)
        {
            var progress = Instantiate(_colorProgressBarPrefab, transform);
            progress.Setup(_configuration.BalloonColors[i]);
        }
    }
}