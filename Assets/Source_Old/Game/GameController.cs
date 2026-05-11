// DEPRECATED - commented out during migration. See MIGRATION_PLAN.md.
// using System;
// using Entitas;
// 
// // Legacy ECS entry point — being replaced by GameManager (MVC). See MIGRATION_PLAN.md.
// [Obsolete("Replaced by Assets/Source/Game/GameManager.cs")]
// public class GameController
// {
//     private readonly Systems _updateSystems;
//     private readonly Systems _fixedUpdateSystems;
// 
//     public GameController(Contexts contexts, IGameConfiguration configuration)
//     {
//         contexts.configuration.SetGameConfiguration(configuration);
// 
//         _updateSystems = new GameUpdateSystems(contexts);
//         _fixedUpdateSystems = new GameFixedUpdateSystems(contexts);
//     }
// 
//     public void Initialize()
//     {
//         _updateSystems.Initialize();
//         _fixedUpdateSystems.Initialize();
//     }
// 
//     public void Execute()
//     {
//         _updateSystems.Execute();
//     }
// 
//     public void FixedExecute()
//     {
//         _fixedUpdateSystems.Execute();
//     }
// 
//     public void Cleanup()
//     {
//         _updateSystems.Cleanup();
//         _fixedUpdateSystems.Cleanup();
//     }
// }