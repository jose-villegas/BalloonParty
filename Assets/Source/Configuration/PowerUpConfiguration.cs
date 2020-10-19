using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PowerUpConfiguration
{
    [SerializeField]
    private List<PowerUpSettings> _powerUps;

    public List<PowerUpSettings> PowerUps => _powerUps;
}