using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class CloudNoiseComputeManager : MonoBehaviour
{
    private const int THREAD_GROUP_SIZE = 8;
    private const int MAX_OCTAVES = 8;

    private class ComputeResources
    {
        public ComputeBuffer HeightProfile;
        public ComputeBuffer Frequencies;
        public ComputeBuffer Amplitudes;
        public ComputeBuffer Offsets;
        public RenderTexture RenderTarget;

        public void Release()
        {
            if (HeightProfile != null)
            {
                HeightProfile.Release();
                HeightProfile = null;
            }
            if (Frequencies != null)
            {
                Frequencies.Release();
                Frequencies = null;
            }
            if (Amplitudes != null)
            {
                Amplitudes.Release();
                Amplitudes = null;
            }
            if (Offsets != null)
            {
                Offsets.Release();
                Offsets = null;
            }
            if (RenderTarget != null)
            {
                RenderTarget.Release();
                RenderTarget = null;
            }
        }
    }

    private ComputeResources resources;

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void ReleaseResources()
    {
        if (resources != null)
        {
            resources.Release();
            resources = null;
        }
    }

    private Vector3[] CalculateFrequencies(float baseFrequency, int octaves)
    {
        var frequencies = new Vector3[MAX_OCTAVES];
        for (int i = 0; i < octaves; i++)
        {
            float freq = baseFrequency * Mathf.Pow(2, i);
            frequencies[i] = new Vector3(freq, freq, freq);
        }
        return frequencies;
    }

    private float[] GenerateHeightProfile(int sizeY, AnimationCurve densityProfile)
    {
        var profile = new float[sizeY];
        for (int y = 0; y < sizeY; y++)
        {
            float heightRatio = y / (float)(sizeY - 1);
            profile[y] = densityProfile.Evaluate(heightRatio);
        }
        return profile;
    }

    private RenderTexture CreateRenderTarget(int sizeXZ, int sizeY)
    {
        var rt = new RenderTexture(sizeXZ, sizeY, 0, RenderTextureFormat.ARGB32);
        rt.dimension = TextureDimension.Tex3D;
        rt.volumeDepth = sizeXZ;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    public void GenerateNoiseTextureGPU(
        ComputeShader compute,
        int sizeXZ,
        int sizeY,
        int octaves,
        float baseFrequency,
        float densityThreshold,
        AnimationCurve densityProfile,
        Vector3[] offsets,
        float[] amplitudes,
        System.Action<Texture3D> onComplete)
    {
        ReleaseResources();

        resources = new ComputeResources();

        // Create render target first
        resources.RenderTarget = CreateRenderTarget(sizeXZ, sizeY);

        // Prepare data
        var frequencies = CalculateFrequencies(baseFrequency, octaves);
        var heightProfile = GenerateHeightProfile(sizeY, densityProfile);

        // Create and setup compute buffers
        resources.HeightProfile = new ComputeBuffer(heightProfile.Length, sizeof(float));
        resources.HeightProfile.SetData(heightProfile);

        resources.Frequencies = new ComputeBuffer(MAX_OCTAVES, sizeof(float) * 3);
        resources.Frequencies.SetData(frequencies);

        resources.Amplitudes = new ComputeBuffer(MAX_OCTAVES, sizeof(float));
        resources.Amplitudes.SetData(amplitudes);

        resources.Offsets = new ComputeBuffer(MAX_OCTAVES, sizeof(float) * 3);
        resources.Offsets.SetData(offsets);

        // Setup compute shader
        int kernelIndex = compute.FindKernel("GenerateNoise3D");

        compute.SetBuffer(kernelIndex, "HeightProfile", resources.HeightProfile);
        compute.SetBuffer(kernelIndex, "Frequencies", resources.Frequencies);
        compute.SetBuffer(kernelIndex, "Amplitudes", resources.Amplitudes);
        compute.SetBuffer(kernelIndex, "Offsets", resources.Offsets);

        compute.SetTexture(kernelIndex, "Result", resources.RenderTarget);
        compute.SetInt("NumOctaves", octaves);
        compute.SetInt("SizeXZ", sizeXZ);
        compute.SetInt("SizeY", sizeY);
        compute.SetFloat("DensityThreshold", densityThreshold);

        // Dispatch compute shader
        int threadGroupsX = Mathf.CeilToInt(sizeXZ / (float)THREAD_GROUP_SIZE);
        int threadGroupsY = Mathf.CeilToInt(sizeY / (float)THREAD_GROUP_SIZE);
        int threadGroupsZ = Mathf.CeilToInt(sizeXZ / (float)THREAD_GROUP_SIZE);

        compute.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);

        // Read back the results
        var nativeArray = new NativeArray<byte>(
            sizeXZ * sizeY * sizeXZ * 4, // *4 because ARGB32 is 4 bytes per pixel
            Allocator.Persistent, 
            NativeArrayOptions.UninitializedMemory
        );

        AsyncGPUReadback.RequestIntoNativeArray(ref nativeArray, resources.RenderTarget, 0, (_) =>
        {
            // Convert ARGB32 data to R8
            var r8Data = new byte[sizeXZ * sizeY * sizeXZ];
            for (int i = 0; i < r8Data.Length; i++)
            {
                // Take the red channel from ARGB32 format
                r8Data[i] = nativeArray[i * 4];
            }

            var texture = new Texture3D(sizeXZ, sizeY, sizeXZ, TextureFormat.R8, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };
            
            texture.SetPixelData(r8Data, 0);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            
            onComplete?.Invoke(texture);
            
            nativeArray.Dispose();
            ReleaseResources();
        });
    }
}
