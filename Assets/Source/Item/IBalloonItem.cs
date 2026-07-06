using Cysharp.Threading.Tasks;

namespace BalloonParty.Item
{
    public interface IBalloonItem : IItem
    {
        /// <summary>
        ///     Activations can overlap, so per-activation state must live in locals, never handler fields.
        /// </summary>
        UniTask Activate(ItemActivationContext context);
    }
}
