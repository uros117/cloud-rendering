Shader "Custom/CloudShaderV4"
{
    Properties
    {
        _NoiseTexture3D ("3D Noise Texture", 3D) = "" {}
        _NoiseTexture2D ("2D Noise Texture", 2D) = "" {}
        _NoiseAmplitude ("Noise Amplitude", Range(0, 1)) = 1 
        _NoiseScale ("Noise Scale", Range(0, 1)) = 1
        _Noise2DScale ("Noise 2D Scale", Range(0, 1)) = 1
        // Two-lobe Henyey-Greenstein phase function parameters
        _HGG0 ("Forward Scattering G", Range(0, 1)) = 0.8
        _HGG1 ("Backward Scattering G", Range(-1, 0)) = -0.5
        _HGLerp ("Phase Function Blend", Range(0, 1)) = 0.5
        _ScatteringAlbedo ("Scattering Albedo", Color) = (0.9, 0.9, 0.9, 1)
        _MultipleScatteringFactor ("Multiple Scattering Factor", Range(0, 1)) = 0.5
        _MinStepSize ("Min Step Size", Float) = 0.1
        _MaxStepSize ("Max Step Size", Float) = 1
        _StepSizeCoefLookup ("Step size coefficient lookup table", 2D) = "white" {}
        _TargetDensity ("Target Density", Range(0, 1)) = 0.3
        _BoxMin ("Box Min", Vector) = (-1, -1, -1, 0)
        _BoxMax ("Box Max", Vector) = (1, 1, 1, 0)
        _BoxFalloff ("Box Falloff", Range(0, 1)) = 0.1
        _ExtinctionFactor ("Extinction Factor", Float) = 1.0
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
            float _HGG0;
            float _HGG1;
            float _HGLerp;
            fixed4 _ScatteringAlbedo;
            float _MultipleScatteringFactor;
            float _ExtinctionFactor;
            sampler3D _NoiseTexture3D;
            sampler2D _NoiseTexture2D;
            sampler2D _CameraDepthTexture;
            float _MinStepSize;
            float _MaxStepSize;
			sampler2D _StepSizeCoefLookup;
            float _TargetDensity;
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

            // Two-lobe Henyey-Greenstein phase function
            float twoLobeHenyeyGreenstein(float cosTheta, float g0, float g1, float blend)
            {
                float g0_2 = g0 * g0;
                float p0 = (1 - g0_2) / (4 * UNITY_PI * pow(1 + g0_2 - 2 * g0 * cosTheta, 1.5));
                
                float g1_2 = g1 * g1;
                float p1 = (1 - g1_2) / (4 * UNITY_PI * pow(1 + g1_2 - 2 * g1 * cosTheta, 1.5));
                
                return lerp(p0, p1, blend);
            }

            float sampleNoise(float3 p) {
                float3 boxDistance = max(max(_BoxMin - p, p - _BoxMax), 0.0);
                float distanceFromBox = length(boxDistance);
                float falloff = 1.0 - smoothstep(0, _BoxFalloff, distanceFromBox);
                
                if (falloff <= 0)
                    return 0;

                float3 boxSize = _BoxMax - _BoxMin;
                float3 uv = float3(p.x / boxSize.x + 0.5, p.y / boxSize.y + 0.5, p.z / boxSize.z + 0.5);
                
                fixed4 noise_tex_sample = tex3D(_NoiseTexture3D, uv);
                fixed4 noise_2d_tex_sample = tex2D(_NoiseTexture2D, uv.xz * _Noise2DScale);
                
                float noise = noise_tex_sample.r;
                float noise2d = (noise_2d_tex_sample.b * 0.75 + noise_2d_tex_sample.g * 0.25);
                
                return noise * falloff * _NoiseAmplitude;
            }

            float calculateStepSizeLookupTable(float x, float targetDensity, float maxStep, float minStep) {
				float g = tex2D(_StepSizeCoefLookup, float2(x, 0.5)).r;
				return minStep + (maxStep - minStep) * g;
            }

            float calculateStepSize(float x, float targetDensity, float maxStep, float minStep) {
				float t = targetDensity;
				float b = 1.0 / (t * t * (1.0-t) * (1.0-t));
				float g = b * x*x * (x-1.0)*(x-1.0);
				return maxStep + (minStep - maxStep) * g;
            }

            // Energy conservative shadow evaluation
            float calculateShadow(float3 position, float3 lightDir, float stepSize)
            {
                float shadow = 1.0;
                float3 samplePos = position;
                
                [loop]
                for (int i = 0; i < 16; i++) // Increased samples for better quality
                {
                    samplePos += lightDir * stepSize;
                    float density = sampleNoise(samplePos);
                    float sigmaE = density * _ExtinctionFactor;
                    
                    // Energy conservative transmittance
                    shadow *= exp(-sigmaE * stepSize);
                }
                
                return shadow;
            }

            // Frostbite-style energy conservative volumetric integration
            fixed4 volumetricMarch(float3 ro, float3 rd, float starting_depth, float depth_map_val, float3 light_dir, float3 light_color) 
            {
                float depth = starting_depth;
                fixed3 scattered_light = fixed3(0.0, 0.0, 0.0);
                float transmittance = 1.0;
                float step_size = _MinStepSize;

                float cosTheta = dot(rd, light_dir);
                float phase = twoLobeHenyeyGreenstein(cosTheta, _HGG0, _HGG1, _HGLerp);

                [loop]
                for(int i = 0; i < MAX_STEPS && depth < depth_map_val && transmittance > 0.01; i++) 
                {
                    float3 p = ro + depth * rd;
                    float density = sampleNoise(p);
                    
                    if(density > 0.0)
                    {
                        // Calculate extinction coefficient
                        float sigmaE = density * _ExtinctionFactor;
                        
                        // Calculate in-scattered light
                        float shadow = calculateShadow(p, light_dir, step_size);
                        float3 S = light_color * _ScatteringAlbedo.rgb * density * phase * shadow;
                        
                        // Energy conservative scattering integration (Frostbite method)
                        // S = incoming light, sigmaE = extinction coefficient
                        float3 Sint = (S - S * exp(-sigmaE * step_size)) / sigmaE;
                        scattered_light += transmittance * Sint;
                        
                        // Update transmittance separately
                        transmittance *= exp(-sigmaE * step_size);
                    }
                    
                    // Adaptive step size based on density
                    step_size = calculateStepSizeLookupTable(density, _TargetDensity, _MaxStepSize, _MinStepSize);
                    depth += step_size;
                }

                // Apply multiple scattering approximation
                scattered_light = lerp(scattered_light, scattered_light * _ScatteringAlbedo.rgb, _MultipleScatteringFactor);
                
                return float4(scattered_light, 1 - transmittance);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 light_dir = normalize(_WorldSpaceLightPos0.xyz);
                float3 light_color = _LightColor0.rgb;

                float3 ray_origin = i.ro;
                float3 ray_direction = normalize(i.hit_pos - i.ro);
                float ray_starting_depth = length(i.hit_pos - i.ro);

                float depth_map = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.view_pos)));

                return volumetricMarch(ray_origin, ray_direction, ray_starting_depth, depth_map, light_dir, light_color);
            }
            ENDCG
        }
    }
}
