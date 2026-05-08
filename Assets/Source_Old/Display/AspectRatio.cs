using System;
using UnityEngine;

public static class AspectRatio
{
    public static Vector2 GetAspectRatio(int x, int y, bool debug = false)
    {
        float f = (float)x / (float)y;
        int i = 0;

        while (true)
        {
            i++;
            if (Math.Abs(System.Math.Round(f * i, 2) - Mathf.RoundToInt(f * i)) < Constants.Tolerance)
                break;
        }

        if (debug)
            Debug.Log("Aspect ratio is " + f * i + ":" + i + " (Resolution: " + x + "x" + y + ")");

        return new Vector2((float)Math.Round(f * i, 2), i);
    }

    public static Vector2 GetAspectRatio(Vector2 xy, bool debug = false)
    {
        float f = xy.x / xy.y;
        int i = 0;

        while (true)
        {
            i++;
            if (Math.Abs(Math.Round(f * i, 2) - Mathf.RoundToInt(f * i)) < Constants.Tolerance)
                break;
        }

        if (debug)
            Debug.Log("Aspect ratio is " + f * i + ":" + i + " (Resolution: " + xy.x + "x" + xy.y + ")");

        return new Vector2((float)Math.Round(f * i, 2), i);
    }
}