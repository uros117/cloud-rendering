Shader "Custom/CloudShaderV1"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseAmplitude ("Noise Amplitude", Range(0, 1000)) = 10
        _NoiseScale ("Noise Scale", Range(0, 10)) = 10
        _DensityMultiplier ("Density Multiplier", Range(0, 10)) = 1
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ScatterColor ("Scatter Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            float _NoiseAmplitude;
            float _NoiseScale;
            float _DensityMultiplier;
            float4 _BaseColor;
            float4 _ScatterColor;

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float mix(float x, float y, float a) {
                return x * (1 - a) + y * a;
            }

            float3 mix(float3 x, float3 y, float a) {
                return x * (1 - a) + y * a;
            }

            float rand(float3 p) 
            {
                return frac(sin(dot(p, float3(12.345, 67.89, 412.12))) * 42123.45) * 2.0 - 1.0;
            }

            float valueNoise(float3 p) 
            {
                float3 u = floor(p);
                float3 v = frac(p);
                float3 s = smoothstep(0.0, 1.0, v);
                
                float a = rand(u);
                float b = rand(u + float3(1.0, 0.0, 0.0));
                float c = rand(u + float3(0.0, 1.0, 0.0));
                float d = rand(u + float3(1.0, 1.0, 0.0));
                float e = rand(u + float3(0.0, 0.0, 1.0));
                float f = rand(u + float3(1.0, 0.0, 1.0));
                float g = rand(u + float3(0.0, 1.0, 1.0));
                float h = rand(u + float3(1.0, 1.0, 1.0));
                
                return mix(mix(mix(a, b, s.x), mix(c, d, s.x), s.y),
                    mix(mix(e, f, s.x), mix(g, h, s.x), s.y),
                    s.z);
            }

            float fbm(float3 p) 
            {
                float3 q = p * _NoiseScale;
                int numOctaves = 8;
                float weight = 0.5;
                float ret = 0.0;
                
                for (int i = 0; i < numOctaves; i++)
                {
                    ret += weight * valueNoise(q);
                    q *= 2.0;
                    weight *= 0.5;
                }
                return clamp(ret * _NoiseAmplitude - p.y * 0.1, 0.0, 1.0) * _DensityMultiplier;
            }

            float4 volumetricMarch(float3 ro, float3 rd, float max_depth) 
            {
                float depth = 0.0;
                float4 color = float4(0.0, 0.0, 0.0, 0.0);
                
                [loop]
                for(int i = 0; i < 64 && depth < max_depth; i++) 
                {
                    float3 p = ro + depth * rd;
                    float offset = (rand(p) - 0.5) * 0.1;
                    p = ro + (depth + offset) * rd;
                    
                    float density = fbm(p);
                    
                    if(density > 1e-3) 
                    {
                        float4 c = float4(mix(_BaseColor.rgb, _ScatterColor.rgb, density), density);
                        c.a *= 0.4;
                        c.rgb *= c.a;
                        color = color + c * (1.0 - color.a);
                    }
                    
                    depth += max(0.05, 0.02 * depth);
                }
                
                return color;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                float depth_map = LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv).r);
                
                float3 ro = _WorldSpaceCameraPos;
                
                float4 clipSpacePos = float4(i.uv * 2.0 - 1.0, 0.0, 1.0);
                float4 viewSpacePos = mul(unity_CameraInvProjection, clipSpacePos);
                viewSpacePos /= viewSpacePos.w;
                float3 rd = mul(unity_CameraToWorld, float4(viewSpacePos.xyz, 0.0)).xyz;
                rd = normalize(rd);

                float4 vmarch = volumetricMarch(ro, rd, depth_map);
                
                return float4(mix(col.rgb, vmarch.rgb, vmarch.a), 1.0);
            }
            ENDCG
        }
    }
}
