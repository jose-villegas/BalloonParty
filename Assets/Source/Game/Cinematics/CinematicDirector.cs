using BalloonParty.Shared.GameState;
using VContainer.Unity;

namespace BalloonParty.Game.Cinematics
{
    internal class CinematicDirector : ITickable, ILateTickable
    {
        private CinematicScene _currentScene;
        private bool _cinematicActive;

        internal bool IsScenePlaying => _currentScene != null;
        internal bool IsCinematicActive => _cinematicActive;

        public void Tick()
        {
            _currentScene?.OnTick?.Invoke();
        }

        public void LateTick()
        {
            _currentScene?.OnLateTick?.Invoke();
        }

        internal void BeginCinematic(CinematicState state)
        {
            _cinematicActive = true;
            Cinematic.Begin(state);
        }

        /// <summary>
        ///     Begins only when no cinematic is active; use <see cref="BeginCinematic"/> for mid-cinematic state switches.
        /// </summary>
        internal bool TryBeginCinematic(CinematicState state)
        {
            if (_cinematicActive || Cinematic.IsPlaying)
            {
                return false;
            }

            BeginCinematic(state);
            return true;
        }

        internal void CompleteScene()
        {
            var scene = _currentScene;
            _currentScene = null;
            scene?.OnEnd?.Invoke();
        }

        internal void EndCinematic()
        {
            _currentScene = null;

            if (_cinematicActive)
            {
                _cinematicActive = false;
                Cinematic.End();
            }
        }

        internal void PlayScene(CinematicScene scene)
        {
            _currentScene = scene;
            scene.OnBegin?.Invoke();
        }
    }
}
