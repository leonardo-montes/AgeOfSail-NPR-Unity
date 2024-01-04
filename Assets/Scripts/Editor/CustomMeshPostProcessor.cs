#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CustomMeshPostProcessor : AssetPostprocessor
{
    private void OnPostprocessModel(GameObject gameObject)
    {
        // Only import meshes inside of the project
        if (!assetPath.StartsWith("Assets/"))
            return;

        string[] path = assetPath.Split('/');
        ProcessGameObject(gameObject, context, path[path.Length - 1]);
    }

    private static void ProcessGameObject (GameObject gameObject, UnityEditor.AssetImporters.AssetImportContext context, string meshName)
    {
        // Cache
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<int> triangles = new List<int>();
        Dictionary<int, int> referenceVertexIdMap = new Dictionary<int, int>();
        List<MeshInflateHelper.Edge> edges = new List<MeshInflateHelper.Edge>();
        List<Vector3> inflationNormals = new List<Vector3>();

        // Iterate through each mesh
        MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < meshFilters.Length; ++i)
            GenerateInflationNormals(ref vertices, ref uv0, ref triangles, ref referenceVertexIdMap, ref edges, ref inflationNormals, gameObject, meshFilters[i].sharedMesh, context, meshName);
    }

    private static void GenerateInflationNormals(ref List<Vector3> vertices, ref List<Vector2> uv0, ref List<int> triangles, ref Dictionary<int, int> referenceVertexIdMap,
                                                 ref List<MeshInflateHelper.Edge> edges, ref List<Vector3> inflationNormals, GameObject gameObject, Mesh mesh,
                                                 UnityEditor.AssetImporters.AssetImportContext context, string meshName)
    {
        // Title
        string editorTitle = "Importing mesh \"" + meshName + "\"";

        // Copy mesh data to lists
        mesh.CopyDataToLists(ref vertices, ref uv0, ref triangles);

        // Get the edges
        if (!MeshInflateHelper.GetEdges(vertices, triangles, ref referenceVertexIdMap, ref edges, null, editorTitle))
        {
            Debug.LogError(string.Format("GenerateInflationNormals: Error while generating edges for {0}.", mesh.name));
            return;
        }

        // Try to generate a closed mesh
        MeshInflateHelper.MeshType meshType = MeshInflateHelper.TryGenerateClosedMeshFromOpenMesh(ref vertices, ref uv0, ref triangles, edges, referenceVertexIdMap, -0.001f, null, editorTitle);
        
        // Closed-mesh: create a new set of normals to inflate the mesh and set them as UV coordinates for shader use
        if (meshType == MeshInflateHelper.MeshType.Closed)
        {
            // Get the inflation normals
            MeshInflateHelper.GenerateInflationNormals(vertices, triangles, edges, referenceVertexIdMap, ref inflationNormals, 1.0f, null, editorTitle);
            
            // Set the UVs (set as UV3 as UV0 and UV1 can be used for textures and UV2 is used for lightmapping)
            mesh.SetUVs(3, inflationNormals);
        }

        // Open or Semi-Open mesh: create a brand new mesh.
        else
            CreateNewInflationMesh(ref vertices, ref uv0, ref triangles, ref referenceVertexIdMap, ref edges, ref inflationNormals, gameObject, mesh, context, editorTitle);
    }

    private static void CreateNewInflationMesh (ref List<Vector3> vertices, ref List<Vector2> uv0, ref List<int> triangles, ref Dictionary<int, int> referenceVertexIdMap,
                                                ref List<MeshInflateHelper.Edge> edges, ref List<Vector3> inflationNormals, GameObject gameObject, Mesh mesh,
                                                UnityEditor.AssetImporters.AssetImportContext context, string editorTitle)
    {
        // Create a new mesh from the mesh data
        Mesh inflationMesh = new Mesh();
        inflationMesh.name = string.Format("{0}_inflation", mesh.name);
        inflationMesh.SetVertices(vertices);
        inflationMesh.SetUVs(0, uv0);
        inflationMesh.SetTriangles(triangles, 0);

        // Reprocess the edges
        if (!MeshInflateHelper.GetEdges(vertices, triangles, ref referenceVertexIdMap, ref edges, null, editorTitle))
        {
            Debug.LogError(string.Format("CreateNewInflationMesh: Error while generating edges from custom version of {0}.", mesh.name));
            return;
        }
        
        // Get and set the inflation normals
        MeshInflateHelper.GenerateInflationNormals(vertices, triangles, edges, referenceVertexIdMap, ref inflationNormals, 1.0f, null, editorTitle);
        inflationMesh.SetUVs(3, inflationNormals);

        // Save to disk
        // - Add mesh to asset list
        context.AddObjectToAsset(GUID.Generate().ToString(), inflationMesh);

        // - Add GameObject to render the new mesh
        GameObject inflationMeshGameObject = new GameObject(inflationMesh.name);
        inflationMeshGameObject.transform.SetParent(gameObject.transform);
        MeshFilter meshFilter = inflationMeshGameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = inflationMesh;
        MeshRenderer meshRenderer = inflationMeshGameObject.AddComponent<MeshRenderer>();
    }
}
#endif