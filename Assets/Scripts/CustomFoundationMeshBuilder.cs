using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class FoundationData
{
    public GameObject FoundationPtGo;
    public float FoundationPtHeight;
}

public class CustomFoundationMeshBuilder : MonoBehaviour
{
    public List<FoundationData> FoundationVerticesList = new List<FoundationData>();

    private Mesh _mesh;
    private Vector3[] _vertices;
    private int[] _triangles;

    void Start()
    {
    }

    void Update()
    {
        GenerateFoundationAndWall();
        // GenerateFoundation();
    }

    private void GenerateFoundationAndWall()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = $"TestGrid_{Time.time}";
            GetComponent<MeshFilter>().mesh = _mesh;
            GetComponent<MeshCollider>().sharedMesh = _mesh;
        }

        int foundationPtCount = FoundationVerticesList.Count;
        _vertices = new Vector3[FoundationVerticesList.Count * 2];

        for (int i = 0; i < foundationPtCount; i++)
        {
            // foundation
            Vector3 pt = FoundationVerticesList[i].FoundationPtGo.transform.position;
            _vertices[i] = transform.InverseTransformPoint(pt);
            // ceiling
            _vertices[i + foundationPtCount] = transform.InverseTransformPoint(pt + Vector3.up * FoundationVerticesList[i].FoundationPtHeight);
        }

        _mesh.vertices = _vertices;

        // foundation + wall
        _triangles = new int[(foundationPtCount - 2 + foundationPtCount) * 2 * 3];
        int triIterIdx = 0;
        // foundation tri
        for (int i = 0; i < foundationPtCount - 2; i++, triIterIdx += 3)
        {
            _triangles[triIterIdx] = 0;
            _triangles[triIterIdx + 1] = i + 1;
            _triangles[triIterIdx + 2] = i + 2;
        }

        // wall tri
        for (int i = 0; i < foundationPtCount; i++, triIterIdx += 6)
        {
            int nextI = (i + 1) % foundationPtCount;
            // Fst tri
            _triangles[triIterIdx] = i;
            _triangles[triIterIdx + 1] = i + foundationPtCount;
            _triangles[triIterIdx + 2] = nextI;
            // Snd tri
            _triangles[triIterIdx + 3] = nextI;
            _triangles[triIterIdx + 4] = i + foundationPtCount;
            _triangles[triIterIdx + 5] = nextI + foundationPtCount;
        }

        // ceiling tri
        for (int i = 0; i < foundationPtCount - 2; i++, triIterIdx += 3)
        {
            _triangles[triIterIdx] = foundationPtCount;
            _triangles[triIterIdx + 1] = i + 2 + foundationPtCount;
            _triangles[triIterIdx + 2] = i + 1 + foundationPtCount;
        }

        _mesh.triangles = _triangles;
        _mesh.RecalculateNormals();
    }

    private void GenerateFoundation()
    {
        GetComponent<MeshFilter>().mesh = _mesh = new Mesh();
        _mesh.name = $"TestGrid_{Time.time}";
        int foundationPtCount = FoundationVerticesList.Count;
        _vertices = new Vector3[FoundationVerticesList.Count];
        for (int i = 0; i < foundationPtCount; i++)
        {
            _vertices[i] = FoundationVerticesList[i].FoundationPtGo.transform.position;
        }

        _mesh.vertices = _vertices;

        int[] triangles = new int[(foundationPtCount - 2) * 3];
        for (int i = 0, ti = 0; i < foundationPtCount - 2; i++, ti += 3)
        {
            triangles[ti] = 0;
            triangles[ti + 1] = i + 1;
            triangles[ti + 2] = i + 2;
        }
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
    }

    private void OnDrawGizmos()
    {
        if (_vertices == null)
        {
            return;
        }

        Gizmos.color = Color.black;

        for (int i = 0; i < _vertices.Length; i++)
        {
            Vector3 pt = transform.TransformPoint(_vertices[i]);
            Gizmos.DrawSphere(pt, 0.02f);
            Handles.Label(pt, $"{i}");
        }

        for (int i = 0; i < _triangles.Length; i += 3)
        {
            int i0 = _triangles[i];
            int i1 = _triangles[i + 1];
            int i2 = _triangles[i + 2];

            Vector3 v0 = transform.TransformPoint(_vertices[i0]);
            Vector3 v1 = transform.TransformPoint(_vertices[i1]);
            Vector3 v2 = transform.TransformPoint(_vertices[i2]);

            Gizmos.DrawLine(v0, v1);
            Gizmos.DrawLine(v1, v2);
            Gizmos.DrawLine(v2, v0);
        }
    }
}
