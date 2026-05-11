// DEPRECATED - commented out during migration. See MIGRATION_PLAN.md.
// using System;
// 
// // Legacy ECS fixed update loop — being replaced by VContainer entry points. See MIGRATION_PLAN.md.
// [Obsolete("Replaced by individual ITickable/IStartable controllers in Assets/Source")]
// public class GameFixedUpdateSystems : Feature
// {
//     public GameFixedUpdateSystems(Contexts contexts)
//     {
//         // collisions
//         Add(new BalloonCollisionSystem(contexts));
// 
//         // clean up
//         Add(new Cleanup2DTriggersSystem(contexts));
//     }
// }
