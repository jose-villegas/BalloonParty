using BalloonParty.Game.Danger;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Binds every <see cref="DangerGradientView"/> under the danger UI to
    ///     <see cref="SpaceDanger.Level"/> at <see cref="Start"/> — after all Awakes, mirroring how the
    ///     shield/health labels are bound from a runtime entry point rather than self-injected.
    /// </summary>
    internal class DangerGradientBinder : IStartable
    {
        private readonly DangerGradientView[] _views;
        private readonly SpaceDanger _danger;

        [Inject]
        internal DangerGradientBinder(DangerGradientView[] views, SpaceDanger danger)
        {
            _views = views;
            _danger = danger;
        }

        public void Start()
        {
            foreach (var view in _views)
            {
                view.Bind(_danger.Level);
            }
        }
    }
}
