using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameDisplayConfiguration
{
    [SerializeField] private List<DisplayOption> _displayOptions;

    public float GetOrthogonalSize()
    {
        var ratio = (float) Screen.width / (float) Screen.height;
        ratio = (float) (Math.Round(ratio, 2));

        DisplayOption chosen = null;
        var closeness = 0f;

        foreach (var displayOption in _displayOptions)
        {
            var lowerPrecision = displayOption.Aspect;
            lowerPrecision = (float) (Math.Round(lowerPrecision, 2));

            if (chosen == null && ratio >= lowerPrecision)
            {
                chosen = displayOption;
                closeness = Math.Abs((lowerPrecision - ratio) / ratio);
            }

            // keep the highest matching
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

        return chosen == null ? - 1 : chosen.OrthogonalSize;
    }
}