using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EditableBox : MonoBehaviour
{
    public Vector3 dimensions = Vector3.one;
    private Vector3 lastDimensions;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Create a default material if none is assigned
        if (meshRenderer.sharedMaterial == null)
        {
            meshRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
        }

        CreateMesh();
    }

    private void Update()
    {
        if (dimensions != lastDimensions)
        {
            CreateMesh();
            lastDimensions = dimensions;
        }
    }

    private void CreateMesh()
    {
        Mesh mesh = new Mesh();

        Vector3 halfSize = dimensions * 0.5f;

        Vector3[] vertices = {
            new Vector3 (-halfSize.x, -halfSize.y, -halfSize.z),
            new Vector3 (halfSize.x, -halfSize.y, -halfSize.z),
            new Vector3 (halfSize.x, halfSize.y, -halfSize.z),
            new Vector3 (-halfSize.x, halfSize.y, -halfSize.z),
            new Vector3 (-halfSize.x, halfSize.y, halfSize.z),
            new Vector3 (halfSize.x, halfSize.y, halfSize.z),
            new Vector3 (halfSize.x, -halfSize.y, halfSize.z),
            new Vector3 (-halfSize.x, -halfSize.y, halfSize.z),
        };
        
        int[] triangles = {
            0, 2, 1, //face front
            0, 3, 2,
            2, 3, 4, //face top
            2, 4, 5,
            1, 2, 5, //face right
            1, 5, 6,
            0, 7, 4, //face left
            0, 4, 3,
            5, 4, 7, //face back
            5, 7, 6,
            0, 6, 7, //face bottom
            0, 1, 6
        };

        Vector2[] uv = new Vector2[]
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();

        meshFilter.sharedMesh = mesh;

    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, dimensions);
    }
}
