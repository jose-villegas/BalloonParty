#if UNITY_ENABLE_CHECKS
using System.Collections.Generic;
using BalloonParty.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalloonParty.Cheats.Autoplay
{
    /// <summary>
    ///     Taps an on-screen button the way a finger does, so autoplay goes through the same screens a player
    ///     does: a raycast at the button's centre, then pointer down, up and click to whatever that hits. Every
    ///     listener on the button runs (its press animation, the scene load, the launch stamp), not only the one
    ///     call the bot is after, and a button something else covers isn't tapped through.
    /// </summary>
    internal static class AutoplayTap
    {
        private static readonly List<RaycastResult> Hits = new();

        /// <summary>
        ///     Taps the first live button that calls <paramref name="method" /> on a <typeparamref name="T" />,
        ///     from a Button's On Click or a <see cref="ScaleDownButton" />'s On Scale Complete. False when there
        ///     is none a finger could tap yet (the screen is still coming in, or something covers it).
        /// </summary>
        internal static bool ButtonThatCalls<T>(string method) where T : Object
        {
            foreach (var button in Object.FindObjectsByType<ScaleDownButton>())
            {
                if (button.isActiveAndEnabled && Calls<T>(button.OnScaleComplete, method) && Tap(button.gameObject))
                {
                    return true;
                }
            }

            foreach (var button in Object.FindObjectsByType<Button>())
            {
                if (button.isActiveAndEnabled && button.IsInteractable() && Calls<T>(button.onClick, method)
                    && Tap(button.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Calls<T>(UnityEventBase unityEvent, string method) where T : Object
        {
            for (var i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                if (unityEvent.GetPersistentTarget(i) is T && unityEvent.GetPersistentMethodName(i) == method)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Tap(GameObject button)
        {
            var events = EventSystem.current;
            if (events == null || button.transform is not RectTransform rect)
            {
                return false;
            }

            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var pointer = new PointerEventData(events)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            };

            Hits.Clear();
            events.RaycastAll(pointer, Hits);
            if (Hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject) != button)
            {
                return false;
            }

            var hit = Hits[0].gameObject;
            pointer.pointerCurrentRaycast = Hits[0];
            pointer.pointerPressRaycast = Hits[0];
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
            return true;
        }
    }
}
#endif
