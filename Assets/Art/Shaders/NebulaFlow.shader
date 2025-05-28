Shader "Unlit/NebulaFlowProceduralTileable"
{
    Properties
    {
        _ColorA   ("Color A", Color) = (0,0.2,0.7,1)
        _ColorB   ("Color B", Color) = (0.7,0,0.5,1)
        _Strength ("Brightness", Range(0,3)) = 1
        _UVOffset ("UV Offset", Vector) = (0,0,0,0)
        _NoiseZoom("Noise Zoom", Float) = 8
        _Contrast ("Contrast", Float) = 2.2
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _ColorA, _ColorB;
            float4 _UVOffset;
            float  _Strength;
            float  _NoiseZoom;
            float  _Contrast;

            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy + _UVOffset.xy;
                return o;
            }

            float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }

            float tileableNoise2D(float2 uv, float scale)
            {
                uv *= scale;

                float2 i = floor(uv);
                float2 f = frac(uv);

                float2 a = mod289(i);
                float2 b = mod289(i + 1.0);

                float2 u = f * f * (3.0 - 2.0 * f);

                float hash00 = frac(sin(dot(a, float2(127.1, 311.7))) * 43758.5453);
                float hash10 = frac(sin(dot(float2(b.x, a.y), float2(127.1, 311.7))) * 43758.5453);
                float hash01 = frac(sin(dot(float2(a.x, b.y), float2(127.1, 311.7))) * 43758.5453);
                float hash11 = frac(sin(dot(b, float2(127.1, 311.7))) * 43758.5453);

                float x1 = lerp(hash00, hash10, u.x);
                float x2 = lerp(hash01, hash11, u.x);
                float final = lerp(x1, x2, u.y);

                return final;
            }

            float fbm(float2 uv)
            {
                float total = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 5; i++)
                {
                    total += tileableNoise2D(uv, 1.0) * amplitude;
                    uv *= 2.0;
                    amplitude *= 0.5;
                }

                return pow(total, _Contrast);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv * _NoiseZoom;
                float n = fbm(uv);
                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, n) * n * _Strength;
                return float4(col, 1);
            }
            ENDCG
        }
    }

    FallBack Off
}
