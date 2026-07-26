using BalloonParty.Game;
using BalloonParty.Game.Level;
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
            var levelController = Container.Resolve<LevelController>();

            foreach (var label in GetComponentsInChildren<ScoreCounterLabel>(true))
            {
                label.Bind(scoreController.TotalScore);
            }

            foreach (var label in GetComponentsInChildren<LevelLabel>(true))
            {
                label.Bind(levelController.Level);
            }
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var bars = GetComponentsInChildren<ColorProgressBar>(true);
            var orbits = GetComponentsInChildren<TimeOfDayOrbit>(true);
            var tints = GetComponentsInChildren<TimeOfDayTint>(true);
            var swaps = GetComponentsInChildren<TimeOfDaySwap>(true);
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
            }
        }
    }
}
