Shader "UI/BlurOptimized"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurRadius ("Blur Radius", Range(0, 20)) = 5.0
        _BlurStrength ("Blur Strength", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }
        
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "BLUR_HORIZONTAL"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _BlurRadius;
            float _BlurStrength;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float4 color = float4(0, 0, 0, 0);
                float totalWeight = 0.0;
                
                // Gaussian weights for 9-tap horizontal blur
                float weights[9] = {0.05, 0.09, 0.12, 0.15, 0.16, 0.15, 0.12, 0.09, 0.05};
                float offsets[9] = {-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0};
                
                // Sample horizontal blur
                for (int j = 0; j < 9; j++)
                {
                    float2 offset = float2(offsets[j] * _BlurRadius * _MainTex_TexelSize.x, 0.0);
                    float weight = weights[j];
                    color += tex2D(_MainTex, uv + offset) * weight;
                    totalWeight += weight;
                }
                
                color /= totalWeight;
                color.a *= _BlurStrength;
                return color;
            }
            ENDCG
        }
        
        Pass
        {
            Name "BLUR_VERTICAL"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _BlurRadius;
            float _BlurStrength;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float4 color = float4(0, 0, 0, 0);
                float totalWeight = 0.0;
                
                // Gaussian weights for 9-tap vertical blur
                float weights[9] = {0.05, 0.09, 0.12, 0.15, 0.16, 0.15, 0.12, 0.09, 0.05};
                float offsets[9] = {-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0};
                
                // Sample vertical blur
                for (int j = 0; j < 9; j++)
                {
                    float2 offset = float2(0.0, offsets[j] * _BlurRadius * _MainTex_TexelSize.y);
                    float weight = weights[j];
                    color += tex2D(_MainTex, uv + offset) * weight;
                    totalWeight += weight;
                }
                
                color /= totalWeight;
                color.a *= _BlurStrength;
                return color;
            }
            ENDCG
        }
    }
    
    Fallback "UI/Default"
}