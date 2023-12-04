Shader "Umawerse/Voronoi"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Highlight("Highlight",Color) = (1,1,1,1)
        _Count("Count",Range(0,1000)) = 200
        _Speed("Speed",Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags 
		{ 
			"Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Opaque"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True" 
			
		}
        LOD 100
 
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"

            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            
            int _Count;
            float _Speed;
            fixed4 _Highlight;
            
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
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TextureSampleAdd;
 
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
 
            float2 N22(float2 uv)
            {
                return frac(float2(sin(dot(uv,float2(1280,720)))*6545.0,sin(dot(uv,float2(720,1280)))*5454.0));
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv =i.uv;
                half4 color = (tex2D(_MainTex,uv) + _TextureSampleAdd) * i.color;
                float minDst = 1;
                //int id = 0;
                for(int i=0;i<_Count;i++){
                    //将一个点的随机值表示为坐标
                    float2 col = N22(float2(i,i));
                    float2 pos = sin(col*(_Time.y*_Speed+i));;
                    float dst = length(uv-pos);
                    if(minDst>dst){
                        minDst = dst;
                        //id = i;
                    }
                 }
               return lerp(color,_Highlight,minDst);
            }
            ENDCG
        }
    }
}
