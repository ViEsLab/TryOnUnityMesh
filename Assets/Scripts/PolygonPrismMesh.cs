using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class PolygonPrismMesh : MonoBehaviour
{
    [System.Serializable]
    public class PrismPoint
    {
        public Transform Point;
        public float BottomY = 0f;
        public float TopY = 2f;
    }

    public List<PrismPoint> Points = new List<PrismPoint>();
    private Mesh _mesh;

    private void Update()
    {
        Generate();
    }

    private void Generate()
    {
        if (Points == null || Points.Count < 3)
            return;

        EnsureMesh();

        int pointCount = Points.Count;
        Vector3[] vertices = new Vector3[pointCount * 2];
        List<Vector2> polygon2D = new List<Vector2>();

        // vertices
        for (int i = 0; i < pointCount; i++)
        {
            PrismPoint p = Points[i];
            Vector3 localPos = transform.InverseTransformPoint(p.Point.position);
            Vector2 xz = new Vector2(localPos.x, localPos.z);
            polygon2D.Add(xz);

            // Bottom
            vertices[i] = new Vector3(xz.x, p.BottomY, xz.y);

            // Top
            vertices[i + pointCount] = new Vector3(xz.x, p.TopY, xz.y);
        }

        List<int> baseTriangles = Triangulate(polygon2D);
        List<int> triangles = new List<int>();

        // bottom
        for (int i = 0; i < baseTriangles.Count; i += 3)
        {
            triangles.Add(baseTriangles[i + 1]);
            triangles.Add(baseTriangles[i + 2]);
            triangles.Add(baseTriangles[i]);
        }

        // ceiling
        for (int i = 0; i < baseTriangles.Count; i += 3)
        {
            triangles.Add(baseTriangles[i] + pointCount);
            triangles.Add(baseTriangles[i + 2] + pointCount);
            triangles.Add(baseTriangles[i + 1] + pointCount);
        }

        // wall
        for (int i = 0; i < pointCount; i++)
        {
            int next = (i + 1) % pointCount;

            int b0 = i;
            int b1 = next;

            int t0 = i + pointCount;
            int t1 = next + pointCount;

            // fst tri
            triangles.Add(b0);
            triangles.Add(t0);
            triangles.Add(b1);

            // snd tri
            triangles.Add(b1);
            triangles.Add(t0);
            triangles.Add(t1);
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.triangles = triangles.ToArray();

        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        MeshCollider collider = GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = _mesh;
    }

    private void EnsureMesh()
    {
        if (_mesh != null)
            return;

        _mesh = new Mesh();
        _mesh.name = "PolygonPrismMesh";

        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }

    // 用 Ear Clipping 的测试构建
    private List<int> Triangulate(List<Vector2> polygon)
    {
        List<int> triangles = new List<int>();
        int n = polygon.Count;
        if (n < 3)
            return triangles;

        List<int> indices = new List<int>();

        // 确保顶点顺序为逆时针，以便正确识别凸点和凹点
        if (IsClockwise(polygon))
        {
            for (int i = n - 1; i >= 0; i--)
                indices.Add(i);
        }
        else
        {
            for (int i = 0; i < n; i++)
                indices.Add(i);
        }

        int guard = 0;
        while (indices.Count > 3 && guard < 5000)
        {
            guard++;
            bool earFound = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int prevIndex = indices[(i - 1 + indices.Count) % indices.Count];
                int currIndex = indices[i];
                int nextIndex = indices[(i + 1) % indices.Count];

                Vector2 a = polygon[prevIndex];
                Vector2 b = polygon[currIndex];
                Vector2 c = polygon[nextIndex];

                if (!IsConvex(a, b, c))
                    continue;

                bool containsPoint = false;

                for (int j = 0; j < indices.Count; j++)
                {
                    int testIndex = indices[j];
                    if (testIndex == prevIndex || testIndex == currIndex || testIndex == nextIndex)
                        continue;

                    if (PointInTriangle(polygon[testIndex], a, b, c))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (containsPoint)
                    continue;

                triangles.Add(prevIndex);
                triangles.Add(currIndex);
                triangles.Add(nextIndex);

                indices.RemoveAt(i);

                earFound = true;
                break;
            }

            if (!earFound)
            {
                Debug.LogError($"存在自相交？");
                return triangles;
            }
        }

        if (indices.Count == 3)
        {
            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
        }

        return triangles;
    }

    private bool IsClockwise(List<Vector2> polygon)
    {
        float sum = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];

            sum += (b.x - a.x) * (b.y + a.y);
        }

        return sum > 0f;
    }

    private bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(b - a, c - b) > 0f;
    }

    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
        bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

        return !(hasNeg && hasPos);
    }

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y)
             - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private void OnDrawGizmos()
    {
        if (_mesh == null)
            return;

        Gizmos.color = Color.black;

        Vector3[] verts = _mesh.vertices;
        int[] tris = _mesh.triangles;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = transform.TransformPoint(verts[tris[i]]);
            Vector3 b = transform.TransformPoint(verts[tris[i + 1]]);
            Vector3 c = transform.TransformPoint(verts[tris[i + 2]]);

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, a);
        }

        Gizmos.color = Color.red;

        for (int i = 0; i < Points.Count; i++)
        {
            PrismPoint p = Points[i];
            if (p.Point != null)
            {
                Vector3 pt = p.Point.position;
                Gizmos.DrawSphere(pt, 0.02f);
            }
        }

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 pt = transform.TransformPoint(verts[i]);
            Handles.Label(pt, $"{i}");
        }
    }
}