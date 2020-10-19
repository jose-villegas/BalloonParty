using Entitas.CodeGeneration.Attributes;
using UnityEngine;

[Configuration, Unique, ComponentName("GameConfiguration")]
public interface IGameConfiguration
{
    Vector2 ThrowerSpawnPoint { get; }
    
    Vector2 ProjectileSpawnPoint { get; }
    
    float ProjectileSpeed { get; }
    
    Vector4 LimitsClockwise { get; }
    
    Vector2Int SlotsSize { get; }
    
    Vector2 SlotSeparation { get; }
    
    Vector2 SlotsOffset { get; }
    
    Vector2 BalloonSpawnAnimationDurationRange { get; }
    
    int GameStartedBalloonLines { get; }
    
    BalloonColorConfiguration[] BalloonColors { get; }
    
    float TimeForBalloonsBalance { get;}
    
    int NewProjectileBalloonLines { get; }
    
    float NewBalloonLinesTimeInterval { get; }
    
    float NudgeDistance { get;  }
    
    float NudgeDuration { get; }
    
    GameDisplayConfiguration DisplayConfiguration { get; }
    
    float ScorePointTraceDuration { get; }
    PowerUpConfiguration PowerUpConfiguration { get; }
    
    int PointsRequiredForLevel(int level);
    Color BalloonColor(string name);
}