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
 #pragma multi_compile _ PIXELSNAP_ON
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"

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

     // Global shader property — set by SceneLightService, not in Properties so
     // material values can't mask it. Points TOWARD the light, normalized;
     // canonical (-0.707, 0.707) = upper-left.
     float4 _SceneLightDir;

     // Set globally by SceneLightService; kept out of Properties so no
     // material value can shadow the scene-wide light. Colour's alpha is the
     // "owner has pushed" validity flag (see SceneLightTint).
     float4 _SceneLightColor;
     float  _SceneLightIntensity;

     // Guarded read of the scene light (see SceneLightService): normalized, toward
     // the light; falls back to the canonical direction if the global hasn't been
     // pushed yet (protects edit-time before its first OnEnable/LateUpdate/OnValidate).
     float2 SceneLightDirection()
     {
         float2 raw = dot(_SceneLightDir.xy, _SceneLightDir.xy) < 1e-4
             ? float2(-0.707, 0.707)
             : _SceneLightDir.xy;
         return normalize(raw);
     }

     // The light's colour × intensity — multiplies into the authored specular response.
     // Neutral (white) when the owner hasn't pushed yet, so nothing dims at edit time.
     float3 SceneLightTint()
     {
         return _SceneLightColor.a > 0.5
             ? _SceneLightColor.rgb * _SceneLightIntensity
             : float3(1.0, 1.0, 1.0);
     }

     fixed4 SampleSpriteTexture(float2 uv)
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
             ? dot(uv - 0.5, -SceneLightDirection()) + 0.5
             : (uv.x + uv.y) / 2;
         if (currentDistanceProjection > lowLevel && currentDistanceProjection < highLevel) {
             float whitePower = 1- (abs(currentDistanceProjection - location) / _ShineWidth);
             // Opted-in shine is "lit by the scene light" — axis AND colour — so tint it;
             // the default (UI) sweep stays pure white regardless of the scene light.
             float3 shineTint = _ShineFromSceneLight > 0.5 ? SceneLightTint() : float3(1.0, 1.0, 1.0);
             color.rgb +=  color.a * whitePower * shineTint;
         }

         return color;
     }

     fixed4 frag(v2f IN) : SV_Target
     {
         UNITY_SETUP_INSTANCE_ID(IN);
         fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
         c.rgb *= c.a;

     return c;
     }
         ENDCG
     }
     }
 }
