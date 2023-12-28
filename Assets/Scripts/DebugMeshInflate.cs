using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[ExecuteAlways]
public class DebugMeshInflate : MonoBehaviour
{
    [SerializeField] private MeshFilter m_DefaultMeshFilter;
    [SerializeField] private MeshFilter m_InflateMeshFilter;
    [Space]
    [SerializeField] private Mesh m_Mesh;
    [Space]
    [SerializeField] private float m_Thickness;
    [SerializeField, Min(0.0001f)] private float m_OpenMeshThickness;
    [SerializeField] private bool m_OpenMeshInflate = false;
    [Space]
    [SerializeField] private bool m_RegenerateInflatedMesh = false;

    [SerializeField] private Mesh m_InflateMesh;

    private void Update ()
    {
        if (m_Mesh == null)
            return;

        // Render the base mesh
        if (m_DefaultMeshFilter != null)
        {
            m_DefaultMeshFilter.sharedMesh = m_Mesh;
        }

        // Regenerate the inflated mesh
        if (m_RegenerateInflatedMesh)
        {
            m_RegenerateInflatedMesh = false;
            GenerateInflatedMesh();
        }

        // Render the inflated mesh
        if (m_InflateMeshFilter != null && m_InflateMesh != null)
        {
            m_InflateMeshFilter.sharedMesh = m_InflateMesh;
        }
    }

    private void GenerateInflatedMesh ()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<int> triangles = new List<int>();

        int vertexCount = m_Mesh.vertexCount;

        // Copy mesh data to lists
        m_Mesh.CopyDataToLists(ref vertices, ref uv0, ref triangles);

        // Get the edges
        Dictionary<int, int> referenceVertexIdMap = new Dictionary<int, int>();
        List<MeshInflateHelper.Edge> edges = new List<MeshInflateHelper.Edge>();
        if (!MeshInflateHelper.GetEdges(vertices, triangles, ref referenceVertexIdMap, ref edges, transform))
        {
            Debug.LogError("Error while getting the edges for the first time.");
            return;
        }

        // Generate a closed mesh if needed
        MeshInflateHelper.MeshType meshType = MeshInflateHelper.TryGenerateClosedMeshFromOpenMesh(ref vertices, ref uv0, ref triangles, edges, referenceVertexIdMap, -m_OpenMeshThickness, transform);
        
        if (m_OpenMeshInflate)
        {
            // If the mesh was rebuild (because it wasn't closed), reprocess the edges
            if (meshType >= MeshInflateHelper.MeshType.SemiOpen)
            {
                if (!MeshInflateHelper.GetEdges(vertices, triangles, ref referenceVertexIdMap, ref edges, transform))
                {
                    Debug.LogError("After rebuilding the mesh, couldn't get the edges.");
                    return;
                }
            }
            
            // Get the inflation normals
            List<Vector3> inflationNormals = new List<Vector3>();
            MeshInflateHelper.GenerateInflationNormals(vertices, triangles, edges, referenceVertexIdMap, ref inflationNormals, m_Thickness, transform);
            
            // Inflate the mesh
            for (int i = 0; i < vertices.Count; ++i)
                vertices[i] += inflationNormals[i];
        }

        // Set the new mesh data
        m_InflateMesh = new Mesh();
        m_InflateMesh.SetVertices(vertices);
        m_InflateMesh.SetUVs(0, uv0);
        m_InflateMesh.SetTriangles(triangles, 0);
        m_InflateMesh.RecalculateNormals();
        m_InflateMesh.RecalculateTangents();
        m_InflateMesh.RecalculateBounds();
    }
}
