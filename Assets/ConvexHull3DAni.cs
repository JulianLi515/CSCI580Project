using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ConvexHull3DAni : MonoBehaviour
{
    public MeshFilter meshFilter;
    public int pointCount = 100;
    public float pointRange = 5f;
    public float stepDelay = 1f;

    private List<Vector3> points;
    private List<Face> faces;
    private Dictionary<Face, List<int>> outsidePoints;

    private int iterationCount = 0;
    private float stepTimer = 0f;

    private Face currentFace;
    private int currentFurthest = -1;
    private HashSet<Face> currentVisibleFaces = new();
    private HashSet<(int, int)> currentHorizon = new();

    private bool isRunning = false;

    void Start()
    {
        points = GenerateRandomPoints(pointCount, pointRange);
        InitializeHull();
    }

    void Update()
    {
        if (!isRunning) return;

        stepTimer += Time.deltaTime;
        if (stepTimer >= stepDelay)
        {
            stepTimer = 0f;
            Step();
        }
    }

    void Step()
    {
        if (outsidePoints.Count == 0)
        {
            Debug.Log("Convex hull complete in " + iterationCount + " iterations.");
            isRunning = false;
            meshFilter.mesh = CreateMesh(faces);
            return;
        }

        iterationCount++;

        var entry = outsidePoints.First();
        currentFace = entry.Key;
        var pointIndices = entry.Value;

        float maxDist = float.NegativeInfinity;
        currentFurthest = -1;
        foreach (int pi in pointIndices)
        {
            float dist = Vector3.Dot(currentFace.normal, points[pi] - points[currentFace.a]);
            if (dist > maxDist)
            {
                maxDist = dist;
                currentFurthest = pi;
            }
        }

        currentVisibleFaces.Clear();
        foreach (var f in faces)
        {
            if (f.IsPointAbove(points[currentFurthest], points))
                currentVisibleFaces.Add(f);
        }

        currentHorizon.Clear();
        foreach (var vf in currentVisibleFaces)
        {
            AddHorizonEdge(currentHorizon, vf.a, vf.b);
            AddHorizonEdge(currentHorizon, vf.b, vf.c);
            AddHorizonEdge(currentHorizon, vf.c, vf.a);
        }

        foreach (var vf in currentVisibleFaces)
            faces.Remove(vf);

        foreach (var edge in currentHorizon)
            faces.Add(new Face(edge.Item1, edge.Item2, currentFurthest, points));

        outsidePoints.Clear();
        for (int i = 0; i < points.Count; i++)
        {
            foreach (var f in faces)
            {
                if (f.IsPointAbove(points[i], points))
                {
                    if (!outsidePoints.ContainsKey(f)) outsidePoints[f] = new();
                    outsidePoints[f].Add(i);
                    break;
                }
            }
        }
    }

    void InitializeHull()
    {
        faces = new();
        outsidePoints = new();
        iterationCount = 0;
        stepTimer = 0f;
        isRunning = true;

        int[] initial = FindInitialTetrahedron(points);
        if (initial == null) { isRunning = false; return; }

        faces.Add(new Face(initial[0], initial[1], initial[2], points));
        faces.Add(new Face(initial[0], initial[3], initial[1], points));
        faces.Add(new Face(initial[0], initial[2], initial[3], points));
        faces.Add(new Face(initial[1], initial[3], initial[2], points));

        for (int i = 0; i < points.Count; i++)
        {
            if (initial.Contains(i)) continue;
            foreach (var f in faces)
            {
                if (f.IsPointAbove(points[i], points))
                {
                    if (!outsidePoints.ContainsKey(f)) outsidePoints[f] = new();
                    outsidePoints[f].Add(i);
                }
            }
        }
    }

    public List<Vector3> GenerateRandomPoints(int count, float range)
    {
        var list = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new Vector3(
                Random.Range(-range, range),
                Random.Range(-range, range),
                Random.Range(-range, range)));
        }
        return list;
    }

    void AddHorizonEdge(HashSet<(int, int)> set, int a, int b)
    {
        if (!set.Remove((b, a)))
            set.Add((a, b));
    }

    Mesh CreateMesh(List<Face> hullFaces)
    {
        Mesh mesh = new Mesh();
        List<Vector3> verts = new();
        List<int> tris = new();
        Dictionary<int, int> map = new();
        int next = 0;
        foreach (var face in hullFaces)
        {
            foreach (int idx in new int[] { face.a, face.b, face.c })
            {
                if (!map.ContainsKey(idx))
                {
                    map[idx] = next++;
                    verts.Add(points[idx]);
                }
                tris.Add(map[idx]);
            }
        }
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    void OnDrawGizmos()
    {
        if (points == null) return;

        Gizmos.color = Color.red;
        foreach (var p in points)
        {
            Gizmos.DrawSphere(p, 0.05f);
        }

        Gizmos.color = Color.gray;
        if (faces != null)
        {
            foreach (var f in faces)
            {
                if (!currentVisibleFaces.Contains(f))
                    DrawTriangle(points[f.a], points[f.b], points[f.c]);
            }
        }

        if (currentFurthest != -1)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(points[currentFurthest], 0.1f);
        }

        Gizmos.color = new Color(1f, 0.4f, 0.8f, 0.5f); // pink for visible faces
        foreach (var f in currentVisibleFaces)
        {
            DrawTriangle(points[f.a], points[f.b], points[f.c]);
        }

        Gizmos.color = Color.blue; // blue for horizon
        foreach (var edge in currentHorizon)
        {
            Gizmos.DrawLine(points[edge.Item1], points[edge.Item2]);
        }
    }

    void DrawTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, a);
    }

    public struct Face
    {
        public int a, b, c;
        public Vector3 normal;

        public Face(int a, int b, int c, List<Vector3> pts)
        {
            this.a = a; this.b = b; this.c = c;
            normal = Vector3.Cross(pts[b] - pts[a], pts[c] - pts[a]).normalized;
        }

        public bool IsPointAbove(Vector3 p, List<Vector3> pts)
        {
            return Vector3.Dot(normal, p - pts[a]) > 1e-5f;
        }
    }

    int[] FindInitialTetrahedron(List<Vector3> points)
    {
        // piont a is just first point in the list
        int a = 0;
        // point b is the furthest point from the first
        int b = FindFurthestPoint(points, a);
        // point c is the point that maximizes the triangle area abc
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

        // point d is the point that maximizes the volume of tetrahedron abcd
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
}
