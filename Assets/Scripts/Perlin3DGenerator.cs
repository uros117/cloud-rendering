using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class Perlin3DGenerator : MonoBehaviour
{
    #region Inspector Fields
    [Header("Material Settings")]
    [SerializeField] private Material cloudMaterial;
    [SerializeField] private string noiseTextureName = "_NoiseTexture3D";
    [SerializeField] private ComputeShader noiseComputeShader;

    [Header("Resolution")]
    [SerializeField, Range(32, 1024)]
    private int resolutionXZ = 64;
    [SerializeField, Range(16, 512)]
    private int resolutionY = 32;
    [SerializeField, Range(1, 8)]
    private int numberOfOctaves = 4;

    [Header("Noise Settings")]
    [SerializeField, Range(0.1f, 2.0f)]
    private float baseFrequency = 0.8f;
    [SerializeField, Range(0.1f, 1.0f)]
    private float densityThreshold = 0.4f;

    [SerializeField]
    private AnimationCurve verticalDensityProfile = new AnimationCurve(
        new Keyframe(0.0f, 0.7f) { inTangent = 0.3f, outTangent = 0.3f },
        new Keyframe(0.3f, 1.0f) { inTangent = 0.1f, outTangent = 0.1f },
        new Keyframe(0.6f, 0.4f) { inTangent = -0.5f, outTangent = -0.5f },
        new Keyframe(1.0f, 0.0f) { inTangent = -0.2f, outTangent = -0.2f }
    );

    [Header("Ray Step Size Control")]
    [SerializeField]
    private AnimationCurve stepSizeCoefProfile = new AnimationCurve(
        new Keyframe(0.0f, 0.7f) { inTangent = 0.3f, outTangent = 0.3f },   // Larger steps near ground
        new Keyframe(0.3f, 1.0f) { inTangent = 0.1f, outTangent = 0.1f },   // Max step size in lower clouds
        new Keyframe(0.6f, 0.4f) { inTangent = -0.5f, outTangent = -0.5f }, // Smaller steps in mid-height
        new Keyframe(1.0f, 0.0f) { inTangent = -0.2f, outTangent = -0.2f }  // Tiny steps at cloud tops
    );

    [SerializeField] private string stepSizeLookupName = "_StepSizeCoefLookup";
    private Texture2D stepSizeLookupTable;
    private const int STEP_LT_RESOLUTION = 16;

    #endregion

    #region Private Fields
    private CloudNoiseComputeManager computeManager;
    private Texture3D runtimeTexture;

    [Header("Noise Configuration")]
    [SerializeField] private NoiseConfiguration noiseConfig;
    #endregion

    #region Unity Lifecycle Methods
    private void OnEnable()
    {
        InitializeComputeManager();
        GenerateNoiseTexture();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            GenerateNoiseTexture();
            GenerateRayStepSizeLUT();
        }
    }

    private void OnDisable()
    {
        CleanupResources();
    }
    #endregion

    private void InitializeComputeManager()
    {
        computeManager = gameObject.GetComponent<CloudNoiseComputeManager>();
        if (computeManager == null)
        {
            computeManager = gameObject.AddComponent<CloudNoiseComputeManager>();
        }
    }

    private void CleanupResources()
    {
        if (runtimeTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeTexture);
            }
            else
            {
                DestroyImmediate(runtimeTexture);
            }
            runtimeTexture = null;
        }

        if (computeManager != null)
        {
            if (Application.isPlaying)
            {
                Destroy(computeManager);
            }
            else
            {
                DestroyImmediate(computeManager);
            }
            computeManager = null;
        }
    }

    private void HandleTextureGenerated(Texture3D newTexture)
    {
        if (runtimeTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeTexture);
            }
            else
            {
                DestroyImmediate(runtimeTexture);
            }
        }

        runtimeTexture = newTexture;
        cloudMaterial.SetTexture(noiseTextureName, runtimeTexture);
    }

    #region Public Methods
    public void GenerateNoiseTexture()
    {
        if (!ValidateComponents()) return;

        computeManager.GenerateNoiseTextureGPU(
            noiseComputeShader,
            resolutionXZ,
            resolutionY,
            numberOfOctaves,
            baseFrequency,
            densityThreshold,
            verticalDensityProfile,
            noiseConfig.GetOffsets(),
            noiseConfig.GetAmplitudes(),
            HandleTextureGenerated
        );
    }

    [ContextMenu("Save Texture")]
    public void SaveNoiseTexture()
    {
        if (!ValidateComponents()) return;

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Noise Texture",
            "CloudNoise3D",
            "asset",
            "Save 3D noise texture"
        );
        
        if (string.IsNullOrEmpty(path)) return;

        computeManager.GenerateNoiseTextureGPU(
            noiseComputeShader,
            resolutionXZ,
            resolutionY,
            numberOfOctaves,
            baseFrequency,
            densityThreshold,
            verticalDensityProfile,
            noiseConfig.GetOffsets(),
            noiseConfig.GetAmplitudes(),
            (texture) =>
            {
                AssetDatabase.CreateAsset(texture, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"Saved noise texture to: {path}");
            }
        );
    }

    private void GenerateRayStepSizeLUT()
    {
        if (stepSizeLookupTable != null)
        {
            if (Application.isPlaying)
                Destroy(stepSizeLookupTable);
            else
                DestroyImmediate(stepSizeLookupTable);
        }

        // Create 1D texture
        stepSizeLookupTable = new Texture2D(STEP_LT_RESOLUTION, 1, TextureFormat.RFloat, false, true);
        stepSizeLookupTable.wrapMode = TextureWrapMode.Clamp;
        stepSizeLookupTable.filterMode = FilterMode.Bilinear;

        // Create data array for the texture
        float[] stepSizes = new float[STEP_LT_RESOLUTION];
        for (int i = 0; i < STEP_LT_RESOLUTION; i++)
        {
            float height = i / (float)(STEP_LT_RESOLUTION - 1);
            stepSizes[i] = stepSizeCoefProfile.Evaluate(height);
        }

        // Set the data directly
        stepSizeLookupTable.SetPixelData(stepSizes, 0);
        stepSizeLookupTable.Apply(false);

        if (cloudMaterial != null)
        {
            cloudMaterial.SetTexture(stepSizeLookupName, stepSizeLookupTable);
        }
    }

    [ContextMenu("Save Ray Step Size LUT")]
    public void SaveRayStepSizeLUT()
    {
        if (!ValidateComponents()) return;

        GenerateRayStepSizeLUT();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Ray Step Size LUT",
            "RayStepSizeLUT",
            "asset",
            "Save ray step size lookup texture"
        );
        
        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(stepSizeLookupTable, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"Saved ray step size LUT to: {path}");
    }
    #endregion

    #region Validation
    private bool ValidateComponents()
    {
        if (cloudMaterial == null)
        {
            Debug.LogWarning("Cloud material is not assigned!", this);
            return false;
        }

        if (noiseComputeShader == null)
        {
            Debug.LogWarning("Compute shader is not assigned!", this);
            return false;
        }

        if (computeManager == null)
        {
            return false;
        }

        return true;
    }
    #endregion
}