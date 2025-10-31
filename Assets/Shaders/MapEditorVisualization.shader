Shader "Hidden/MapEditorVisualization"
{
    Properties
    {
        _ColorMap ("Color Map", 2D) = "white" {}
        _Background ("Background", 2D) = "white" {}
        _ShowBackground ("Show Background", Float) = 1.0
        _ShowGrid ("Show Grid", Float) = 1.0
        _ShowErrors ("Show Errors", Float) = 0.0
        _ZoomLevel ("Zoom Level", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _ColorMap;
            sampler2D _Background;
            float _ShowBackground;
            float _ShowGrid;
            float _ShowErrors;
            float _ZoomLevel;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 color = tex2D(_ColorMap, i.uv);
                fixed4 background = tex2D(_Background, i.uv);
                
                // 混合背景
                if (_ShowBackground > 0.5)
                {
                    color.rgb = lerp(background.rgb, color.rgb, color.a);
                }
                
                // 添加网格效果（在放大时显示）
                if (_ShowGrid > 0.5 && _ZoomLevel > 2.0)
                {
                    float2 gridUV = i.uv * _ScreenParams.xy / (_ZoomLevel * 10.0);
                    float grid = (1.0 - step(0.95, frac(gridUV.x))) * 0.1 + 
                                (1.0 - step(0.95, frac(gridUV.y))) * 0.1;
                    color.rgb += grid;
                }
                
                return color;
            }
            ENDCG
        }
    }
}