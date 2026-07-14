// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

//Kaan Yamanyar,Levent Seckin
 Shader "BalloonParty/Sprite/ShinyDefault"
 {
     Properties
     {
         [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
         _Color("Tint", Color) = (1,1,1,1)
         [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)
         _ShineLocation("ShineLocation", Range(0,1)) = 0
         _ShineWidth("ShineWidth", Range(0,1)) = 0
         _ShineSpeed("ShineSpeed", Float) = 0
         // OPT-IN scene lighting: on, the sweep axis derives from _SceneLightDir (scenario
         // objects); off (default), the classic hardcoded 45-degree diagonal (UI stays art).
         [ToggleUI] _ShineFromSceneLight("Shine Follows Scene Light", Float) = 0
         [PerRendererData] _TimeOffset("TimeOffset", Float) = 0
         [MaterialToggle] PixelSnap("Pixel snap", Float) = 0
     }

         SubShader
     {
         Tags
     {
         "Queue" = "Transparent"
         "IgnoreProjector" = "True"
         "RenderType" = "Transparent"
         "PreviewType" = "Plane"
         "CanUseSpriteAtlas" = "True"
     }

         Cull Off
         Lighting Off
         ZWrite Off
         Blend One OneMinusSrcAlpha

         Pass
     {
         CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #pragma multi_compile _ PIXELSNAP_ON
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 #include "../Include/SceneLight.cginc"

     struct appdata_t
     {
         float4 vertex   : POSITION;
         float4 color    : COLOR;
         float2 texcoord : TEXCOORD0;
         UNITY_VERTEX_INPUT_INSTANCE_ID
     };

     struct v2f
     {
         float4 vertex   : SV_POSITION;
         fixed4 color : COLOR;
         float2 texcoord  : TEXCOORD0;
         // Sampled ONCE per sprite (its own centre, vertex stage) so the shine axis/tint
         // stay coherent across the whole quad — the PaintBlob pattern.
         float2 lightDir  : TEXCOORD1;
         float3 lightTint : TEXCOORD2;
         UNITY_VERTEX_INPUT_INSTANCE_ID
     };

     #ifdef UNITY_INSTANCING_ENABLED
         UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
             UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
         UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
         #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
     #else
         fixed4 _RendererColor;
     #endif

     fixed4 _Color;

     v2f vert(appdata_t IN)
     {
         v2f OUT;
         UNITY_SETUP_INSTANCE_ID(IN);
         UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
         OUT.vertex = UnityObjectToClipPos(IN.vertex);
         OUT.texcoord = IN.texcoord;
         OUT.color = IN.color * _Color * _RendererColor;

         // Sprite centre in world space (VTF, target 3.5) — one coherent light reading
         // for the whole shine sweep instead of bending per-fragment.
         float2 spriteCenterWorld = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13);
         OUT.lightDir  = SceneLightDirectionAtLOD(spriteCenterWorld);
         OUT.lightTint = SceneLightTintAtLOD(spriteCenterWorld);
 #ifdef PIXELSNAP_ON
         OUT.vertex = UnityPixelSnap(OUT.vertex);
 #endif

         return OUT;
     }

     sampler2D _MainTex;
     sampler2D _AlphaTex;
     float _AlphaSplitEnabled;
     float _ShineLocation;
     float _ShineWidth;
     float _ShineSpeed;
     float _ShineFromSceneLight;
     float _TimeOffset;

     fixed4 SampleSpriteTexture(float2 uv, float2 lightDir, float3 lightTint)
     {
         fixed4 color = tex2D(_MainTex, uv);

 #if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
         if (_AlphaSplitEnabled)
             color.a = tex2D(_AlphaTex, uv).r;
 #endif //UNITY_TEXTURE_ALPHASPLIT_ALLOWED

         float location = _ShineLocation;
         if (_ShineSpeed > 0)
         {
             location = frac((_Time.y + _TimeOffset) * _ShineSpeed);
         }

         float lowLevel = location - _ShineWidth;
         float highLevel = location + _ShineWidth;
         // Opted-in materials sweep along the scene light's axis, travelling DOWN-light
         // (enters from the lit side; top-to-bottom under the canonical upper-left light);
         // the default keeps the classic hardcoded 45-degree diagonal.
         float currentDistanceProjection = _ShineFromSceneLight > 0.5
             ? dot(uv - 0.5, -lightDir) + 0.5
             : (uv.x + uv.y) / 2;
         if (currentDistanceProjection > lowLevel && currentDistanceProjection < highLevel) {
             float whitePower = 1- (abs(currentDistanceProjection - location) / _ShineWidth);
             // Opted-in shine is "lit by the scene light" — axis AND colour — so tint it;
             // the default (UI) sweep stays pure white regardless of the scene light.
             float3 shineTint = _ShineFromSceneLight > 0.5 ? lightTint : float3(1.0, 1.0, 1.0);
             color.rgb +=  color.a * whitePower * shineTint;
         }

         return color;
     }

     fixed4 frag(v2f IN) : SV_Target
     {
         UNITY_SETUP_INSTANCE_ID(IN);
         fixed4 c = SampleSpriteTexture(IN.texcoord, IN.lightDir, IN.lightTint) * IN.color;
         c.rgb *= c.a;

     return c;
     }
         ENDCG
     }
     }
 }
