using BalloonParty.Balloon.Model;
using UniRx;

namespace BalloonParty.Balloon.View
{
    /// <summary>Implemented by prefab MonoBehaviours that react to model binding; BalloonView discovers and calls them automatically.</summary>
    public interface IBalloonViewBinding
    {
        void Bind(IBalloonModel model, CompositeDisposable disposables);
    }
}
