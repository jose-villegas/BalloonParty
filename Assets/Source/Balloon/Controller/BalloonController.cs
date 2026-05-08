using VContainer;
using VContainer.Unity;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;

namespace BalloonParty.Balloon.Controller
{
    public class BalloonController : IStartable
    {
        private readonly BalloonModel _model;
        private readonly BalloonView _view;

        [Inject]
        public BalloonController(BalloonModel model, BalloonView view)
        {
            _model = model;
            _view = view;
        }

        public void Start()
        {
            _view.Bind(_model);
        }

        public BalloonModel Model => _model;
    }
}