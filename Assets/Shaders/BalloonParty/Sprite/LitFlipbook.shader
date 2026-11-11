Shader "BalloonParty/Sprite/LitFlipbook"
{
    // Cel shades baked surface data with the live scene light, so a baked animation (the card reveal's balloon
    // pops, Tools/art/balloon_pop) sits in the board's light and style. The texture holds the surface, not a
    // picture: red and green the normal's x and y as the camera saw it (0.5 is flat; z is rebuilt), blue the
    // ambient occlusion, alpha the coverage. The light comes from the top of the screen's side the scene light
    // points to (_SceneLightDir), raised by Light Height; the colour is the vertex colour (an Image's or
    // RawImage's colour: the balloon's palette tint).
    //
    // The sheet is two layers of the same frames, the rubber in front of the balloon's centre in its top half
    // and the rubber behind in its bottom half, so a fragment finds the other layer half a sheet away. That
    // gives two cheap screen-space effects, none of them precise, which the flat cel tones don't ask for:
    // - Subsurface: a few taps toward the light add up how much rubber the light crosses to get here. Where
    //   it crosses little (the rim facing the light, a lone flap with no rubber behind it) the latex glows in
    //   a brighter, richer tint, as light through a balloon does. Faces turned away from the light glow too.
    // - A drop shadow, drawn by the back layer only: both layers' coverage, offset away from the light, tinted
    //   the balloon's colour at the balloons' opacity (Balloon/BalloonShadow), so it falls like theirs.
    //
    // The light's colour and dimming are taken as the balloons' body takes them (Sprite/Diffuse in Ambient
    // mode on Balloon/DiffuseSprite), so a reveal at dusk sits in the same light as the balloons. Works on a
    // RawImage (pick the frame with its UV rect). Not mask-safe: it has no UI clip rect or stencil, so under a
    // RectMask2D or Mask it draws unclipped (PencilText's block is the one to copy if it ever needs to).
    // Tools/art/balloon_pop/pack_sheets.py previews the bake with a smooth version of this light, not the cel
    // look; the game is the place to judge the shading.
    Properties
    {
        [PerRendererData] _MainTex ("Surface (normal xy, occlusion, coverage)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        // The sheet's frames per layer, across and down: set from ICardRevealSettings by UI/CardReveal's view, so
        // a fragment knows its frame and the taps below never read a neighbouring one.
        [HideInInspector] _SheetGrid ("Sheet Grid", Vector) = (6, 4, 0, 0)

        [Header(Cel Shading)]
        // How far above the screen the light sits: low rakes across the surface, high lights it head on.
        _LightHeight ("Light Height", Range(0.1, 3)) = 0.9
        // Where the light's falloff (N.L) turns from lit to the middle tone, and from it to the shadow tone.
        _LitEdge ("Lit Edge", Range(-1, 1)) = 0.45
        _ShadowEdge ("Shadow Edge", Range(-1, 1)) = -0.1
        _MidTone ("Middle Tone", Range(0, 1)) = 0.86
        _ShadowTone ("Shadow Tone", Range(0, 1)) = 0.68
        // How wide each step between tones is: 0 is a hard line.
        _BandSoftness ("Band Softness", Range(0.001, 0.3)) = 0.03
        // The baked occlusion below this reads as a crease, drawn in the crease tone.
        _CreaseEdge ("Crease Edge", Range(0, 1)) = 0.55
        _CreaseTone ("Crease Tone", Range(0, 1)) = 0.6
        // The highlight: how much of the surface it covers (lower is bigger) and how bright it is.
        _HighlightSize ("Highlight Size", Range(0.8, 1)) = 0.965
        _Specular ("Highlight Strength", Range(0, 2)) = 0.9

        [Header(Subsurface)]
        // How strongly the glow takes over where it shows.
        _Scatter ("Strength", Range(0, 1)) = 0.85
        // How far toward the light the taps reach, in frame widths: wider glows deeper in from the rim.
        _ScatterReach ("Reach (frames)", Range(0.005, 0.2)) = 0.07
        // How much of the glow comes from the rim facing the light, from a lone layer (a flap, a shred), and
        // from faces turned away from the light.
        _ScatterRim ("From The Rim", Range(0, 2)) = 1.2
        _ScatterThin ("From Lone Rubber", Range(0, 2)) = 0.9
        _Transmission ("From Behind", Range(0, 2)) = 0.7
        // Where the summed glow turns the tone over: lower glows more of the surface.
        _ScatterEdge ("Glow Edge", Range(0, 1)) = 0.5
        // How much brighter than the tint the glow is: the light comes through richer and lighter.
        _GlowLift ("Glow Lift", Range(0, 1)) = 0.45

        [Header(Drop Shadow)]
        // Tinted by the balloon's colour, as Balloon/BalloonShadow is; alpha is its opacity.
        _ShadowColor ("Color", Color) = (1, 1, 1, 0.25)
        // How far the shadow falls from the rubber, away from the light, in frame widths.
        _ShadowReach ("Distance (frames)", Range(0, 0.2)) = 0.12

        [Header(Scene Light)]
        // As on Sprite/Diffuse: how much of the time-of-day light's colour and dimming the surface takes,
        // and how much of its colour it loses as the light darkens. The defaults are the balloons'.
        _AmbientHue ("Ambient Hue", Range(0, 1)) = 0.25
        _AmbientDimming ("Ambient Dimming", Range(0, 1)) = 0.4
        _AmbientDesaturation ("Ambient Desaturation", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        ZTest    [unity_GUIZTestMode]
        Blend    One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex     : SV_POSITION;
                fixed4 color      : COLOR;
                float2 texcoord   : TEXCOORD0;
                float3 lightTint  : TEXCOORD1;
                float3 toLight    : TEXCOORD2;
                float3 halfway    : TEXCOORD3;
                float  shadowFade : TEXCOORD4;
            };

            sampler2D _MainTex;
            float4 _SheetGrid;
            half4 _Color;
            fixed4 _RendererColor;
            float _LightHeight;
            float _LitEdge;
            float _ShadowEdge;
            float _MidTone;
            float _ShadowTone;
            float _BandSoftness;
            float _CreaseEdge;
            float _CreaseTone;
            float _HighlightSize;
            float _Specular;
            float _Scatter;
            float _ScatterReach;
            float _ScatterRim;
            float _ScatterThin;
            float _Transmission;
            float _ScatterEdge;
            float _GlowLift;
            fixed4 _ShadowColor;
            float _ShadowReach;
            float _AmbientHue;
            float _AmbientDimming;
            float _AmbientDesaturation;

            // A step from 0 to 1 as x passes edge, as wide as the band softness.
            float Band(float x, float edge)
            {
                return smoothstep(edge - _BandSoftness, edge + _BandSoftness, x);
            }

            // Both layers' coverage at uv, 0 to 2, and none outside this fragment's frame (cellMin to cellMax): the
            // frames sit edge to edge, and the rubber of the next one must not cast or glow here.
            float Rubber(float2 uv, float2 toOther, float2 cellMin, float2 cellMax)
            {
                float2 within = step(cellMin, uv) * step(uv, cellMax);
                float here = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0)).a + tex2Dlod(_MainTex, float4(uv + toOther, 0.0, 0.0)).a;
                return here * within.x * within.y;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                // One light for the whole quad: the flat global one, as the balloons' bodies take it.
                OUT.lightTint = SceneLightTint() + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);
                OUT.color.rgb = SceneLightDesaturate(OUT.color.rgb, _AmbientDesaturation);

                // The 2D game camera looks straight down the board, so the screen's axes are the world's.
                OUT.toLight = normalize(float3(SceneLightDirection(), _LightHeight));
                OUT.halfway = normalize(OUT.toLight + float3(0.0, 0.0, 1.0));
                OUT.shadowFade = ShadowLightFade();
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                bool back = uv.y < 0.5;
                float2 toOther = float2(0.0, back ? 0.5 : -0.5);
                // A frame's size in the sheet, and this fragment's frame, inset a little so a tap at its edge
                // doesn't filter in the next one. Outside it, a tap finds no rubber.
                float2 cell = float2(1.0 / _SheetGrid.x, 0.5 / _SheetGrid.y);
                float2 cellMin = floor(uv / cell) * cell;
                float2 inset = cell * 0.004;
                float2 cellMax = cellMin + cell - inset;
                cellMin += inset;
                // Frames are square, so a frame width each way is the same distance on screen.
                float2 towardLight = normalize(IN.toLight.xy) * cell;
                float3 albedo = IN.color.rgb;

                half4 surface = tex2D(_MainTex, uv);
                float3 rgb = 0.0;
                if (surface.a > 0.002)
                {
                    float2 nxy = surface.rg * 2.0 - 1.0;
                    float3 normal = float3(nxy, sqrt(saturate(1.0 - dot(nxy, nxy))));
                    float facing = dot(normal, IN.toLight);

                    float tone = lerp(_ShadowTone, lerp(_MidTone, 1.0, Band(facing, _LitEdge)), Band(facing, _ShadowEdge));
                    float crease = 1.0 - Band(surface.b, _CreaseEdge);
                    tone *= lerp(1.0, _CreaseTone, crease);
                    float highlight = Band(dot(normal, IN.halfway), _HighlightSize) * _Specular * (1.0 - crease);

                    // How much rubber lies toward the light (three taps through both layers, 0 to 1), and how
                    // much is here: one layer on its own is a flap or a shred with nothing behind it.
                    float2 tap = towardLight * (_ScatterReach / 3.0);
                    float toward = (Rubber(uv + tap, toOther, cellMin, cellMax)
                        + Rubber(uv + 2.0 * tap, toOther, cellMin, cellMax)
                        + Rubber(uv + 3.0 * tap, toOther, cellMin, cellMax)) / 6.0;
                    float lone = 1.0 - 0.5 * (surface.a + tex2Dlod(_MainTex, float4(uv + toOther, 0.0, 0.0)).a);
                    float scatter = (1.0 - toward) * _ScatterRim + lone * _ScatterThin
                        + saturate(-facing) * _Transmission;
                    float glow = Band(scatter, _ScatterEdge) * _Scatter * (1.0 - crease);
                    float3 glowColor = saturate(albedo * (albedo + _GlowLift) * (1.0 + _GlowLift));

                    rgb = lerp(albedo * tone, glowColor, glow) * IN.lightTint + IN.lightTint * highlight;
                    // Like the balloons: however hot the light, the rubber keeps its colour and never blooms.
                    rgb = SceneLightCapToWhite(rgb);
                }

                float alpha = surface.a * IN.color.a;
                half4 result = half4(rgb * alpha, alpha);

                if (back)
                {
                    // The rubber that would be here if it moved away from the light: both layers, so the shadow
                    // is the whole balloon's. Hard-edged, as cel shadows are.
                    float2 shadowFrom = uv + towardLight * _ShadowReach;
                    float cover = saturate(Rubber(shadowFrom, toOther, cellMin, cellMax));
                    float shadow = cover * _ShadowColor.a * IN.shadowFade * IN.color.a * (1.0 - alpha);
                    result.rgb += _ShadowColor.rgb * albedo * IN.lightTint * shadow;
                    result.a += shadow;
                }

                return result;
            }
            ENDCG
        }
    }
}
