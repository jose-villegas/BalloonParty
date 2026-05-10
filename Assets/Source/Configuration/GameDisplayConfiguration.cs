#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace BalloonParty.Configuration
{
    [Serializable]
    public class GameDisplayConfiguration
    {
        [SerializeField] private List<DisplayOption> _displayOptions;

        public float GetOrthogonalSize()
        {
            var ratio = (float)Math.Round((float)Screen.width / Screen.height, 2);

            DisplayOption chosen = null;
            var closeness = 0f;

            foreach (var displayOption in _displayOptions)
            {
                var lowerPrecision = (float)Math.Round(displayOption.Aspect, 2);

                if (chosen == null && ratio >= lowerPrecision)
                {
                    chosen = displayOption;
                    closeness = Math.Abs((lowerPrecision - ratio) / ratio);
                }

                if (chosen != null && ratio >= displayOption.Aspect)
                {
                    var newDifference = Math.Abs((lowerPrecision - ratio) / ratio);

                    if (newDifference < closeness)
                    {
                        chosen = displayOption;
                        closeness = newDifference;
                    }
                }
            }

            return chosen == null ? -1 : chosen.OrthogonalSize;
        }
    }
}
