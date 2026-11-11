Shader "Umawerse/ColorMask"{
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_MaskColor("Mask Color",Color) = (0.95,0.95,0.95,1)
		_Thread("Thread",Range(0,16)) = 0.8 
		_Slope("Slope",Range(0,1)) = 0.2
	}
	SubShader {
		Tags 
		{ 
			"Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True" 
			
		}
		LOD 200
		Blend SrcAlpha OneMinusSrcAlpha
		Pass{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			sampler2D _MainTex;
			float4 _MainTex_ST;
			float3 _MaskColor;
			float _Thread;
			float _Slope;
			# include "UnityCG.cginc"
			struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
 
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };
			v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
			float4 frag(v2f i):COLOR
			{
				float3 c = tex2D (_MainTex, i.uv).rgb;
				float d = length(abs(_MaskColor-c));
				float e = _Thread * (1-_Slope);
				float a = smoothstep(e,_Thread,d);
				return float4(i.color.rgb,i.color.a * a);
			}
			ENDCG
		}
	}
	FallBack "Sprites/Default"
}
