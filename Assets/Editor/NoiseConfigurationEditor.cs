using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NoiseConfiguration))]
public class NoiseConfigurationEditor : Editor
{
    private SerializedProperty octaves;
    private bool[] foldoutStates;

    private void OnEnable()
    {
        octaves = serializedObject.FindProperty("octaves");
        foldoutStates = new bool[octaves.arraySize];
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Header
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Noise Octaves", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Randomize", GUILayout.Width(80)))
        {
            if (EditorUtility.DisplayDialog("Randomize Offsets", 
                "Are you sure you want to randomize all offset values?", 
                "Yes", "No"))
            {
                ((NoiseConfiguration)target).Randomize();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Octaves
        for (int i = 0; i < octaves.arraySize; i++)
        {
            DrawOctaveElement(i);
        }

        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawOctaveElement(int index)
    {
        SerializedProperty octave = octaves.GetArrayElementAtIndex(index);
        SerializedProperty offset = octave.FindPropertyRelative("offset");
        SerializedProperty amplitude = octave.FindPropertyRelative("amplitude");

        // Background box
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Header
        EditorGUILayout.BeginHorizontal();
        foldoutStates[index] = EditorGUILayout.Foldout(foldoutStates[index], 
            $"Octave {index + 1}", true);
        
        GUI.enabled = index > 0;
        if (GUILayout.Button("↑", GUILayout.Width(20)))
        {
            octaves.MoveArrayElement(index, index - 1);
            var temp = foldoutStates[index];
            foldoutStates[index] = foldoutStates[index - 1];
            foldoutStates[index - 1] = temp;
        }
        GUI.enabled = index < octaves.arraySize - 1;
        if (GUILayout.Button("↓", GUILayout.Width(20)))
        {
            octaves.MoveArrayElement(index, index + 1);
            var temp = foldoutStates[index];
            foldoutStates[index] = foldoutStates[index + 1];
            foldoutStates[index + 1] = temp;
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (foldoutStates[index])
        {
            EditorGUI.indentLevel++;
            
            // Offset
            EditorGUILayout.PropertyField(offset);

            // Amplitude with slider
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(amplitude);
            amplitude.floatValue = EditorGUILayout.Slider(amplitude.floatValue, 0f, 1f);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }
}
