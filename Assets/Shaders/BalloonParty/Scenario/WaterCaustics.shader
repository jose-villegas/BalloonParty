Shader "Hidden/BalloonParty/Scenario/WaterCaustics"
{
    // One tile of the light the water's surface focuses on the bottom (WaterCausticBuffer draws it each frame
    // into a small square target that wraps). The net of bright curved lines real caustics make: the borders of
    // drifting cells, warped so they bend, bright on the border and fading into each cell. Water.shader lays the
    // tile under its surface twice, at two scales and angles, the second varying the brightness of the first
    // along its lines, so the tiling never shows.
    //
    // Out: r = focused light (0 = none, 1 = the core of a line). Every term wraps at the tile's edges.
    Properties
    {
        // Cells across the tile: more = a finer net.
        _CausticCells     ("Cells Across",               Range(2, 12))   = 5
        // How fast the cells drift, so the net shifts the way light under moving water does.
        _CausticSpeed     ("Drift Speed",                Float)          = 0.6
        // Tile widths the net is bent by: 0 = straight borders, more = curvier lines.
        _CausticWarp      ("Warp",                       Range(0, 0.1))  = 0.04
        // How quickly a line's light falls off into the cell: more = thinner lines.
        _CausticSharpness ("Sharpness",                  Float)          = 10
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define TAU 6.2831853

            float _CausticCells;
            float _CausticSpeed;
            float _CausticWarp;
            float _CausticSharpness;

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // How far a tile position is from the nearest border between two cells (0 on a border), cells
            // counted modulo the tile so the net wraps. Each cell's point circles its own centre at its own pace.
            float CellBorder(float2 uv, float cells, float time)
            {
                float2 p = uv * cells;
                float2 cell = floor(p);
                float2 f = p - cell;
                float nearest = 8.0;
                float second = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        // Not fmod: GPU division is approximate, so fmod(10, 5) can come out 5, and the cell
                        // across the tile's edge would hash differently from its twin. The half rounds that away.
                        float2 id = cell + offset;
                        id -= cells * floor((id + 0.5) / cells);
                        float2 h = Hash2(id);
                        float2 site = 0.5 + 0.35 * sin(time * (0.6 + 0.8 * h.yx) + TAU * h);
                        float d = length(offset + site - f);
                        second = d < nearest ? nearest : min(second, d);
                        nearest = min(nearest, d);
                    }
                }

                return second - nearest;
            }

            float4 frag(v2f_img IN) : SV_Target
            {
                float time = _Time.y * _CausticSpeed;
                float cells = max(2.0, round(_CausticCells));

                // Whole waves across the tile, so the warp wraps with it.
                float2 uv = IN.uv;
                float2 warp = float2(sin(TAU * (2.0 * uv.y + uv.x) + time * 0.7),
                                     sin(TAU * (2.0 * uv.x - uv.y) - time * 0.9))
                            + 0.5 * float2(sin(TAU * 3.0 * uv.y - time * 1.3), sin(TAU * 3.0 * uv.x + time * 1.1));
                uv += warp * _CausticWarp;

                float border = CellBorder(uv, cells, time);
                float light = exp(-border * _CausticSharpness);
                return float4(light, 0.0, 0.0, 1.0);
            }
            ENDCG
        }
    }
}
