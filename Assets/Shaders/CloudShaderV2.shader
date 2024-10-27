Shader "Unlit/CloudShaderV2"
{
    Properties
    {
        //_MainTex ("Texture", 2D) = "white" {}
        _Seed ("Seed vector", Vector) = (0,0,0,0)
        _NoiseAmplitude ("Noise Amplitude", Range(0, 1000)) = 10
        _NoiseScale ("Noise Scale", Range(0, 10)) = 10
        _HGG ("Henyey-Greenstein G", Range(-1, 1)) = 0
        _ScatteringAlbedo ("Scattering Albedo", Color) = (0.9, 0.9, 0.9, 1)
        _MultipleScatteringFactor ("Multiple Scattering Factor", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            #define MAX_STEPS 100
            #define MAX_DIST 100
            #define SURF_DIST 1e-3

            // Properties
            float _NoiseAmplitude;
            float _NoiseScale;
            float _HGG;
            fixed4 _ScatteringAlbedo;
            float _MultipleScatteringFactor;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _CameraDepthTexture;
            float3 _Seed;
            float _StepSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 ro : TEXCOORD1;
                float3 hit_pos : TEXCOORD2;
                float4 view_pos : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.ro = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1));
                o.hit_pos = v.vertex;
                o.view_pos = ComputeScreenPos(o.vertex);
                return o;
            }

            // Utility functions
            float mix(float x, float y, float a) {
                return x * (1 - a) + y * a;
            }

            float3 mix(float3 x, float3 y, float a) {
                return x * (1 - a) + y * a;
            }

            float hash(float n) {
                return frac(sin(n)*43758.5453);
            }

            // Noise functions
            float noise(float3 x) {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f*f*(3.0-2.0*f);
                float n = p.x + p.y*57.0 + 113.0*p.z;
                return lerp(lerp(lerp(hash(n+0.0), hash(n+1.0),f.x),
                           lerp(hash(n+57.0), hash(n+58.0),f.x),f.y),
                           lerp(lerp(hash(n+113.0), hash(n+114.0),f.x),
                           lerp(hash(n+170.0), hash(n+171.0),f.x),f.y),f.z);
            }

            float rand(float3 p) {
                return frac(sin(dot(p, float3(12.345, 67.89, 412.12))) * 42123.45) * 2.0 - 1.0;
            }

            float fbm(float3 p) {
                float3 q = _NoiseScale * p + _Seed;
                int numOctaves = 8;
                float weight = 0.5;
                float ret = 0.0;
                
                for (int i = 0; i < numOctaves; i++)
                {
                    ret += weight * (noise(q) - 0.5);
                    q *= 2.0;
                    weight *= 0.5;
                }

                float len = length(p);
                if(len < 0.5) {
                    return clamp(ret * exp(-len*2) , 0.0, 1.0) * _NoiseAmplitude;
                } else {
                    return 0;
                }
            }

            // Henyey-Greenstein phase function
            float henyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1 - g2) / (4 * UNITY_PI * pow(1 + g2 - 2 * g * cosTheta, 1.5));
            }

            float calculateShadow(float3 position, float3 lightDir)
            {
                float shadow = 1.0;
                float step_size = 0.01;
                float3 samplePos = position;
                
                for (int i = 0; i < 20 && shadow > 0.001; i++)
                {
                    samplePos += lightDir * step_size;
                    float density = fbm(samplePos);
                    
                    shadow *= exp(-density * step_size);
                    
                    step_size = clamp(0.02 / (density + 1), 0.01, 0.2);
                }
                
                return shadow;
            }

            fixed4 volumetricMarch(float3 ro, float3 rd, float depth_map_val, float3 light_dir, float3 light_color) {
                float depth = 0.0;
                fixed3 scattered_light = fixed3(0.0, 0.0, 0.0); 

                float total_density = 0;
                float step_size = 0;
                float last_density = 0;
                float transmittance = 1.0;

                float cosTheta = dot(rd, light_dir);
                float phaseHG = henyeyGreenstein(cosTheta, _HGG);

                for(int i = 0; i < 100 && depth < depth_map_val && transmittance > 0.01; i++) {
                    float3 p = ro + depth * rd;
                    float offset = (rand(p) - 0.5) * 0.005;
                    p = ro + (depth + offset) * rd;
                    float density = fbm(p);

                    float current_step_transmittance = exp(-density * step_size);
                    transmittance *= current_step_transmittance;
                    total_density += (density + last_density) / 2 * step_size;

                    scattered_light += transmittance * calculateShadow(p, light_dir) * 
                                       lerp(_ScatteringAlbedo, phaseHG * light_color, _MultipleScatteringFactor) * 
                                       density * step_size;

                    step_size = clamp(0.02 / (density + 1), 0.01, 0.2);
                    depth += step_size;
                    last_density = density;
                }

                float4 c = float4(scattered_light, 1 - transmittance);
                return c;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 light_dir = normalize(_WorldSpaceLightPos0.xyz);
                float3 light_color = _LightColor0.rgb;

                float3 ray_origin = i.hit_pos;
                float3 ray_direction = normalize(i.hit_pos - i.ro);

                float depth_map = tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.view_pos)).r;
                float depth_map_val = LinearEyeDepth(depth_map);

                fixed4 vmarch = volumetricMarch(ray_origin, ray_direction, depth_map_val, light_dir, light_color);
                return vmarch;
            }
            ENDCG
        }
    }
}
