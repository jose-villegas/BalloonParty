using BalloonParty.Game;
using BalloonParty.Game.Score;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Score
{
    public class ScoreUILifetimeScope : LifetimeScope
    {
        private void Start()
        {
            var scoreController = Container.Resolve<ScoreController>();

            foreach (var label in GetComponentsInChildren<ScoreCounterLabel>(true))
            {
                label.Bind(scoreController.TotalScore);
            }
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var bars = GetComponentsInChildren<ColorProgressBar>(true);
            var orbits = GetComponentsInChildren<TimeOfDayOrbit>(true);
            var tints = GetComponentsInChildren<TimeOfDayTint>(true);
            var swaps = GetComponentsInChildren<TimeOfDaySwap>(true);
            var visibilities = GetComponentsInChildren<TimeOfDayVisibility>(true);
            var levelLabels = GetComponentsInChildren<LevelLabel>(true);
            builder.RegisterBuildCallback(InjectChildren);
            return;

            void InjectChildren(IObjectResolver resolver)
            {
                foreach (var bar in bars)
                {
                    resolver.Inject(bar);
                }

                foreach (var orbit in orbits)
                {
                    resolver.Inject(orbit);
                }

                foreach (var tint in tints)
                {
                    resolver.Inject(tint);
                }

                foreach (var swap in swaps)
                {
                    resolver.Inject(swap);
                }

                foreach (var visibility in visibilities)
                {
                    resolver.Inject(visibility);
                }

                foreach (var label in levelLabels)
                {
                    resolver.Inject(label);
                }
            }
        }
    }
}
