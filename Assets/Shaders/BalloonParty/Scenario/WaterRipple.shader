Shader "Hidden/BalloonParty/Scenario/WaterRipple"
{
    // The water's ripples (WaterRippleBuffer): one height field over the disturbance field's bounds, redrawn each
    // frame from the rings in flight. Each ring started where the game reported an impact (a ball's path, a pop, a
    // bomb, a balloon gliding in) and is a short packet of crests whose front walks outward at Ring Speed, flattening
    // as it spreads and fading over Fade seconds, so it reads as a ring travelling away from where the water was
    // struck. A ball leaves a trail of them, each a little further out than the one after it.
    //
    // A wave-equation simulation was tried first, driven by the disturbance field and then by the impacts: driven
    // by the soft field the water only sloshed in board-wide blobs, and with dozens of impacts a second its rings
    // interfered into a busy chop in which no single ring read.
    //
    // Out: r = surface height (RHalf, signed).
    Properties
    {
        // How fast a ring's front walks outward.
        _RippleSpeed      ("Ring Speed (world/s)",       Float) = 1.8
        // World units between a ring's crests.
        _RippleWavelength ("Wavelength (world)",         Float) = 0.4
        // How many wavelengths deep a ring's packet of crests is.
        _RipplePacket     ("Crests Per Ring",            Float) = 1.2
        // Seconds for a ring to lose most of its height (the time constant).
        _RippleFade       ("Fade (seconds)",             Float) = 0.9
        // How tall a ring of full strength starts.
        _RippleDrive      ("Drive",                      Float) = 0.32
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
            #include "UnityCG.cginc"

            #define TAU 6.2831853
            // WaterRippleBuffer's MaxRings.
            #define MAX_RINGS 48

            float _RippleSpeed;
            float _RippleWavelength;
            float _RipplePacket;
            float _RippleFade;
            float _RippleDrive;

            // Set by WaterRippleBuffer: the field's world bounds, and the rings in flight (xy = world origin,
            // z = age in seconds, w = strength 0..1).
            float2 _RippleFieldMin;
            float2 _RippleFieldSize;
            float4 _Rings[MAX_RINGS];
            int    _RingCount;

            float4 frag(v2f_img IN) : SV_Target
            {
                float2 world = _RippleFieldMin + IN.uv * _RippleFieldSize;
                float wavelength = max(_RippleWavelength, 0.05);
                float packet = wavelength * max(_RipplePacket, 0.25);
                float height = 0.0;

                for (int i = 0; i < MAX_RINGS; i++)
                {
                    if (i >= _RingCount)
                    {
                        break;
                    }

                    float4 ring = _Rings[i];
                    float front = ring.z * _RippleSpeed;
                    // Behind the front (negative) the crests trail inward; the packet's envelope keeps them few.
                    float behind = length(world - ring.xy) - front;
                    float envelope = exp(-behind * behind / (packet * packet));
                    // A ring spreads its height around a longer circle as it grows, and loses it to the fade.
                    float spread = rsqrt(1.0 + front / wavelength);
                    float strength = ring.w * exp(-ring.z / max(_RippleFade, 0.05)) * spread;
                    height += cos(behind / wavelength * TAU) * envelope * strength;
                }

                return float4(height * _RippleDrive, 0.0, 0.0, 0.0);
            }
            ENDCG
        }
    }
}
