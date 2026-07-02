using BalloonParty.Balloon.Model;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Item
{
    public interface IBalloonItem : IItem
    {
        /// <summary>
        ///     Activates the item for one balloon. Handlers are singletons and activations can
        ///     overlap (an AoE item can trigger several in one frame, and chain/splash effects
        ///     resolve over time), so all per-activation state must live in locals captured by
        ///     the activation — never in handler fields.
        /// </summary>
        UniTask Activate(IBalloonModel balloon, Vector3 worldPosition);
    }
}
