using System;
using UnityEngine;

[Serializable]
public class PowerUpSettings
{
    public BalloonPowerUp Type;
    public int TurnCheckEvery;
    [Range(0f, 100f)]
    public float Probability;
    public int MaximumAllowed;
}