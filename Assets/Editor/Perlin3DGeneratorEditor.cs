using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Perlin3DGenerator))]
public class Perlin3DGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        Perlin3DGenerator generator = (Perlin3DGenerator)target;
        
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        //using (new EditorGUI.DisabledScope(generator.GetCloudMaterial() == null))
        //{
        //    if (GUILayout.Button("Bake High Resolution Texture", GUILayout.Height(30)))
        //    {
        //        BakeHighResolutionTexture(generator);
        //    }
        //}

        //if (generator.GetCloudMaterial() == null)
        //{
        //    EditorGUILayout.HelpBox("Assign a material to enable baking.", MessageType.Warning);
        //}
    }

    private void BakeHighResolutionTexture(Perlin3DGenerator generator)
    {
        //string path = EditorUtility.SaveFilePanelInProject(
        //    "Save Noise Texture",
        //    "CloudNoise3D",
        //    "asset",
        //    "Save high resolution 3D noise texture"
        //);
        
        //if (!string.IsNullOrEmpty(path))
        //{
        //    Texture3D texture = generator.GenerateNoiseTexture(
        //        generator.bakeResolutionXZ,
        //        generator.bakeResolutionY,
        //        generator.bakeNumberOfOctaves
        //    );

        //    if (texture != null)
        //    {
        //        AssetDatabase.CreateAsset(texture, path);
        //        AssetDatabase.SaveAssets();

        //        Material material = generator.GetCloudMaterial();
        //        material.SetTexture(generator.GetTexturePropertyName(), texture);
        //        EditorUtility.SetDirty(material);
        //    }
        //}
    }
}
