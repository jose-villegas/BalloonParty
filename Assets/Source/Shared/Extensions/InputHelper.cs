using UnityEngine;
using UnityEngine.EventSystems;

namespace BalloonParty.Shared.Extensions
{
    internal static class InputHelper
    {
        /// <summary>
        ///     True when the current primary pointer is over a uGUI element (a Button, a panel), so raw
        ///     <c>Input.GetMouseButton</c> world handlers can ignore taps the UI already consumed. Passes the
        ///     active touch's fingerId on device (the no-arg overload only tracks the mouse pointer); falls
        ///     back to the mouse pointer in the editor. Safe before an EventSystem exists (returns false).
        /// </summary>
        internal static bool PointerIsOverUI()
        {
            var events = EventSystem.current;
            if (events == null)
            {
                return false;
            }

            if (Input.touchCount > 0)
            {
                return events.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            }

            return events.IsPointerOverGameObject();
        }
    }
}
