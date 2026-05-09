using BalloonParty.Game;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Score
{
    public class ScoreUILifetimeScope : GameChildLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ColorProgressBarInstancer>();
        }

        private void Start()
        {
            var scoreController = Container.Resolve<ScoreController>();

            foreach (var label in GetComponentsInChildren<ScoreCounterLabel>(true))
                label.Bind(scoreController.TotalScore);

            foreach (var label in GetComponentsInChildren<LevelLabel>(true))
                label.Bind(scoreController.Level);
        }
    }
}