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

     fixed4 SampleSpriteTexture(float2 uv)
     {
         fixed4 color = tex2D(_MainTex, uv);

 #if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
         if (_AlphaSplitEnabled)
             color.a = tex2D(_AlphaTex, uv).r;
 #endif //UNITY_TEXTURE_ALPHASPLIT_ALLOWED




         float lowLevel = _ShineLocation - _ShineWidth;
         float highLevel = _ShineLocation + _ShineWidth;
         float currentDistanceProjection = (uv.x + uv.y) / 2;
         if (currentDistanceProjection > lowLevel && currentDistanceProjection < highLevel) {
             float whitePower = 1- (abs(currentDistanceProjection - _ShineLocation ) / _ShineWidth);
             color.rgb +=  color.a * whitePower;
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
