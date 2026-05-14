using BalloonParty.Balloon.Model;
using UniRx;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    /// Implemented by MonoBehaviours on a balloon prefab that need to react to model binding.
    /// BalloonView discovers all components implementing this interface and calls Bind()
    /// automatically — keeping BalloonView agnostic of specific balloon types.
    /// </summary>
    public interface IBalloonViewBinding
    {
        void Bind(IBalloonModel model, CompositeDisposable disposables);
    }
}
