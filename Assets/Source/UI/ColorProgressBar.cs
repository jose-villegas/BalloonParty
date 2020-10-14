using System.Collections.Generic;
using System.Linq;
using Entitas;
using UnityEngine;
using UnityEngine.UI;

public class ColorProgressBar : MonoBehaviour, IAnyGameLevelProgressListener, IAnyGameLevelListener
{
    [SerializeField] private Graphic[] _graphicsToSetColor;
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private ScoreNotice _notice;

    private List<ScoreNotice> _notices;
    private IBalloonColorConfiguration _colorConfiguration;
    private Contexts _contexts;
    private IGroup<GameEntity> _colorPopCount;

    public void Setup(IBalloonColorConfiguration colorConfiguration, IGameConfiguration gameConfiguration)
    {
        _contexts = Contexts.sharedInstance;
        _colorConfiguration = colorConfiguration;
        _colorPopCount = _contexts.game.GetGroup(GameMatcher.BalloonLastColorPopCount);

        foreach (var image in _graphicsToSetColor)
        {
            image.color = colorConfiguration.Color;
        }

        // setup with current values
        var progresses = _contexts.game.GetGroup(GameMatcher.GameLevelProgress);

        if (_contexts.game.hasGameLevel)
        {
            OnAnyGameLevel(_contexts.game.gameLevelEntity, _contexts.game.gameLevel.Value);
        }

        foreach (var progress in progresses)
        {
            if (progress.gameLevelProgress.Name == colorConfiguration.Name)
            {
                OnAnyGameLevelProgress(progress, progress.gameLevelProgress.Name, progress.gameLevelProgress.Current);
            }
        }

        // create listeners
        var colorProgress = _contexts.game.CreateEntity();
        colorProgress.AddAnyGameLevelListener(this);
        colorProgress.AddAnyGameLevelProgressListener(this);
    }

    public void OnAnyGameLevelProgress(GameEntity entity, string name, int current)
    {
        if (_colorConfiguration.Name == name)
        {
            _progressSlider.value = current;

            if (_notices == null)
            {
                _notices = new List<ScoreNotice>();
            }

            var usable = _notices.FindIndex(x => x.IsUsable);

            if (_notices.Count == 0 || usable < 0)
            {
                var newInstance = Instantiate(_notice, transform);
                _notices.Add(newInstance);
            }
            
            if (usable >= 0 && usable < _notices.Count)
            {
                var instance = _notices[usable];

                var colorPopCount = _colorPopCount.GetEntities()[0].balloonLastColorPopCount.Value;
                instance.ScoreUp(colorPopCount);
            }
        }
    }

    public void OnAnyGameLevel(GameEntity entity, int value)
    {
        var requirement = GameConfiguration.PointsRequiredForLevel(value + 1);

        _progressSlider.maxValue = requirement;
        _progressSlider.value = 0;
    }
}