using Entitas;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class ScoreCounterLabel : MonoBehaviour, IAnyGamePersistentScoreListener
{
    private Text _label;
    private Contexts _contexts;
    private IGameConfiguration _configuration;
    private IGroup<GameEntity> _scores;

    private void Awake()
    {
        _label = GetComponent<Text>();

    }

    private void Start()
    {
        _contexts = Contexts.sharedInstance;
        _configuration = _contexts.configuration.gameConfiguration.value;
        _scores = _contexts.game.GetGroup(GameMatcher.GamePersistentScore);
        _label.text = "0";

        // listening entity
        var e = _contexts.game.CreateEntity();
        e.AddAnyGamePersistentScoreListener(this);
    }

    public void OnAnyGamePersistentScore(GameEntity entity, string name, int score)
    {
        var sum = 0;

        foreach (var scoreHolder in _scores)
        {
            sum += scoreHolder.gamePersistentScore.Score;
        }

        _label.text = sum.ToString("N0");
    }
}