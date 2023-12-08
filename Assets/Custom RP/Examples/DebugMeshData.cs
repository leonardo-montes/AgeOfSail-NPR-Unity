using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugMeshData : MonoBehaviour
{
    [SerializeField] private bool m_debug = false;
    [SerializeField] private float m_VertexNormalLength = 1.0f;
    [SerializeField] private float m_VertexTangentLength = 1.0f;
    [SerializeField] private float m_VertexBinormalLength = 1.0f;

    void OnDrawGizmos()
    {
        if (!m_debug)
            return;

        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;

        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            Vector3 vertexPosWS = transform.TransformPoint(mesh.vertices[i]);
            Vector3 vertexNormalWS = transform.TransformDirection(mesh.normals[i]);
            Vector3 vertexTangentWS = transform.TransformDirection(mesh.tangents[i]);
            Vector3 vertexBinormalWS = (Vector3.Cross(vertexNormalWS, vertexTangentWS) * mesh.tangents[i].w).normalized;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(vertexPosWS, vertexPosWS + vertexNormalWS * m_VertexNormalLength);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(vertexPosWS, vertexPosWS + vertexTangentWS * m_VertexTangentLength);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(vertexPosWS, vertexPosWS + vertexBinormalWS * m_VertexBinormalLength);
        }
    }
}
