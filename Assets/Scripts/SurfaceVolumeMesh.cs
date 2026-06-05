using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class SurfaceVolumeMesh : MonoBehaviour
{
    [System.Serializable]
    public class SurfacePoint
    {
        public Transform Point;
        public bool Boundary = true;
        public float TopHeight = 2f;
        public float BottomHeight = 0f;
    }

    public List<SurfacePoint> Points = new List<SurfacePoint>();
    public List<int> BoundaryOrder = new List<int>();

    private Mesh _mesh;

    private void Update()
    {
        Generate();
    }

    private void Generate()
    {
        if (BoundaryOrder.Count < 3)
            return;

        EnsureMesh();

        int pointCount = Points.Count;

        List<Vector3> vertices = new();
        // bottom
        for (int i = 0; i < pointCount; i++)
        {
            SurfacePoint p = Points[i];
            Vector3 local = transform.InverseTransformPoint(p.Point.position);
            vertices.Add(new Vector3(local.x, p.BottomHeight, local.z));
        }

        // ceiling
        for (int i = 0; i < pointCount; i++)
        {
            SurfacePoint p = Points[i];
            Vector3 local = transform.InverseTransformPoint(p.Point.position);
            vertices.Add(new Vector3(local.x, p.TopHeight, local.z));
        }

        List<int> triangles = new List<int>();
        BuildTopSurface(triangles, pointCount);

        int topTriCount = triangles.Count;
        for (int i = 0; i < topTriCount; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            triangles.Add(c - pointCount);
            triangles.Add(b - pointCount);
            triangles.Add(a - pointCount);
        }

        // wall
        BuildWalls(triangles, pointCount);

        _mesh.Clear();
        _mesh.vertices = vertices.ToArray();
        _mesh.triangles = triangles.ToArray();
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        MeshCollider mc = GetComponent<MeshCollider>();
        mc.sharedMesh = null;
        mc.sharedMesh = _mesh;
    }

    private void BuildTopSurface(List<int> triangles, int pointCount)
    {
        if (BoundaryOrder.Count == Points.Count)
        {
            // 处理边界点
            List<Vector2> polygon = new List<Vector2>();

            foreach (int idx in BoundaryOrder)
            {
                Vector3 local = transform.InverseTransformPoint(Points[idx].Point.position);
                polygon.Add(new Vector2(local.x, local.z));
            }

            // Ear clipping
            List<int> boundaryTris = Triangulate(polygon);
            // 三角填充
            for (int i = 0; i < boundaryTris.Count; i += 3)
            {
                int a = BoundaryOrder[boundaryTris[i]] + pointCount;
                int b = BoundaryOrder[boundaryTris[i + 1]] + pointCount;
                int c = BoundaryOrder[boundaryTris[i + 2]] + pointCount;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        // 处理内部点
        // TODO: [ViE] 考虑连接方式
        for (int i = 0; i < Points.Count; i++)
        {
            if (Points[i].Boundary)
                continue;

            ConnectInteriorPoint(triangles, i, pointCount);
        }
    }

    private void ConnectInteriorPoint(List<int> triangles, int interiorIndex, int pointCount)
    {
        Vector3 local = transform.InverseTransformPoint(Points[interiorIndex].Point.position);
        Vector2 center = new Vector2(local.x, local.z);

        List<(float angle, int idx)> sorted = new List<(float angle, int idx)>();
        foreach (int boundaryIdx in BoundaryOrder)
        {
            Vector3 bLocal = transform.InverseTransformPoint(Points[boundaryIdx].Point.position);
            Vector2 dir = new Vector2(bLocal.x, bLocal.z) - center;
            float angle = Mathf.Atan2(dir.y, dir.x);
            sorted.Add((angle, boundaryIdx));
        }

        sorted.Sort((a, b) => a.angle.CompareTo(b.angle));

        for (int i = 0; i < sorted.Count; i++)
        {
            int next = (i + 1) % sorted.Count;

            triangles.Add(interiorIndex + pointCount);
            triangles.Add(sorted[next].idx + pointCount);
            triangles.Add(sorted[i].idx + pointCount);
        }
    }

    private void BuildWalls(List<int> triangles, int pointCount)
    {
        for (int i = 0; i < BoundaryOrder.Count; i++)
        {
            int next = (i + 1) % BoundaryOrder.Count;

            int b0 = BoundaryOrder[i];
            int b1 = BoundaryOrder[next];

            int t0 = b0 + pointCount;
            int t1 = b1 + pointCount;

            // fst tri
            triangles.Add(b0);
            triangles.Add(t0);
            triangles.Add(b1);

            //snd tri
            triangles.Add(b1);
            triangles.Add(t0);
            triangles.Add(t1);
        }
    }

    // 用 Ear Clipping 的测试构建
    private List<int> Triangulate(List<Vector2> polygon)
    {
        List<int> triangles = new List<int>();
        int n = polygon.Count;
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
                    int test = indices[j];
                    if (test == prevIndex || test == currIndex || test == nextIndex)
                        continue;

                    if (PointInTriangle(polygon[test], a, b, c))
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

        bool neg = d1 < 0 || d2 < 0 || d3 < 0;
        bool pos = d1 > 0 || d2 > 0 || d3 > 0;

        return !(neg && pos);
    }

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private void EnsureMesh()
    {
        if (_mesh != null)
            return;

        _mesh = new Mesh();
        _mesh.name = "SurfaceVolumeMesh";

        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }

    private void OnDrawGizmos()
    {
        if (_mesh == null)
            return;

        Vector3[] verts = _mesh.vertices;
        int[] tris = _mesh.triangles;

        Gizmos.color = Color.green;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = transform.TransformPoint(verts[tris[i]]);
            Vector3 b = transform.TransformPoint(verts[tris[i + 1]]);
            Vector3 c = transform.TransformPoint(verts[tris[i + 2]]);

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, a);
        }

        foreach (var p in Points)
        {
            if (p.Point == null)
                continue;

            Gizmos.color = p.Boundary ? Color.red : Color.blue;
            Gizmos.DrawSphere(p.Point.position, 0.02f);
        }

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 pt = transform.TransformPoint(verts[i]);
            Handles.Label(pt, $"{i}");
        }
    }
}