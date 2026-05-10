using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [Serializable]
    public class PowerUpConfiguration
    {
        [SerializeField] private List<PowerUpSettings> _powerUps;

        public List<PowerUpSettings> PowerUps => _powerUps;

        public PowerUpSettings this[BalloonPowerUp type] => _powerUps.First(x => x.Type == type);
    }
}
