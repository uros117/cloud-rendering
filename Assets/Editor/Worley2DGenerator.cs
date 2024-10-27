using UnityEditor;
using UnityEngine;
using Unity.Mathematics;

public class Worley2DGenerator : MonoBehaviour
{
    [MenuItem("CreateExamples/2DWorleyTillableTexture")]
    static void CreateTileableWorleyTexture2D()
    {
        Unity.Mathematics.Random rand = new Unity.Mathematics.Random(1234123412);

        // Set the texture parameters
        int size = 512; // Size of the texture
        TextureFormat format = TextureFormat.RGB24; // RGB format for flexibility
        TextureWrapMode wrapMode = TextureWrapMode.Repeat; // For tiling

        // Create the texture and apply the parameters
        Texture2D texture = new Texture2D(size, size, format, false);
        texture.wrapMode = wrapMode;

        // Create a 2-dimensional array to store noise data
        Color[] noiseValues = new Color[size * size];

        // Parameters for Worley noise
        int gridSize = 10; // 10x10 grid
        int numCells = gridSize * gridSize; // Total number of cells
        float2[] cellPoints = new float2[numCells];

        // Generate random cell points within each grid cell
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                int index = x + y * gridSize;
                float cellSize = 1f / gridSize;
                float2 cellCorner = new float2(x * cellSize, y * cellSize);
                cellPoints[index] = cellCorner + new float2(rand.NextFloat() * cellSize, rand.NextFloat() * cellSize);
            }
        }

        // Populate the array with Worley noise
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float2 uv = new float2(x / (float)size, y / (float)size);
                Color noiseColor = TileableWorleyNoise2D(uv, cellPoints, gridSize);
                noiseValues[x + y * size] = noiseColor;
            }
        }

        // Copy the noise values to the texture
        texture.SetPixels(noiseValues);

        // Apply the changes to the texture and upload the updated texture to the GPU
        texture.Apply();        

        // Save the texture to your Unity Project
        AssetDatabase.CreateAsset(texture, "Assets/Example2DWorleyTillableTexture.asset");
    }

    // Function to generate 2D tileable Worley noise
    static Color TileableWorleyNoise2D(float2 uv, float2[] cellPoints, int gridSize)
    {
        float f1 = float.MaxValue; // Closest distance
        float f2 = float.MaxValue; // Second closest distance

        for (int cellY = -1; cellY <= 1; cellY++)
        {
            for (int cellX = -1; cellX <= 1; cellX++)
            {
                float2 cellOffset = new float2(cellX, cellY);
                
                for (int y = 0; y < gridSize; y++)
                {
                    for (int x = 0; x < gridSize; x++)
                    {
                        int index = x + y * gridSize;
                        float2 point = cellPoints[index] + cellOffset;
                        float2 vecToPoint = point - uv;
                        float distToPoint = math.length(vecToPoint);

                        if (distToPoint < f1)
                        {
                            f2 = f1;
                            f1 = distToPoint;
                        }
                        else if (distToPoint < f2)
                        {
                            f2 = distToPoint;
                        }
                    }
                }
            }
        }

        // Normalize distances
        f1 = math.saturate(f1 * gridSize);
        f2 = math.saturate(f2 * gridSize);

        // Create different noise patterns
        float worley1 = 1 - f1;
        float worley2 = 1 - f2;

        return new Color(worley1, worley2, noise.pnoise(uv * 10f,new float2(10.0f, 10.0f)));
    }
}
