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
    [SerializeField] private ScorePointTrail _scoreTrail;

    [SerializeField] private Animator _animator;
    [SerializeField] private ParticleSystem _completionParticleSystem;

    private List<ScoreNotice> _notices;
    private static List<ScorePointTrail> _trails;
    private IBalloonColorConfiguration _colorConfiguration;
    private Contexts _contexts;
    private IGameConfiguration _configuration;
    private int _currentCount = 0;

    public void Setup(IBalloonColorConfiguration colorConfiguration, IGameConfiguration gameConfiguration)
    {
        _contexts = Contexts.sharedInstance;
        _configuration = gameConfiguration;
        _colorConfiguration = colorConfiguration;

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

            if (entity.hasPosition && current <= _progressSlider.maxValue)
            {
                _currentCount += 1;
                PopScoreNotice();
                ShowScorePointTrail(entity);
            }

            if (current >= _progressSlider.maxValue)
            {
                _completionParticleSystem.gameObject.SetActive(true);
                _completionParticleSystem.Play();
                _animator.SetBool("Completed", true);
            }
        }
        else
        {
            _currentCount = 0;
        }
    }

    private void ShowScorePointTrail(GameEntity entity)
    {
        if (_trails == null)
        {
            _trails = new List<ScorePointTrail>();
        }

        var usable = _trails.FindIndex(x => x.IsUsable);
        ScorePointTrail trail = null;

        if (_trails.Count == 0 || usable < 0)
        {
            trail = Instantiate(_scoreTrail, entity.position.Value, Quaternion.identity);
            _trails.Add(trail);
        }

        if (usable >= 0 && usable < _trails.Count)
        {
            trail = _trails[usable];
            trail.transform.position = entity.position.Value;
            trail.transform.localScale = Vector3.one;
        }

        if (trail != null)
        {
            trail.Setup(transform.position, _colorConfiguration.Color, _configuration, () =>
            {
                _animator.SetTrigger("TrailHit");
            });
        }
    }

    private void PopScoreNotice()
    {
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
            instance.ScoreUp(_currentCount);
        }
    }

    public void OnAnyGameLevel(GameEntity entity, int value)
    {
        var requirement = GameConfiguration.PointsRequiredForLevel(value + 1);

        _progressSlider.maxValue = requirement;
        _progressSlider.value = 0;

        // reset vfx
        _completionParticleSystem.Stop();
        _completionParticleSystem.gameObject.SetActive(false);
        _animator.SetBool("Completed", false);
    }
}