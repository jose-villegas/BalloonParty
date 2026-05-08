using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class PowerUpConfiguration
{
    [SerializeField] private List<PowerUpSettings> _powerUps;

    public List<PowerUpSettings> PowerUps => _powerUps;

    public PowerUpSettings this[BalloonPowerUp type]
    {
        get { return _powerUps.First(x => x.Type == type); }
    }
}