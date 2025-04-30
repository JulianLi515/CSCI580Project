using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class QuickHull3D : MonoBehaviour
{
    public MeshFilter meshFilter;
    public int pointCount = 100;
    public float pointRange = 5f;

    private List<Vector3> debugPoints = new List<Vector3>();
    private List<Face> currentHull = new List<Face>();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            debugPoints = GenerateRandomPoints(pointCount, pointRange);
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            currentHull = ComputeConvexHull(debugPoints);
            var mesh = CreateMesh(currentHull);
            meshFilter.mesh = mesh;
        }
    }

    public List<Vector3> GenerateRandomPoints(int count, float range)
    {
        var points = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            points.Add(new Vector3(
                Random.Range(-range, range),
                Random.Range(-range, range),
                Random.Range(-range, range)));
        }
        return points;
    }

    public struct Face
    {
        public int a, b, c;
        public Vector3 normal;

        public Face(int a, int b, int c, List<Vector3> vertices)
        {
            this.a = a; this.b = b; this.c = c;
            normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
        }

        public bool IsPointAbove(Vector3 point, List<Vector3> vertices)
        {
            return Vector3.Dot(normal, point - vertices[a]) > 1e-5f;
        }
    }

    public List<Face> ComputeConvexHull(List<Vector3> points)
    {
        List<Face> faces = new List<Face>();
        if (points.Count < 4) return faces;

        int[] initial = FindInitialTetrahedron(points);
        if (initial == null) return faces;

        faces.Add(new Face(initial[0], initial[1], initial[2], points));
        faces.Add(new Face(initial[0], initial[3], initial[1], points));
        faces.Add(new Face(initial[0], initial[2], initial[3], points));
        faces.Add(new Face(initial[1], initial[3], initial[2], points));

        var faceQueue = new Queue<Face>(faces);
        var outsidePoints = new Dictionary<Face, List<int>>();

        foreach (var pointIndex in System.Linq.Enumerable.Range(0, points.Count))
        {
            if (initial.Contains(pointIndex)) continue;
            foreach (var face in faces)
            {
                if (face.IsPointAbove(points[pointIndex], points))
                {
                    if (!outsidePoints.ContainsKey(face)) outsidePoints[face] = new List<int>();
                    outsidePoints[face].Add(pointIndex);
                }
            }
        }

        while (outsidePoints.Count > 0)
        {
            var entry = outsidePoints.First();
            var face = entry.Key;
            var pointIndices = entry.Value;

            float maxDist = float.NegativeInfinity;
            int furthest = -1;
            foreach (int pi in pointIndices)
            {
                float dist = Vector3.Dot(face.normal, points[pi] - points[face.a]);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    furthest = pi;
                }
            }

            var visibleFaces = new HashSet<Face>();
            foreach (var f in faces)
            {
                if (f.IsPointAbove(points[furthest], points))
                    visibleFaces.Add(f);
            }

            var horizonEdges = new HashSet<(int, int)>();
            foreach (var vf in visibleFaces)
            {
                AddHorizonEdge(horizonEdges, vf.a, vf.b);
                AddHorizonEdge(horizonEdges, vf.b, vf.c);
                AddHorizonEdge(horizonEdges, vf.c, vf.a);
            }

            foreach (var vf in visibleFaces)
                faces.Remove(vf);

            foreach (var edge in horizonEdges)
                faces.Add(new Face(edge.Item1, edge.Item2, furthest, points));

            outsidePoints.Clear();
            foreach (var pointIndex in System.Linq.Enumerable.Range(0, points.Count))
            {
                bool assigned = false;
                foreach (var newFace in faces)
                {
                    if (newFace.IsPointAbove(points[pointIndex], points))
                    {
                        if (!outsidePoints.ContainsKey(newFace)) outsidePoints[newFace] = new List<int>();
                        outsidePoints[newFace].Add(pointIndex);
                        assigned = true;
                        break;
                    }
                }
            }
        }

        return faces;
    }

    int[] FindInitialTetrahedron(List<Vector3> points)
    {
        int a = 0;
        int b = FindFurthestPoint(points, a);
        int c = -1;
        float maxArea = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            if (i == a || i == b) continue;
            float area = Vector3.Cross(points[b] - points[a], points[i] - points[a]).magnitude;
            if (area > maxArea)
            {
                maxArea = area;
                c = i;
            }
        }
        if (c == -1) return null;

        int d = -1;
        float maxVolume = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            if (i == a || i == b || i == c) continue;
            float vol = Mathf.Abs(Vector3.Dot(points[i] - points[a], Vector3.Cross(points[b] - points[a], points[c] - points[a])));
            if (vol > maxVolume)
            {
                maxVolume = vol;
                d = i;
            }
        }
        if (d == -1) return null;
        return new int[] { a, b, c, d };
    }

    int FindFurthestPoint(List<Vector3> points, int from)
    {
        float maxDist = 0f;
        int index = -1;
        for (int i = 0; i < points.Count; i++)
        {
            if (i == from) continue;
            float dist = Vector3.Distance(points[from], points[i]);
            if (dist > maxDist)
            {
                maxDist = dist;
                index = i;
            }
        }
        return index;
    }

    void AddHorizonEdge(HashSet<(int, int)> set, int a, int b)
    {
        if (!set.Remove((b, a)))
            set.Add((a, b));
    }

    Mesh CreateMesh(List<Face> faces)
    {
        var mesh = new Mesh();
        var verts = new List<Vector3>();
        var tris = new List<int>();

        Dictionary<int, int> indexMap = new Dictionary<int, int>();
        int nextIndex = 0;

        foreach (var face in faces)
        {
            foreach (int idx in new int[] { face.a, face.b, face.c })
            {
                if (!indexMap.ContainsKey(idx))
                {
                    indexMap[idx] = nextIndex++;
                    verts.Add(debugPoints[idx]);
                }
                tris.Add(indexMap[idx]);
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    void OnDrawGizmos()
    {
        if (debugPoints == null) return;
        Gizmos.color = Color.red;
        foreach (var point in debugPoints)
        {
            Gizmos.DrawSphere(point, 0.1f);
        }
    }
}
