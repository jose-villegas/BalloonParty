using System;
using BalloonParty.Shared.GameState;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Play button for the launch screen: rolls the clouds (the ascend scroll) for
    ///     <see cref="_ascendDuration" /> and only THEN transitions to Game — so the game doesn't appear
    ///     until the initial scroll finishes. Wire this to the Play button's onClick in place of the plain
    ///     <c>NavigationTrigger</c>. The cloud field (<c>CloudFieldService</c>) reflects the scroll from
    ///     <see cref="LaunchAscend" />; it's held afterwards, so the game continues at the same scroll.
    /// </summary>
    public class LaunchPlayTrigger : MonoBehaviour
    {
        [Tooltip("World-space distance the clouds roll during the launch ascend.")]
        [SerializeField] private Vector2 _ascendScroll = new(0f, 3f);

        [Tooltip("Seconds the ascend scroll plays before the game transition fires.")]
        [SerializeField] private float _ascendDuration = 1.5f;

        private bool _triggered;

        public void Play()
        {
            if (_triggered)
            {
                return;
            }

            _triggered = true;
            PlayAsync().Forget();
        }

        private async UniTaskVoid PlayAsync()
        {
            LaunchAscend.Begin(_ascendDuration, _ascendScroll);
            await UniTask.Delay(TimeSpan.FromSeconds(_ascendDuration), DelayType.UnscaledDeltaTime);
            Navigation.TransitionTo(NavigationState.Game);
        }
    }
}
