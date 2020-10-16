public class GameUpdateSystems : Feature
{
    public GameUpdateSystems(Contexts contexts)
    {
        // initialization
        Add(new SloIndexerSystem(contexts));
        Add(new GameStartedThrowerSpawnSystem(contexts));
        Add(new GameStartedBalloonsSpawnSystem(contexts));
        Add(new AssetInstancingSystem(contexts));
        Add(new ProjectileReloadSystem(contexts));
        Add(new BalloonLineSpawnerSystem(contexts));
        Add(new ProjectileShieldSystem(contexts));
        Add(new BalloonHitScoreSystem(contexts));
        Add(new BalloonHitNudgeAnimationSystem(contexts));
        Add(new BalloonHitDestructionSystem(contexts));

        // movement
        Add(new ThrowerDirectionSystem(contexts));
        Add(new ThrowerRotationSystem(contexts));
        Add(new ProjectileTransformSystem(contexts));
        Add(new ThrowLoadedProjectileSystem(contexts));
        Add(new FreeProjectileMovementSystem(contexts));
        Add(new BalanceBalloonsSystem(contexts));
        Add(new ProjectileBounceSystem(contexts));

        // events
        Add(new GameEventSystems(contexts));
    }
}