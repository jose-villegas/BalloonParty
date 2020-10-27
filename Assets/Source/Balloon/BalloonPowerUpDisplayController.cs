using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(LinkedViewController))]
public class BalloonPowerUpDisplayController : MonoBehaviour, IBalloonPowerUpListener, IBalloonHitListener
{
    [SerializeField] private List<DisplayPowerUp> _powerUps;

    private LinkedViewController _linkedView;
    private IGameConfiguration _configuration;

    [Serializable]
    private class DisplayPowerUp
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
        foreach (var displayPowerUp in _powerUps)
        {
            displayPowerUp.Reference.gameObject.SetActive(displayPowerUp.Type == value);

            if (displayPowerUp.Type == value)
            {
                displayPowerUp.Reference.Setup(_configuration.BalloonColors
                    .First(x => x.Name == entity.balloonColor.Value), entity);
            }
        }
    }

    public void OnBalloonHit(GameEntity entity)
    {
        foreach (var displayPowerUp in _powerUps)
        {
            var value = entity.balloonPowerUp.Value;
            displayPowerUp.Reference.gameObject.SetActive(displayPowerUp.Type == value);

            if (displayPowerUp.Type == value)
            {
                displayPowerUp.Reference.Activate();
            }
        }
    }
}