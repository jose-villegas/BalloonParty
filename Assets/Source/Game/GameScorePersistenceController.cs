using Entitas;
using UnityEngine;

public class GameScorePersistenceController : MonoBehaviour
{
    private Contexts _contexts;
    private IGameConfiguration _configuration;
    private IGroup<GameEntity> _scores;

    private void Start()
    {
        _contexts = Contexts.sharedInstance;
        _configuration = _contexts.configuration.gameConfiguration.value;

        // create score entries
        foreach (var configurationBalloonColor in _configuration.BalloonColors)
        {
            var currentScore = 0;

            if (PlayerPrefs.HasKey(configurationBalloonColor.Name))
            {
                currentScore = PlayerPrefs.GetInt(configurationBalloonColor.Name);
            }

            var e = _contexts.game.CreateEntity();
            e.AddGameScore(configurationBalloonColor.Name, currentScore);
        }

        _scores = _contexts.game.GetGroup(GameMatcher.GameScore);
    }

    private void OnApplicationPause(bool focus)
    {
        if (_scores == null || _scores.count == 0) return;

        foreach (var score in _scores)
        {
            var gameScore = score.gameScore;

            PlayerPrefs.SetInt(gameScore.Name, gameScore.Score);
        }

        PlayerPrefs.Save();
    }
}