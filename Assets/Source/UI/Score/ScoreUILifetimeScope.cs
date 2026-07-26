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
            var rotators = GetComponentsInChildren<TimeOfDayOrbit>(true);
            builder.RegisterBuildCallback(InjectChildren);
            return;

            void InjectChildren(IObjectResolver resolver)
            {
                foreach (var bar in bars)
                {
                    resolver.Inject(bar);
                }

                foreach (var rotator in rotators)
                {
                    resolver.Inject(rotator);
                }
            }
        }
    }
}
