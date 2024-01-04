using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeshInflateHelper
{
    public enum MeshType { Closed, SemiOpen, FullyOpen }

    /// <summary>
    /// Edge struct that contains two indices of an edge and two indices for their related
    /// triangles.
    /// 
    /// Does not support non-manifold meshes.
    /// </summary>
    public struct Edge
    {
        public int ie0;
        public int ie1;
        public int if0;
        public int? if1;

        public Edge(int ie0, int ie1, int if0)
        {
            this.ie0 = ie0;
            this.ie1 = ie1;
            this.if0 = if0;
            this.if1 = null;
        }
    }

    /// <summary>
    /// Half of PI.
    /// </summary>
    private const float c_PI2 = math.PI / 2.0f;
    
    /// <summary>
    /// Check and add or fill edges to a list.
    /// 
    /// If an edge already exist with the current vertices (compare the distance of the vertices)
    /// fill the existing edge. If it is already filled entirely (2 triangle vertices
    /// already set), the mesh is non-manifold, so we return false and stop there (we don't
    /// process non-manifold meshes).
    /// 
    /// Otherwise, if we couldn't find an edge with the current vertices, we create it and add
    /// it to the list.
    /// </summary>
    /// <param name="vertices">List of vertices from the Mesh.</param>
    /// <param name="edges">List of Edges to check and fill.</param>
    /// <param name="i0">Vertex ID of the first vertex of the edge.</param>
    /// <param name="i1">Vertex ID of the second vertex of the edge.</param>
    /// <param name="i2">Vertex ID of the vertex to build the triangle.</param>
    /// <returns>Returns false if the mesh is non-manifold.</returns>
    public static bool AddToList(this List<Edge> edges, List<Vector3> vertices, int i0, int i1, int i2)
    {
        // Cache
        Edge edge;
        Vector3 v0, v1, edgeV0, edgeV1;
        int edgeCount = edges.Count;

        v0 = vertices[i0];
        v1 = vertices[i1];

        // Check if another edge already exists
        for (int i = 0; i < edgeCount; ++i)
        {
            edgeV0 = vertices[edges[i].ie0];
            edgeV1 = vertices[edges[i].ie1];

            // Same edge
            if ((edgeV0 - v0).sqrMagnitude <= math.EPSILON && (edgeV1 - v1).sqrMagnitude <= math.EPSILON ||
                (edgeV0 - v1).sqrMagnitude <= math.EPSILON && (edgeV1 - v0).sqrMagnitude <= math.EPSILON)
            {
                edge = edges[i];

                // Check if the second face has already been assigned
                // If it's true, we return false because it means that the mesh is non-manifold
                if (edge.if1 != null)
                    return false;

                // Otherwise, assign it!
                edge.if1 = i2;
                edges[i] = edge;
                return true;
            }
        }

        // Add new edge
        edges.Add(new Edge(i0, i1, i2));
        return true;
    }

    /// <summary>
    /// Check and add or fill edges to a list.
    /// 
    /// If an edge already exist with the current vertices (compare their vertex id) fill the
    /// existing edge. If it is already filled entirely (2 triangle vertices already set), the
    /// mesh is non-manifold, so we return false and stop there (we don't process non-manifold
    /// meshes).
    /// 
    /// Otherwise, if we couldn't find an edge with the current vertices, we create it and add
    /// it to the list.
    /// </summary>
    /// <param name="vertices">List of vertices from the Mesh.</param>
    /// <param name="edges">List of Edges to check and fill.</param>
    /// <param name="i0">Vertex ID of the first vertex of the edge.</param>
    /// <param name="i1">Vertex ID of the second vertex of the edge.</param>
    /// <param name="i2">Vertex ID of the vertex to build the triangle.</param>
    /// <returns>Returns false if the mesh is non-manifold.</returns>
    public static bool AddToList(this List<Edge> edges, int i0, int i1, int i2)
    {
        // Cache
        Edge edge;
        int edgeCount = edges.Count;

        // Check if another edge already exists
        for (int i = 0; i < edgeCount; ++i)
        {
            edge = edges[i];

            // Same edge
            if ((edge.ie0 == i0 && edge.ie1 == i1) || (edge.ie0 == i1 && edge.ie1 == i0))
            {
                // Check if the second face has already been assigned
                // If it's true, we return false because it means that the mesh is non-manifold
                if (edge.if1 != null)
                    return false;

                // Otherwise, assign it!
                edge.if1 = i2;
                edges[i] = edge;
                return true;
            }
        }

        // Add new edge
        edges.Add(new Edge(i0, i1, i2));
        return true;
    }

    /// <summary>
    /// Get the surface normal of a triangle.
    /// </summary>
    /// <param name="a">First vertex position of the triangle.</param>
    /// <param name="b">Second vertex position of the triangle.</param>
    /// <param name="c">Last vertex position of the triangle.</param>
    /// <returns>Normalized vector.</returns>
    private static Vector3 GetSurfaceNormal (Vector3 a, Vector3 b, Vector3 c)
    {
        return math.normalize(math.cross(b - a, c - a));
    }

    /// <summary>
    /// Based on Blender's 'float angle_normalized_v3v3(const float v1[3], const float v2[3])'
    /// Link: https://github.com/blender/blender/blob/9c0bffcc89f174f160805de042b00ae7c201c40b/source/blender/blenlib/intern/math_vector.c#L452
    /// </summary>
    private static float Angle (Vector3 a, Vector3 b)
    {
        // This is the same as 'math.acos(math.dot(v1, v2))', but more accurate
        if (math.dot(a, b) >= 0.0f)
            return 2.0f * math.asin((b - a).magnitude / 2.0f);
        
        return math.PI - 2.0f * math.asin((-b - a).magnitude / 2.0f);
    }

    /// <summary>
    /// Copy vertex and triangle data from a mesh into Lists.
    /// </summary>
    /// <param name="mesh">Mesh to copy from.</param>
    /// <param name="vertices">Resulting vertex list.</param>
    /// <param name="triangles">Resulting triangle list.</param>
    public static void CopyDataToLists (this Mesh mesh, ref List<Vector3> vertices, ref List<Vector2> uv0, ref List<int> triangles)
    {
        // Cache
        Vector3[] meshVertices = mesh.vertices;
        Vector2[] meshUV0 = mesh.uv;
        int[] meshTriangles = mesh.triangles;

        int vertexCount = meshVertices.Length;
        int triCount = meshTriangles.Length;

        // Clear
        vertices.Clear();
        uv0.Clear();
        triangles.Clear();

        // Fill
        for (int i = 0; i < vertexCount; ++i)
        {
            vertices.Add(meshVertices[i]);
            uv0.Add(meshUV0[i]);
        }

        for (int i = 0; i < triCount; ++i)
            triangles.Add(meshTriangles[i]);

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vertices"></param>
    /// <param name="triangles"></param>
    /// <param name="referenceVertexIdMap"></param>
    /// <returns></returns>
    public static bool GetEdges(List<Vector3> vertices, List<int> triangles, ref Dictionary<int, int> referenceVertexIdMap, ref List<Edge> edges,
                                Transform debugTransform = null, string editorTitle = null)
    {
#if UNITY_EDITOR
        // Editor
        if (editorTitle != null)
            UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Get edges", "Startup...", 0.0f);
#endif

        // Cache
        Vector3 v0, v1;
        int i0, i1, i2;
        int vertexCount = vertices.Count;
        int triCount = triangles.Count;

        // Check if the base data is weird
        for (int i = 0; i < triCount; i += 3)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Get edges", "Verifying base data...", (float)i / triCount);
#endif

            i0 = triangles[i];
            i1 = triangles[i + 1];
            i2 = triangles[i + 2];
            if (i0 == i1 || i0 == i2 || i1 == i2)
            {
                Debug.LogError(string.Format("GetEdges: Mesh is weird (some triangles have twice the same index, meaning they are just a line). Triangle indices are the following: {0}; {1}; {2}.", i0, i1, i2));
                return false;
            }
        }

        // Reset
        referenceVertexIdMap.Clear();
        edges.Clear();

        // Get all unique vertices and fill normals
        for (int i = 0; i < vertexCount; ++i)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Get edges", "Aggregating vertices sharing the same position...", (float)i / vertexCount);
#endif

            v0 = vertices[i];
            if (referenceVertexIdMap.TryAdd(i, i))
            {
                for (int j = 0; j < vertexCount; ++j)
                {
                    if (j == i)
                        continue;

                    v1 = vertices[j];
                    if ((v0 - v1).magnitude <= math.EPSILON)
                    {
                        referenceVertexIdMap.TryAdd(j, i);
                    }
                }
            }
        }

        // Get all edges
        for (int i = 0; i < triCount; i += 3)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Get edges", "Get all edges...", (float)i / triCount);
#endif

            // Get unique vertices index
            if (!referenceVertexIdMap.TryGetValue(triangles[i], out i0) ||
                !referenceVertexIdMap.TryGetValue(triangles[i + 1], out i1) ||
                !referenceVertexIdMap.TryGetValue(triangles[i + 2], out i2))
            {
                Debug.LogError("GetEdges: Vertex not found in Merged Vertices Dictionnary."); 
                return false;
            }

            // Check if there was an error when getting the indices
            if (i0 == i1 || i0 == i2 || i1 == i2)
            {
                Debug.LogError("GetEdges: Unique vertices error (two indices are the same, it's an edge not a triangle).");
                return false;
            }

            /* As we are using a reference vertex already, we don't have to check if the
             * vertex position are the same!
            if (!edges.AddToList(vertices, i0, i1, i2) ||
                !edges.AddToList(vertices, i1, i2, i0) ||
                !edges.AddToList(vertices, i2, i0, i1))*/
            if (!edges.AddToList(i0, i1, i2) ||
                !edges.AddToList(i1, i2, i0) ||
                !edges.AddToList(i2, i0, i1))
            {
                Debug.LogError("GetEdges: Mesh edges are invalid (more than two faces for one edge, mesh is non-manifold).");
                if (debugTransform != null)
                {
                    Debug.DrawLine(debugTransform.TransformPoint(vertices[i0]), debugTransform.TransformPoint(vertices[i1]), Color.red, 15.0f);
                    Debug.DrawLine(debugTransform.TransformPoint(vertices[i0]), debugTransform.TransformPoint(vertices[i2]), Color.red, 15.0f);
                    Debug.DrawLine(debugTransform.TransformPoint(vertices[i1]), debugTransform.TransformPoint(vertices[i2]), Color.red, 15.0f);
                }
                return false;
            }
        }

#if UNITY_EDITOR
        // Editor
        if (editorTitle != null)
            UnityEditor.EditorUtility.ClearProgressBar();
#endif

        // Done
        return true;
    }

    /// <summary>
    /// Based on Blender's Solidify Modifier
    /// Link: https://github.com/blender/blender/blob/9c0bffcc89f174f160805de042b00ae7c201c40b/source/blender/bmesh/operators/bmo_extrude.cc#L636
    /// 
    /// Only works on manifold meshes.
    /// </summary>
    /// <param name="vertices"></param>
    /// <param name="thickness"></param>
    /// <param name="inflationNormals"></param>
    /// <returns></returns>
    public static void GenerateInflationNormals(List<Vector3> vertices, List<int> triangles, List<Edge> edges, Dictionary<int, int> referenceVertexIdMap,
                                                ref List<Vector3> inflationNormals, float thickness = 1.0f, Transform debugTransform = null,
                                                string editorTitle = null)
    {
#if UNITY_EDITOR
        // Editor
        if (editorTitle != null)
            UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Generate inflated normals", "Startup...", 0.0f);
#endif

        // Cache
        Vector3 edgeNormal, f0;
        Vector3? f1;
        int i0;
        Edge edge;

        int vertexCount = vertices.Count;
        int edgeCount = edges.Count;
        
        // Reset
        inflationNormals.Clear();

        // Fill in empty normals
        for (int i = 0; i < vertexCount; ++i)
        {
            inflationNormals.Add(Vector3.zero);
        }
        
        // Compute normals for all edges
        for (int i = 0; i < edgeCount; ++i)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Generate inflated normals", "Computing normals for all edges...", (float)i / edgeCount);
#endif

            edge = edges[i];
            edgeNormal = Vector3.zero;
            
            // Get triangle normals
            f0 = GetSurfaceNormal(vertices[edge.ie0], vertices[edge.ie1], vertices[edge.if0]);
            f1 = edge.if1 != null ? GetSurfaceNormal(vertices[edge.ie1], vertices[edge.ie0], vertices[edge.if1.Value]) : null;

            // Edge has two faces
            if (f1 != null)
            {
                float angle = Angle(f0, f1.Value);
                if (angle > 0.0f)
                {
                    // Calculate the edge normal using the angle between the faces as a weighting
                    edgeNormal = (f0 + f1.Value).normalized * angle;
                }
                else
                {
                    // Can't do anything useful here!
                    continue;
                }
            }

            // Edge has only one face
            else
            {
                // The weight on this is undefined as there is only one face,
                // PI2 is 90d in radians and that seems good enough.
                edgeNormal = f0 * c_PI2;
            }

            inflationNormals[edge.ie0] += edgeNormal;
            inflationNormals[edge.ie1] += edgeNormal;
        }
        
        // Check for specific cases
        //int[] values = new int[4];
        for (int i = 0; i < vertexCount; ++i)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Generate inflated normals", "Checking specific cases (totally flat surfaces)...", (float)i / vertexCount);
#endif

            // In case the elements are totally flat, use base normal
            if (inflationNormals[i].sqrMagnitude <= math.EPSILON)
            {
                // Get reference id
                referenceVertexIdMap.TryGetValue(i, out i0);
                
                // Get the current edge
                for (int j = 0; j < edgeCount; ++j)
                {
                    if (edges[j].ie0 == i0 || edges[j].ie1 == i0 || edges[j].if0 == i0 || edges[j].if1 == i0)
                    {
                        // Set the surface normal
                        inflationNormals[i] = (edges[j].if1 == i0 ?
                            GetSurfaceNormal(vertices[edges[j].ie1], vertices[edges[j].ie0], vertices[edges[j].if1.Value]) :
                            GetSurfaceNormal(vertices[edges[j].ie0], vertices[edges[j].ie1], vertices[edges[j].if0])) * math.PI;
                        break;
                    }
                }
            }
        }

        // Copy to all inflated normals from reference inflated normals
        for (int i = 0; i < vertexCount; ++i)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Generate inflated normals", "Copying extrusion normals to all vertices...", (float)i / vertexCount);
#endif

            if (referenceVertexIdMap.TryGetValue(i, out i0) && i != i0)
                inflationNormals[i] = inflationNormals[i0];
        }
        
        // Apply thickness
        for (int i = 0; i < vertexCount; ++i)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Generate inflated normals", "Applying thickness...", (float)i / vertexCount);
                
            if (debugTransform != null)
            {
                Vector3 pos = debugTransform.TransformPoint(vertices[i]);
                Debug.DrawLine(pos, pos + debugTransform.TransformDirection(inflationNormals[i]) * 0.1f, Color.red, 10.0f);
            }
#endif

            inflationNormals[i] *= thickness;

        }

#if UNITY_EDITOR
        // Editor
        if (editorTitle != null)
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
    }

    /// <summary>
    /// Generate a closed mesh from an open mesh.
    /// </summary>
    public static MeshType TryGenerateClosedMeshFromOpenMesh (ref List<Vector3> vertices, ref List<Vector2> uv0, ref List<int> triangles, List<Edge> edges,
                                                              Dictionary<int, int> referenceVertexIdMap, float thickness = -0.001f, Transform debugTransform = null,
                                                              string editorTitle = null)
    {
#if UNITY_EDITOR
        // Editor
        if (editorTitle != null)
            UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Generating closed mesh", "Startup...", 0.0f);
#endif

        // Cache
        Edge edge;
        int i, id;
        int edgeCount = edges.Count;

        // Get mask vertices (vertices part of an open-mesh island)
        List<int> maskedStartEdgesId = new List<int>();
        List<int> maskedEdgesIdOpenSet = new List<int>();
        List<int> maskedEdgesIdClosedSet = new List<int>();
        for (i = 0; i < edgeCount; ++i)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Generating closed mesh", "Getting masked vertices...", (float)i / edgeCount);
#endif

            if (edges[i].if1 == null)
            {
                maskedStartEdgesId.Add(i);
                maskedEdgesIdOpenSet.Add(i);

#if UNITY_EDITOR
                if (debugTransform != null)
                {
                    //Debug.DrawLine(debugTransform.TransformPoint(vertices[edges[i].ie0]), debugTransform.TransformPoint(vertices[edges[i].ie1]), Color.red, 10.0f);
                }
#endif
            }
        }

        // Early-out if there is no open part
        int maskedStartEdgesIdCount = maskedStartEdgesId.Count;
        if (maskedStartEdgesIdCount <= 0)
        {
#if UNITY_EDITOR
            // Editor
            if (editorTitle != null)
                UnityEditor.EditorUtility.ClearProgressBar();
#endif

            return MeshType.Closed;
        }

        // Expand the mask
#if UNITY_EDITOR
        int processedEdges = 0;
#endif
        while (maskedEdgesIdOpenSet.Count > 0)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
            {
                ++processedEdges;
                if (UnityEditor.EditorUtility.DisplayCancelableProgressBar(editorTitle + " - Generating closed mesh", "Expanding the mask to nearby vertices... " + (((float)processedEdges / edgeCount) * 100.0f).ToString("f0") + "%", (float)processedEdges / edgeCount))
                {
                    Debug.LogError("TryGenerateClosedMeshFromOpenMesh: Cancelled by user.");
                    break;
                }
            }
#endif

            // Get the first element
            id = maskedEdgesIdOpenSet[0];
            edge = edges[id];

            // Remove the element from the open set and add it to the closed set
            maskedEdgesIdClosedSet.Add(id);
            maskedEdgesIdOpenSet.RemoveAt(0);
            
#if UNITY_EDITOR
            if (debugTransform != null)
            {
                //Debug.DrawLine(debugTransform.TransformPoint(vertices[edge.ie0]), debugTransform.TransformPoint(vertices[edge.ie1]), Color.red, 10.0f);
                
                //Debug.DrawLine(debugTransform.TransformPoint(vertices[edge.ie0]), debugTransform.TransformPoint(vertices[edge.ie0]) + Vector3.up * 0.05f, Color.green, 10.0f);
                //Debug.DrawLine(debugTransform.TransformPoint(vertices[edge.ie1]), debugTransform.TransformPoint(vertices[edge.ie1]) + Vector3.up * 0.05f, Color.green, 10.0f);
            }
#endif

            // Find all neighbors
            for (i = 0; i < edgeCount; ++i)
            {
                // Skip if edge is same
                if (i == id)
                    continue;

                // Check if neighbor 
                if (edge.ie0 == edges[i].ie0 || edge.ie1 == edges[i].ie0 || edge.ie0 == edges[i].ie1 || edge.ie1 == edges[i].ie1)
                {
                    // Skip if edge closed or open edges
                    if (maskedEdgesIdClosedSet.Contains(i) || maskedEdgesIdOpenSet.Contains(i))
                        continue;

                    maskedEdgesIdOpenSet.Add(i);
                }
            }
        }

        // Get proper extrusion normals
        List<Vector3> extrusionNormals = new List<Vector3>();
        GenerateInflationNormals(vertices, triangles, edges, referenceVertexIdMap, ref extrusionNormals, thickness, debugTransform, editorTitle);

        // Extrude edges
        MaskedExtrude(ref vertices, ref uv0, ref triangles, edges, referenceVertexIdMap, maskedStartEdgesId, maskedEdgesIdClosedSet, extrusionNormals, thickness, editorTitle);

#if UNITY_EDITOR
        // Editor
        if (editorTitle != null)
            UnityEditor.EditorUtility.ClearProgressBar();
#endif

        // Set new
        return maskedEdgesIdClosedSet.Count == edges.Count ? MeshType.FullyOpen : MeshType.SemiOpen;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="vertices"></param>
    /// <param name="uv0"></param>
    /// <param name="triangles"></param>
    /// <param name="edges"></param>
    /// <param name="referenceVertexIdMap"></param>
    /// <param name="maskedStartEdgesId"></param>
    /// <param name="maskedEdgesIdClosedSet"></param>
    /// <param name="extrusionNormals"></param>
    /// <param name="thickness"></param>
    private static void MaskedExtrude (ref List<Vector3> vertices, ref List<Vector2> uv0, ref List<int> triangles, List<Edge> edges,
                                       Dictionary<int, int> referenceVertexIdMap, List<int> maskedStartEdgesId, List<int> maskedEdgesIdClosedSet,
                                       List<Vector3> extrusionNormals, float thickness, string editorTitle = null)
    {
#if UNITY_EDITOR
        // Editor
        if (editorTitle != null)
            UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Extrude vertices", "Startup...", 0.0f);
#endif

        // Cache
        Edge edge;
        int i, j, id, i0, i1, i2, it0, it1, it2;

        int vertexCount = vertices.Count;
        int triCount = triangles.Count;
        int maskedEdgesIdClosedSetCount = maskedEdgesIdClosedSet.Count;

        // Loop through all vertices and add the masked one
        bool[] isEdge = new bool[vertexCount];
        Dictionary<int, int> addedVerticesMap = new Dictionary<int, int>();
        int newVertexCount = vertexCount;
        for (i = 0; i < vertexCount; ++i)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Extrude vertices", "Creating new vertices from masked vertices...", (float)i / vertexCount);
#endif

            // Check if is in masked area
            referenceVertexIdMap.TryGetValue(i, out id);
            for (j = 0; j < maskedEdgesIdClosedSetCount; ++j)
            {
                edge = edges[maskedEdgesIdClosedSet[j]];
                if (edge.ie0 == id || edge.ie1 == id)
                {
                    // Add
                    vertices.Add(vertices[i] + extrusionNormals[i]);
                    uv0.Add(uv0[i] + Vector2.right);

                    // Keep track
                    addedVerticesMap.Add(i, newVertexCount);
                    isEdge[i] = maskedStartEdgesId.Contains(maskedEdgesIdClosedSet[j]);
                    ++newVertexCount;
                    break;
                }
            }
        }
        
        // Loop through all triangles and add if it is a masked vertex, also bridge the gap when encountering an edge
        for (i = 0; i < triCount; i += 3)
        {
#if UNITY_EDITOR
            if (editorTitle != null)
                UnityEditor.EditorUtility.DisplayProgressBar(editorTitle + " - Extrude vertices", "Bridging surfaces...", (float)i / triCount);
#endif

            // Cache
            it0 = triangles[i];
            it1 = triangles[i + 1];
            it2 = triangles[i + 2];

            // Duplicate triangles (inverted)
            if (addedVerticesMap.TryGetValue(it0, out i0) &&
                addedVerticesMap.TryGetValue(it1, out i1) &&
                addedVerticesMap.TryGetValue(it2, out i2))
            {
                triangles.Add(i2);
                triangles.Add(i1);
                triangles.Add(i0);
                
                // If it's an edge
                Bridge(ref vertices, ref uv0, ref triangles, referenceVertexIdMap, edges, maskedStartEdgesId, it0, it1, it2, i0, i1, i2, thickness);
            }
        }

#if UNITY_EDITOR
        // Editor
        if (editorTitle != null)
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vertices"></param>
    /// <param name="uv0"></param>
    /// <param name="triangles"></param>
    /// <param name="referenceVertexIdMap"></param>
    /// <param name="edges"></param>
    /// <param name="maskedStartEdgesId"></param>
    /// <param name="it0"></param>
    /// <param name="it1"></param>
    /// <param name="it2"></param>
    /// <param name="i0"></param>
    /// <param name="i1"></param>
    /// <param name="i2"></param>
    /// <param name="thickness"></param>
    private static void Bridge (ref List<Vector3> vertices, ref List<Vector2> uv0, ref List<int> triangles, Dictionary<int, int> referenceVertexIdMap,
                                List<Edge> edges, List<int> maskedStartEdgesId, int it0, int it1, int it2, int i0, int i1, int i2, float thickness)
    {
        // Get reference values
        referenceVertexIdMap.TryGetValue(it0, out int rit0);
        referenceVertexIdMap.TryGetValue(it1, out int rit1);
        referenceVertexIdMap.TryGetValue(it2, out int rit2);
        
        // Find the three edges related to the triangle inside of the start edges list
        for (int i = 0; i < maskedStartEdgesId.Count; ++i)
        {
            Edge edge = edges[maskedStartEdgesId[i]];
            if (edge.ie0 == rit0 && edge.ie1 == rit1)
                CreateBridge(ref vertices, ref uv0, ref triangles, it0, it1, i0, i1, thickness);
            else if (edge.ie1 == rit0 && edge.ie0 == rit1)
                CreateBridge(ref vertices, ref uv0, ref triangles, it1, it0, i1, i0, thickness);
            else if (edge.ie0 == rit1 && edge.ie1 == rit2)
                CreateBridge(ref vertices, ref uv0, ref triangles, it1, it2, i1, i2, thickness);
            else if (edge.ie1 == rit1 && edge.ie0 == rit2)
                CreateBridge(ref vertices, ref uv0, ref triangles, it2, it1, i2, i1, thickness);
            else if (edge.ie0 == rit2 && edge.ie1 == rit0)
                CreateBridge(ref vertices, ref uv0, ref triangles, it2, it0, i2, i0, thickness);
            else if (edge.ie1 == rit2 && edge.ie0 == rit0)
                CreateBridge(ref vertices, ref uv0, ref triangles, it0, it2, i0, i2, thickness);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vertices"></param>
    /// <param name="uv0"></param>
    /// <param name="triangles"></param>
    /// <param name="it0"></param>
    /// <param name="it1"></param>
    /// <param name="i0"></param>
    /// <param name="i1"></param>
    /// <param name="thickness"></param>
    private static void CreateBridge (ref List<Vector3> vertices, ref List<Vector2> uv0, ref List<int> triangles, int it0, int it1, int i0, int i1, float thickness)
    {
        int vertexCount = vertices.Count;

        vertices.Add(vertices[it0]);
        vertices.Add(vertices[it1]);
        vertices.Add(vertices[i0]);
        vertices.Add(vertices[i1]);

        float distX = (uv0[it0] - uv0[it1]).magnitude;
        float distY = math.abs(thickness * 10.0f);

        uv0.Add(Vector2.zero);
        uv0.Add(new Vector2(distX, 0.0f));
        uv0.Add(new Vector2(0.0f,  distY));
        uv0.Add(new Vector2(distX, distY));

        triangles.Add(vertexCount);
        triangles.Add(vertexCount + 2);
        triangles.Add(vertexCount + 1);

        triangles.Add(vertexCount + 2);
        triangles.Add(vertexCount + 3);
        triangles.Add(vertexCount + 1);
    }

    /// <summary>
    /// Check if a mesh is open or closed.
    /// </summary>
    /// <returns>Returns True is the mesh is open.</returns>
    public static bool IsOpenMesh (List<Edge> edges)
    {
        // Cache
        int edgeCount = edges.Count;

        // Go through each edge and check if one has only one face instead of two
        for (int i = 0; i < edgeCount; ++i)
        {
            if (edges[i].if1 == null)
                return true;
        }

        // Mesh is closed.
        return false;
    }
}
