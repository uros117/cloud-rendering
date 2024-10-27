using UnityEngine;

public class CloudRenderer : MonoBehaviour
{

    public Color lightColor0 = Color.white;
    public ComputeShader cloudComputeShader;
    public Texture3D noiseTexture3D;
    public Texture2D noiseTexture2D;
    public float noiseAmplitude = 10f;
    public float noiseScale = 1f;
    public float noise2DScale = 1f;
    public float hgg = 0f;
    public Color scatteringAlbedo = Color.white;
    public float multipleScatteringFactor = 0.5f;
    public float minStepSize = 0.1f;
    public float maxStepSize = 1f;
    public int shadowStepCount = 10;
    public float densityThreshold = 0.01f;

    private RenderTexture resultTexture;
    private int kernelHandle;
    private Camera mainCamera;
    private ComputeBuffer cameraParamsBuffer;
    private ComputeBuffer cameraForwardBuffer;

    void Start()
    {
        mainCamera = Camera.main;
        kernelHandle = cloudComputeShader.FindKernel("CSMain");

        resultTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
        resultTexture.enableRandomWrite = true;
        resultTexture.Create();

        cameraParamsBuffer = new ComputeBuffer(1, 4 * sizeof(float));
        cameraForwardBuffer = new ComputeBuffer(1, 4 * sizeof(float));

        SetupShader();
    }

    void OnDestroy()
    {
        if (cameraParamsBuffer != null) cameraParamsBuffer.Release();
        if (cameraForwardBuffer != null) cameraForwardBuffer.Release();
    }

    void SetupShader()
    {
        cloudComputeShader.SetTexture(kernelHandle, "Result", resultTexture);
        cloudComputeShader.SetTexture(kernelHandle, "NoiseTexture3D", noiseTexture3D);
        cloudComputeShader.SetTexture(kernelHandle, "NoiseTexture2D", noiseTexture2D);
        cloudComputeShader.SetTexture(kernelHandle, "CameraDepthTexture", Shader.GetGlobalTexture("_CameraDepthTexture"));

        cloudComputeShader.SetBuffer(kernelHandle, "CameraParams", cameraParamsBuffer);
        cloudComputeShader.SetBuffer(kernelHandle, "CameraForward", cameraForwardBuffer);

        cloudComputeShader.SetVector("_LightColor0", lightColor0);
        cloudComputeShader.SetFloat("_NoiseAmplitude", noiseAmplitude);
        cloudComputeShader.SetFloat("_NoiseScale", noiseScale);
        cloudComputeShader.SetFloat("_Noise2DScale", noise2DScale);
        cloudComputeShader.SetFloat("_HGG", hgg);
        cloudComputeShader.SetVector("_ScatteringAlbedo", scatteringAlbedo);
        cloudComputeShader.SetFloat("_MultipleScatteringFactor", multipleScatteringFactor);
        cloudComputeShader.SetFloat("_MinStepSize", minStepSize);
        cloudComputeShader.SetFloat("_MaxStepSize", maxStepSize);
        cloudComputeShader.SetInt("_ShadowStepCount", shadowStepCount);
        cloudComputeShader.SetFloat("_DensityThreshold", densityThreshold);
    }

    void Update()
    {
        Vector4 cameraParams = new Vector4(mainCamera.transform.position.x, mainCamera.transform.position.y, mainCamera.transform.position.z, mainCamera.nearClipPlane);
        cameraParamsBuffer.SetData(new Vector4[] { cameraParams });

        Vector4 cameraForward = new Vector4(mainCamera.transform.forward.x, mainCamera.transform.forward.y, mainCamera.transform.forward.z, 0);
        cameraForwardBuffer.SetData(new Vector4[] { cameraForward });

        cloudComputeShader.Dispatch(kernelHandle, Mathf.CeilToInt(Screen.width / 8f), Mathf.CeilToInt(Screen.height / 8f), 1);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(resultTexture, destination);
    }
}
