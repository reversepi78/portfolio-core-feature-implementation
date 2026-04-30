using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MiniMapNavMeshMeshBuilder : MonoBehaviour
{
    [SerializeField] private Material miniMapNavMeshMaterial;
    [SerializeField] private string miniMapGroundLayerName = "MiniMap_Nav";
    [SerializeField] private float yOffset = 0.05f; // 바닥과 완전히 붙어있으면 실제 땅과 겹쳐져서 깜빡거리기 때문에 살짝 띄워놓음

    private void Awake()
    {
        BuildNavMeshMesh();
    }

    [ContextMenu("Build NavMesh Mesh")]
    public void BuildNavMeshMesh()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        Mesh mesh = new Mesh();
        mesh.name = "MiniMapNavMesh";

        Vector3[] vertices = triangulation.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].y += yOffset;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangulation.indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = miniMapNavMeshMaterial;

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        int layer = LayerMask.NameToLayer(miniMapGroundLayerName);
        if (layer != -1)
            gameObject.layer = layer;
    }
}