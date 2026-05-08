using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class LevelLabel : MonoBehaviour, IAnyGameLevelListener
{
    [SerializeField] private bool _nextLevel;

    private Text _label;
    private Contexts _contexts;

    private void Awake()
    {
        _label = GetComponent<Text>();
    }

    private void Start()
    {
        _contexts = Contexts.sharedInstance;

        var e = _contexts.game.CreateEntity();
        e.AddAnyGameLevelListener(this);

        if (_contexts.game.hasGameLevel)
        {
            OnAnyGameLevel(_contexts.game.gameLevelEntity, _contexts.game.gameLevel.Value);
        }
    }

    public void OnAnyGameLevel(GameEntity entity, int value)
    {
        _label.text = (value + (_nextLevel ? 1 : 0)).ToString("N0");
    }
}