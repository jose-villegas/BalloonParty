using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(LinkedViewController))]
public class BalloonPowerUpDisplayController : MonoBehaviour, IBalloonPowerUpListener, IBalloonHitListener
{
    [SerializeField] private List<PowerUpControllerReference> _powerUps;

    private LinkedViewController _linkedView;
    private IGameConfiguration _configuration;
    private PowerUpControllerReference _powerUpController;
    private bool _activated;

    [Serializable]
    private class PowerUpControllerReference
    {
        public BalloonPowerUp Type;
        public BalloonPowerUpController Reference;
    }

    private void Awake()
    {
        _configuration = Contexts.sharedInstance.configuration.gameConfiguration.value;
        _linkedView = GetComponent<LinkedViewController>();
        _linkedView.OnViewLinked += OnViewLinked;
    }

    private void OnViewLinked(GameEntity gameEntity)
    {
        gameEntity.AddBalloonPowerUpListener(this);
        gameEntity.AddBalloonHitListener(this);
    }

    public void OnBalloonPowerUp(GameEntity entity, BalloonPowerUp value)
    {
        foreach (var powerUp in _powerUps)
        {
            powerUp.Reference.gameObject.SetActive(powerUp.Type == value);

            if (powerUp.Type == value)
            {
                _powerUpController = powerUp;
                _powerUpController.Reference.Setup(_configuration.BalloonColors
                    .First(x => x.Name == entity.balloonColor.Value), entity);

                return;
            }
        }
    }

    public void OnBalloonHit(GameEntity entity)
    {
        if (_activated) return;
        
        _activated = true;
        _powerUpController.Reference.Activate();
    }
}