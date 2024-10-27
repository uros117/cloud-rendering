using UnityEngine;
using System;

[Serializable]
public class NoiseOctaveSettings
{
    public Vector3 offset = Vector3.zero;
    public float amplitude = 1.0f;
}

[CreateAssetMenu(fileName = "NoiseConfiguration", menuName = "Cloud Noise/Noise Configuration")]
public class NoiseConfiguration : ScriptableObject
{
    public NoiseOctaveSettings[] octaves = new NoiseOctaveSettings[8]
    {
        new NoiseOctaveSettings { offset = new Vector3(15.73f, 63.91f, 27.39f), amplitude = 1.0f },
        new NoiseOctaveSettings { offset = new Vector3(87.23f, 34.57f, 76.92f), amplitude = 0.5f },
        new NoiseOctaveSettings { offset = new Vector3(45.32f, 96.15f, 12.48f), amplitude = 0.25f },
        new NoiseOctaveSettings { offset = new Vector3(71.84f, 23.69f, 89.32f), amplitude = 0.125f },
        new NoiseOctaveSettings { offset = new Vector3(33.54f, 78.41f, 55.91f), amplitude = 0.0625f },
        new NoiseOctaveSettings { offset = new Vector3(67.24f, 12.85f, 43.28f), amplitude = 0.03125f },
        new NoiseOctaveSettings { offset = new Vector3(89.47f, 45.32f, 91.76f), amplitude = 0.015625f },
        new NoiseOctaveSettings { offset = new Vector3(23.67f, 89.14f, 34.52f), amplitude = 0.007812f }
    };

    public void Randomize()
    {
        for (int i = 0; i < octaves.Length; i++)
        {
            octaves[i].offset = new Vector3(
                UnityEngine.Random.Range(0f, 100f),
                UnityEngine.Random.Range(0f, 100f),
                UnityEngine.Random.Range(0f, 100f)
            );
        }
    }

    public Vector3[] GetOffsets()
    {
        Vector3[] offsets = new Vector3[octaves.Length];
        for (int i = 0; i < octaves.Length; i++)
        {
            offsets[i] = octaves[i].offset;
        }
        return offsets;
    }

    public float[] GetAmplitudes()
    {
        float[] amplitudes = new float[octaves.Length];
        for (int i = 0; i < octaves.Length; i++)
        {
            amplitudes[i] = octaves[i].amplitude;
        }
        return amplitudes;
    }
}
