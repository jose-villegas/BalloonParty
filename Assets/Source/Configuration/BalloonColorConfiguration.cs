using System;
using UnityEngine;

[Serializable]
public class BalloonColorConfiguration : IBalloonColorConfiguration
{
    [SerializeField] private string _name;
    [SerializeField] private Color _color;

    public string Name => _name;

    public Color Color => _color;
}