// DEPRECATED - commented out during migration. See MIGRATION_PLAN.md.
// // Legacy backward-compatible stub — the canonical type is now
// // ItemConfiguration in Assets/Source/Configuration.
// 
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
// 
// namespace BalloonParty.Configuration
// {
//     [Serializable]
//     public class PowerUpConfiguration
//     {
//         [SerializeField] private List<PowerUpSettings> _powerUps;
// 
//         public List<PowerUpSettings> PowerUps => _powerUps;
// 
//         public PowerUpSettings this[BalloonPowerUp type] => _powerUps.First(x => x.Type == type);
//     }
// }
// 
// 
