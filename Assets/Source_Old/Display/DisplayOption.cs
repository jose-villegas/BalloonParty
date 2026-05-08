using System;
using UnityEngine;

[Serializable]
public class DisplayOption
{
    [SerializeField] private Vector2 _aspectRatio;
    [SerializeField] private float _orthogonalSize;

    public Vector2 Ratio => _aspectRatio;

    public float OrthogonalSize => _orthogonalSize;

    public float Aspect => _aspectRatio.x / _aspectRatio.y;
}