using BalloonParty.Game.Health;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     Binds the health UI's <see cref="HealthCounterLabel"/>(s) to the live HP at
    ///     <see cref="Start"/> — after every <c>Awake</c> has run, so each label has resolved its
    ///     TMP component. Mirrors how the shield UI binds its labels from a runtime entry point
    ///     rather than self-injecting during the parent scope's build.
    /// </summary>
    internal class HealthLabelBinder : IStartable
    {
        private readonly HealthCounterLabel[] _labels;
        private readonly PlayerHealthController _health;

        [Inject]
        internal HealthLabelBinder(HealthCounterLabel[] labels, PlayerHealthController health)
        {
            _labels = labels;
            _health = health;
        }

        public void Start()
        {
            foreach (var label in _labels)
            {
                label.Bind(_health.Current);
            }
        }
    }
}
