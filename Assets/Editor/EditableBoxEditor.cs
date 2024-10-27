using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EditableBox))]
public class EditableBoxEditor : Editor
{
    private EditableBox editableBox;
    private Vector3 lastHitPoint;

    private void OnEnable()
    {
        editableBox = (EditableBox)target;
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            editableBox.SendMessage("CreateMesh", SendMessageOptions.DontRequireReceiver);
        }
    }

    private void OnSceneGUI()
    {
        if (editableBox == null) return;

        // Draw handles for each face of the box
        DrawBoxFaceHandles();

        // Allow scene view selection of faces
        HandleMouseInteraction();
    }

    private void DrawBoxFaceHandles()
    {
        Vector3[] directions = { Vector3.right, Vector3.up, Vector3.forward };
        for (int i = 0; i < 3; i++)
        {
            Vector3 dir = directions[i];
            Vector3 size = editableBox.dimensions;
            size[i] = 0;

            Vector3 posA = editableBox.transform.TransformPoint(Vector3.Scale(dir, editableBox.dimensions) * 0.5f);
            Vector3 posB = editableBox.transform.TransformPoint(Vector3.Scale(dir, editableBox.dimensions) * -0.5f);

            float handleSize = HandleUtility.GetHandleSize(posA) * 0.1f;
            Vector3 snap = Vector3.one * 0.5f;

            EditorGUI.BeginChangeCheck();
            var fmh_52_49_638650720078957639 = Quaternion.identity; posA = Handles.FreeMoveHandle(posA, handleSize, snap, Handles.DotHandleCap);
            var fmh_53_49_638650720078964963 = Quaternion.identity; posB = Handles.FreeMoveHandle(posB, handleSize, snap, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(editableBox, "Resize Box");
                Vector3 newDimensions = editableBox.dimensions;
                newDimensions[i] = editableBox.transform.InverseTransformVector(posA - posB).magnitude;
                editableBox.dimensions = newDimensions;
                editableBox.SendMessage("CreateMesh", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void HandleMouseInteraction()
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (IntersectRayBox(ray, editableBox.transform, editableBox.dimensions, out Vector3 hitPoint))
            {
                lastHitPoint = hitPoint;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && e.modifiers == EventModifiers.None)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (IntersectRayBox(ray, editableBox.transform, editableBox.dimensions, out Vector3 hitPoint))
            {
                Vector3 delta = hitPoint - lastHitPoint;
                Vector3 localDelta = editableBox.transform.InverseTransformVector(delta);

                Undo.RecordObject(editableBox, "Resize Box");
                editableBox.dimensions += Vector3.Scale(localDelta, Vector3.one * 2f);
                editableBox.dimensions = Vector3.Max(editableBox.dimensions, Vector3.one * 0.1f); // Prevent negative or zero dimensions
                editableBox.SendMessage("CreateMesh", SendMessageOptions.DontRequireReceiver);

                lastHitPoint = hitPoint;
                e.Use();
            }
        }
    }

    private bool IntersectRayBox(Ray ray, Transform boxTransform, Vector3 boxDimensions, out Vector3 hitPoint)
    {
        Vector3 localRayOrigin = boxTransform.InverseTransformPoint(ray.origin);
        Vector3 localRayDirection = boxTransform.InverseTransformDirection(ray.direction);

        Vector3 min = -boxDimensions * 0.5f;
        Vector3 max = boxDimensions * 0.5f;

        float tmin = float.MinValue, tmax = float.MaxValue;

        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(localRayDirection[i]) < Mathf.Epsilon)
            {
                if (localRayOrigin[i] < min[i] || localRayOrigin[i] > max[i])
                {
                    hitPoint = Vector3.zero;
                    return false;
                }
            }
            else
            {
                float ood = 1f / localRayDirection[i];
                float t1 = (min[i] - localRayOrigin[i]) * ood;
                float t2 = (max[i] - localRayOrigin[i]) * ood;

                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                }

                tmin = Mathf.Max(tmin, t1);
                tmax = Mathf.Min(tmax, t2);

                if (tmin > tmax)
                {
                    hitPoint = Vector3.zero;
                    return false;
                }
            }
        }

        Vector3 localHitPoint = localRayOrigin + localRayDirection * tmin;
        hitPoint = boxTransform.TransformPoint(localHitPoint);
        return true;
    }
}
