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
    // - A drop shadow, on a material of its own with Drop Shadow on, drawn on an image of the back layer's frame
    //   behind the balloon: both layers' coverage, softened, moved away from the light in the vertex stage (so
    //   it is never cut off at the frame's edge) and tinted a dark shade of the balloon's colour.
    //
    // The steps between tones are anti-aliased to a screen pixel (fwidth), however far the sheet is scaled, so
    // the fragment shader has no per-pixel branches. The light is read from the scene light field per corner
    // (SceneLight.cginc), as the balloons' body takes it (Sprite/Diffuse in Full mode): the time-of-day light,
    // re-graded as on the balloons, plus local lights (a pop's flash, a laser), whose colour adds and whose
    // direction bends the shading toward them; it needs the vertex stage's texture reads (target 3.5). The
    // card reveal draws it on a World Space canvas among the board's sprites (UI/CameraFittedCanvas), so the
    // screen-space light tints it and its scene capture sees it. Works on a RawImage (pick the frame with its UV
    // rect). Not mask-safe: it has no UI clip rect or stencil, so under a RectMask2D or Mask it draws unclipped
    // (PencilText's block is the one to copy if it ever needs to). Tools/art/balloon_pop/pack_sheets.py
    // previews the bake with a smooth version of this light, not the cel look; the game is the place to judge it.
    Properties
    {
        [PerRendererData] _MainTex ("Surface (normal xy, occlusion, coverage)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        // The sheet's frames per layer, across and down: set from ICardRevealSettings by UI/CardReveal's view, so
        // a fragment knows its frame and the taps below never read a neighbouring one.
        [HideInInspector] _SheetGrid ("Sheet Grid", Vector) = (8, 6, 0, 0)

        [Header(Cel Shading)]
        // How far above the screen the light sits: low rakes across the surface, high lights it head on.
        _LightHeight ("Light Height", Range(0.1, 3)) = 0.9
        // Where the light's falloff (N.L) turns from lit to the middle tone, and from it to the shadow tone.
        _LitEdge ("Lit Edge", Range(-1, 1)) = 0.45
        _ShadowEdge ("Shadow Edge", Range(-1, 1)) = -0.1
        _MidTone ("Middle Tone", Range(0, 1)) = 0.86
        _ShadowTone ("Shadow Tone", Range(0, 1)) = 0.68
        // How wide each step between tones is at the least; never narrower than a screen pixel.
        _BandSoftness ("Band Softness", Range(0, 0.3)) = 0.06
        // The baked occlusion below this reads as a crease, drawn in the crease tone.
        _CreaseEdge ("Crease Edge", Range(0, 1)) = 0.3
        _CreaseTone ("Crease Tone", Range(0, 1)) = 0.8
        // The highlight, a spot of the light's own colour as on the board's balloons: how much of the surface it
        // covers (lower is bigger), how soft its edge is at the least, and how much of the light it shows.
        _HighlightSize ("Highlight Size", Range(0.8, 1)) = 0.985
        _HighlightSoftness ("Highlight Softness", Range(0, 0.05)) = 0.008
        _Specular ("Highlight Strength", Range(0, 1)) = 0.9

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
        // On for the shadow's own material: this image then draws the shadow instead of the rubber.
        [Toggle] _DropShadow ("Draws The Drop Shadow", Float) = 0
        // Multiplies the balloon's colour, so the shadow is a dark shade of it; alpha is its opacity.
        _ShadowColor ("Color", Color) = (0.42, 0.42, 0.5, 0.42)
        // How far the shadow falls from the rubber, away from the light, in canvas units, and how soft its edge
        // is, in frame widths.
        _ShadowOffset ("Distance", Range(0, 40)) = 10
        _ShadowSoftness ("Softness (frames)", Range(0, 0.05)) = 0.012

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
            #pragma target 3.5
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
            float _HighlightSoftness;
            float _Specular;
            float _Scatter;
            float _ScatterReach;
            float _ScatterRim;
            float _ScatterThin;
            float _Transmission;
            float _ScatterEdge;
            float _GlowLift;
            float _DropShadow;
            fixed4 _ShadowColor;
            float _ShadowOffset;
            float _ShadowSoftness;
            float _AmbientHue;
            float _AmbientDimming;
            float _AmbientDesaturation;

            // A step from 0 to 1 as x passes edge: at least softness wide, and never narrower than a screen pixel,
            // so a hard cel edge doesn't stair-step when the sheet is drawn larger than it was baked.
            float Step(float x, float edge, float softness)
            {
                float width = max(softness, fwidth(x));
                return smoothstep(edge - width, edge + width, x);
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
                // The light field where this corner is: the time-of-day light plus any local light (a pop's flash,
                // a laser), as the balloons' bodies take it (Sprite/Diffuse in Full mode), and a direction bent
                // toward a near light. Read per corner, so a light beside the balloon lights its near side more.
                float2 world = mul(unity_ObjectToWorld, IN.vertex).xy;
                OUT.lightTint = SceneLightTintAtLOD(world) + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);
                // The 2D game camera looks straight down the board, so the screen's axes are the world's.
                OUT.toLight = normalize(float3(SceneLightDirectionAtLOD(world), _LightHeight));
                OUT.halfway = normalize(OUT.toLight + float3(0.0, 0.0, 1.0));
                OUT.shadowFade = ShadowLightFadeAtLOD(world);

                // The shadow's image is the back layer's, moved away from the light: on a canvas, object space is
                // the canvas's, so the distance is in canvas units. The time-of-day light's direction, the same at
                // every corner, so the image moves whole.
                float4 position = IN.vertex;
                position.xy -= SceneLightDirection() * _ShadowOffset * _DropShadow;
                OUT.vertex = UnityObjectToClipPos(position);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                OUT.color.rgb = SceneLightDesaturate(OUT.color.rgb, _AmbientDesaturation);
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 toOther = float2(0.0, uv.y < 0.5 ? 0.5 : -0.5);
                // A frame's size in the sheet, and this fragment's frame, inset a little so a tap at its edge
                // doesn't filter in the next one. Outside it, a tap finds no rubber.
                float2 cell = float2(1.0 / _SheetGrid.x, 0.5 / _SheetGrid.y);
                float2 cellMin = floor(uv / cell) * cell;
                float2 inset = cell * 0.004;
                float2 cellMax = cellMin + cell - inset;
                cellMin += inset;
                float3 albedo = IN.color.rgb;

                if (_DropShadow > 0.5)
                {
                    // The whole balloon's outline, both layers, softened by four taps around it.
                    float2 soft = cell * _ShadowSoftness;
                    float cover = saturate(Rubber(uv, toOther, cellMin, cellMax)) * 2.0
                        + saturate(Rubber(uv + float2(soft.x, 0.0), toOther, cellMin, cellMax))
                        + saturate(Rubber(uv - float2(soft.x, 0.0), toOther, cellMin, cellMax))
                        + saturate(Rubber(uv + float2(0.0, soft.y), toOther, cellMin, cellMax))
                        + saturate(Rubber(uv - float2(0.0, soft.y), toOther, cellMin, cellMax));
                    float shadow = cover / 6.0 * _ShadowColor.a * IN.shadowFade * IN.color.a;
                    return half4(_ShadowColor.rgb * albedo * IN.lightTint * shadow, shadow);
                }

                // Frames are square, so a frame width each way is the same distance on screen.
                float2 towardLight = normalize(IN.toLight.xy) * cell;
                half4 surface = tex2D(_MainTex, uv);
                float2 nxy = surface.rg * 2.0 - 1.0;
                float3 normal = float3(nxy, sqrt(saturate(1.0 - dot(nxy, nxy))));
                float facing = dot(normal, IN.toLight);

                float tone = lerp(_ShadowTone, lerp(_MidTone, 1.0, Step(facing, _LitEdge, _BandSoftness)),
                    Step(facing, _ShadowEdge, _BandSoftness));
                float crease = 1.0 - Step(surface.b, _CreaseEdge, _BandSoftness);
                tone *= lerp(1.0, _CreaseTone, crease);
                float highlight = Step(dot(normal, IN.halfway), _HighlightSize, _HighlightSoftness)
                    * _Specular * (1.0 - crease);

                // How much rubber lies toward the light (three taps through both layers, 0 to 1), and how much is
                // here: one layer on its own is a flap or a shred with nothing behind it.
                float2 tap = towardLight * (_ScatterReach / 3.0);
                float toward = (Rubber(uv + tap, toOther, cellMin, cellMax)
                    + Rubber(uv + 2.0 * tap, toOther, cellMin, cellMax)
                    + Rubber(uv + 3.0 * tap, toOther, cellMin, cellMax)) / 6.0;
                float lone = 1.0 - 0.5 * (surface.a + tex2Dlod(_MainTex, float4(uv + toOther, 0.0, 0.0)).a);
                float scatter = (1.0 - toward) * _ScatterRim + lone * _ScatterThin + saturate(-facing) * _Transmission;
                float glow = Step(scatter, _ScatterEdge, _BandSoftness) * _Scatter * (1.0 - crease);
                float3 glowColor = saturate(albedo * (albedo + _GlowLift) * (1.0 + _GlowLift));

                // The highlight replaces the colour rather than adding to it, so it reads white, not a paler red.
                float3 rgb = lerp(lerp(albedo * tone, glowColor, glow), 1.0, highlight) * IN.lightTint;
                // Like the balloons: however hot the light, the rubber keeps its colour and never blooms.
                rgb = SceneLightCapToWhite(rgb);
                float alpha = surface.a * IN.color.a;
                return half4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
