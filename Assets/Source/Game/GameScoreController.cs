using Entitas;
using UnityEngine;

public class GameScoreController : MonoBehaviour
{
    private Contexts _contexts;
    private IGameConfiguration _configuration;
    private IGroup<GameEntity> _scores;
    private IGroup<GameEntity> _progressScores;

    private void Start()
    {
        _contexts = Contexts.sharedInstance;
        _configuration = _contexts.configuration.gameConfiguration.value;
        _scores = _contexts.game.GetGroup(GameMatcher.GamePersistentScore);
        _progressScores = _contexts.game.GetGroup(GameMatcher.GameLevelProgress);

        // create score entries
        foreach (var configurationBalloonColor in _configuration.BalloonColors)
        {
            // extract persistent score
            var currentScore = 0;

            if (PlayerPrefs.HasKey(configurationBalloonColor.Name))
            {
                currentScore = PlayerPrefs.GetInt(configurationBalloonColor.Name);
            }

            var persistent = _contexts.game.CreateEntity();
            persistent.AddGamePersistentScore(configurationBalloonColor.Name, currentScore);

            // extract last level progress
            var currentProgress = 0;

            if (PlayerPrefs.HasKey(configurationBalloonColor.Name + Constants.ProgressSuffix))
            {
                currentProgress = PlayerPrefs.GetInt(configurationBalloonColor.Name + Constants.ProgressSuffix);
            }

            var progress = _contexts.game.CreateEntity();
            progress.AddGameLevelProgress(configurationBalloonColor.Name, currentProgress);
        }

        // obtain current player level
        var currentLevel = 0;

        if (PlayerPrefs.HasKey(Constants.Level))
        {
            currentLevel = PlayerPrefs.GetInt(Constants.Level);
        }

        var level = _contexts.game.CreateEntity();
        level.AddGameLevel(currentLevel);
    }

    private void OnApplicationPause(bool focus)
    {
        if (_scores == null || _scores.count == 0) return;

        foreach (var score in _scores)
        {
            var gameScore = score.gamePersistentScore;

            PlayerPrefs.SetInt(gameScore.Name, gameScore.Score);
        }

        foreach (var score in _progressScores)
        {
            var progress = score.gameLevelProgress;

            PlayerPrefs.SetInt(progress.Name + Constants.ProgressSuffix, progress.Current);
        }

        PlayerPrefs.SetInt(Constants.Level, _contexts.game.gameLevel.Value);
        PlayerPrefs.Save();
    }
}