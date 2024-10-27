Shader "Custom/CloudShaderV3"
{
    Properties
    {
        _NoiseTexture3D ("3D Noise Texture", 3D) = "" {}
        _NoiseTexture2D ("2D Noise Texture", 2D) = "" {}
        _NoiseAmplitude ("Noise Amplitude", Range(0, 1)) = 1 
        _NoiseScale ("Noise Scale", Range(0, 1)) = 1
        _Noise2DScale ("Noise 2D Scale", Range(0, 1)) = 1
        _HGG ("Henyey-Greenstein G", Range(-1, 1)) = 0
        _ScatteringAlbedo ("Scattering Albedo", Color) = (0.9, 0.9, 0.9, 1)
        _MultipleScatteringFactor ("Multiple Scattering Factor", Range(0, 1)) = 0.5
        _MinStepSize ("Min Step Size", Float) = 0.1
        _MaxStepSize ("Max Step Size", Float) = 1
        _BoxMin ("Box Min", Vector) = (-1, -1, -1, 0)
        _BoxMax ("Box Max", Vector) = (1, 1, 1, 0)
        _BoxFalloff ("Box Falloff", Range(0, 1)) = 0.1
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
            #pragma target 5.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            #define MAX_STEPS 100
            #define MAX_DIST 100
            #define SURF_DIST 1e-3

            float _NoiseAmplitude;
            float _NoiseScale;
            float _Noise2DScale;
            float _HGG;
            fixed4 _ScatteringAlbedo;
            float _MultipleScatteringFactor;
            sampler3D _NoiseTexture3D;
            sampler2D _NoiseTexture2D;
            sampler2D _CameraDepthTexture;
            float _MinStepSize;
			float _MaxStepSize;
            float3 _BoxMin;
			float3 _BoxMax;
			float _BoxFalloff;


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 ro : TEXCOORD0;
                float3 hit_pos : TEXCOORD1;
                float4 view_pos : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.ro = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
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

            float3 sdRoundBox(float3 p, float3 b, float r) {
			    float3 q = abs(p) - b;
			    return length(max(q, float3(0,0,0))) + min(max(q.x,max(q.y, q.z)), 0) - r;
			}


            // 3D texture sampling function
            float sampleNoise(float3 p) {
                // Check if the point is inside the box
				float3 boxDistance = max(max(_BoxMin - p, p - _BoxMax), 0.0);
				float distanceFromBox = length(boxDistance);

				// Apply falloff
				float falloff = 1.0 - smoothstep(0, _BoxFalloff, distanceFromBox);

				// If the point is outside the box (including falloff), return 0
				if (falloff <= 0)
					return 0;

                float3 boxSize = _BoxMax - _BoxMin;

                float3 uv = float3(p.x / boxSize.x + 0.5, p.y / boxSize.y + 0.5, p.z / boxSize.z + 0.5);
                fixed4 noise_tex_sample = tex3D(_NoiseTexture3D, uv);
                fixed4 noise_2d_tex_sample = tex2D(_NoiseTexture2D, uv.xz * _Noise2DScale);
                float noise = noise_tex_sample.r;

                float noise2d = (noise_2d_tex_sample.b * 0.75 + noise_2d_tex_sample.g * 0.25);

                return  noise * falloff * _NoiseAmplitude;
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
                
                for (int i = 0; i < 10 && shadow > 0.001; i++)
                {
                    samplePos += lightDir * step_size;
                    float density = sampleNoise(samplePos);
                    
                    shadow *= exp(-density * step_size);
                    
                    step_size = clamp(_MinStepSize / (density + 1), _MinStepSize, _MaxStepSize);
                }
                
                return shadow;
            }

            fixed4 volumetricMarch(float3 ro, float3 rd, float starting_depth , float depth_map_val, float3 light_dir, float3 light_color) {
                float depth = starting_depth;
                fixed3 scattered_light = fixed3(0.0, 0.0, 0.0); 

                float total_density = 0;
                float step_size = 0.01;
                float transmittance = 1.0;

                float cosTheta = dot(rd, light_dir);
                float phaseHG = henyeyGreenstein(cosTheta, _HGG);

                [loop]
                for(int i = 0; i < MAX_STEPS && depth < depth_map_val && transmittance > 0.01; i++) {
                    float3 p = ro + depth * rd;
                    float density = sampleNoise(p);

                    float current_step_transmittance = exp(-density * step_size);
                    transmittance *= current_step_transmittance;
                    total_density += density * step_size;

                    scattered_light += transmittance * calculateShadow(p, light_dir) * 
                                       lerp(_ScatteringAlbedo, phaseHG * light_color, _MultipleScatteringFactor) * 
                                       density * step_size;

                    step_size = _MinStepSize;
                    depth += step_size;
                }

                float4 c = float4(scattered_light, 1 - transmittance);
                return c;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 light_dir = normalize(_WorldSpaceLightPos0.xyz);
                float3 light_color = _LightColor0.rgb;

                float3 ray_origin = i.ro;
                float3 ray_direction = normalize(i.hit_pos - i.ro);
                float ray_starting_depth = length(i.hit_pos - i.ro);

                float depth_map = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.view_pos)));

                fixed4 vmarch = volumetricMarch(ray_origin, ray_direction, ray_starting_depth, depth_map, light_dir, light_color);
                return vmarch;
            }
            ENDCG
        }
    }
}
