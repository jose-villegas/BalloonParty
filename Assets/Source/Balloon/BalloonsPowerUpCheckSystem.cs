using System.Collections.Generic;
using System.Linq;
using Entitas;
using UnityEngine;

public class BalloonsPowerUpCheckSystem : ReactiveSystem<GameEntity>
{
    private readonly Contexts _contexts;
    private readonly IGroup<GameEntity> _newBalloons;
    private readonly IGroup<GameEntity> _balloonsPowerUp;
    private readonly IGameConfiguration _configuration;

    public BalloonsPowerUpCheckSystem(Contexts contexts) : base(contexts.game)
    {
        _contexts = contexts;
        _configuration = contexts.configuration.gameConfiguration.value;
        _newBalloons = _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.Balloon, GameMatcher.NewBalloon));
        _balloonsPowerUp = _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.Balloon, GameMatcher.BalloonPowerUp));
    }

    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.BalloonsPowerUpCheckEvent);
    }

    protected override bool Filter(GameEntity entity)
    {
        return entity.isBalloonsPowerUpCheckEvent;
    }

    protected override void Execute(List<GameEntity> entities)
    {
        var turns = _contexts.game.gameTurnCounter.Value;

        // filter by turn
        var availablePowerUps = _configuration.PowerUpConfiguration.PowerUps
            .Where(x => turns % x.TurnCheckEvery == 0);

        // filter my maximum cap
        availablePowerUps = availablePowerUps
            .Where(power =>
            {
                var currentActive = _balloonsPowerUp == null || _balloonsPowerUp.count <= 0
                    ? 0
                    : _balloonsPowerUp.AsEnumerable().Count(x => x.balloonPowerUp.Value == power.Type);

                return currentActive < power.MaximumAllowed;
            });

        var powerUpSettings = availablePowerUps as PowerUpSettings[];

        if (powerUpSettings == null)
        {
            powerUpSettings = availablePowerUps.ToArray();
        }

        if (powerUpSettings.Any())
        {
            var sumOfProbabilities = powerUpSettings.Sum(x => x.Probability);
            var probabilityCheck = Random.Range(0f, sumOfProbabilities);
            var shift = 0f;
            
            // check for which power up is going to be activated
            foreach (var powerUpSetting in powerUpSettings)
            {
                if (probabilityCheck <= powerUpSetting.Probability + shift)
                {
                    // only different of none are added as power ups
                    if (powerUpSetting.Type != BalloonPowerUp.None)
                    {
                        var indexOf = Random.Range(0, _newBalloons.count);
                        var balloon = _newBalloons.GetEntities()[indexOf];

                        balloon.ReplaceBalloonPowerUp(powerUpSetting.Type);
                    }

                    break;
                }

                shift += powerUpSetting.Probability;
            }
        }
    }
}