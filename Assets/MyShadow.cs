/// group memebers:
/// lijulian@usc.edu
/// jfu03326@usc.edu


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class MyShadows : MonoBehaviour
{
    [SerializeField] private Transform shadowTransform;
    // only support directional light for now
    [SerializeField] private Transform lightTransform;

    [SerializeField] private LayerMask targetLayerMask;

    [SerializeField] private PolygonCollider2D groundCollider;

    [SerializeField] private MeshCollider meshCollider;

    private Vector3[] objectVertices;

    private Vector3[] shadowPoints;

    private Vector2[] shadowPoints3Dto2D;

    Mesh GeneratedMesh;



    // variables for optimization
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private Vector3 previousScale;

    private bool canUpdateCollider = true;

    [SerializeField][Range(0.02f, 1f)] private float shadowColliderUpdateTime = 0.08f;




    private void Awake()
    {
        objectVertices = transform.GetComponent<MeshFilter>().mesh.vertices.Distinct().ToArray();
    }

    private void Update()
    {
        shadowTransform.position = transform.position;
    }

    private void FixedUpdate()
    {
        //optimization here to only check if object transform has changed and not every frame

        if (TransformHasChanged() && canUpdateCollider)
        {
            Invoke("UpdateShadowCollider", shadowColliderUpdateTime);
            canUpdateCollider = false;
        }

        previousPosition = transform.position;
        previousRotation = transform.rotation;
        previousScale = transform.localScale;

        //Invoke("UpdateShadowCollider", shadowColliderUpdateTime);
    }





    /// <summary>
    /// Updates the shadow collider by computing the shadow points based on the object's vertices and the light's direction.
    /// </summary>
    private void UpdateShadowCollider()
    {
        ComputeShadowColliderMeshVertices();


        if (shadowPoints == null) return;

        shadowPoints3Dto2D = new Vector2[shadowPoints.Length];

        for (int i = 0; i < shadowPoints.Length; i++)
        {
            // Compute world coordinate
            Vector3 currShadowPoint = shadowTransform.TransformPoint(shadowPoints[i]);

            // Zero y value
            currShadowPoint.y = 0;

            // Convert to 2D
            shadowPoints3Dto2D[i] = new Vector2(currShadowPoint.x, currShadowPoint.z);
        }


        // create a new path in the 2d ground collider and then create 3d mesh from it
        // To be implemented
        groundCollider.pathCount = 2;
        groundCollider.SetPath(1, shadowPoints3Dto2D.ToList());
        Make3DMesh();

        canUpdateCollider = true;


    }





    /// <summary>
    /// Computes the shadow collider mesh vertices based on the object's vertices and the light's direction.
    /// result is stored in shadowPoints for debug draw and later use
    /// </summary>
    /// <returns></returns>
    private void ComputeShadowColliderMeshVertices()
    {
        shadowPoints = new Vector3[objectVertices.Length];

        Vector3 raycastDirection = lightTransform.forward;

        int n = objectVertices.Length;

        // Loop through each vertex of the object and compute prjection point on the ground
        for (int i = 0; i < n; i++)
        {
            Vector3 point = transform.TransformPoint(objectVertices[i]);

            shadowPoints[i] = ComputeIntersectionPoint(point, raycastDirection);
        }

        // Remove interior points and order vertices using convex hull
        shadowPoints = RemoveInteriorPointsAndOrder(shadowPoints);
    }

    /// <summary>
    /// Computes the intersection point of a ray from the object's vertex in the direction of the light source.
    /// targetLayerMask should be ground layer
    /// </summary>
    /// <param name="fromPosition"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    private Vector3 ComputeIntersectionPoint(Vector3 fromPosition, Vector3 direction)
    {
        RaycastHit hit;

        if (Physics.Raycast(fromPosition, direction, out hit, Mathf.Infinity, targetLayerMask))
        {
            return hit.point - transform.position;
        }

        return fromPosition + 100 * direction - transform.position;
    }


    /// <summary>
    /// Removes interior points and orders the points in a clockwise manner using a convex hull algorithm.
    /// </summary>
    /// <param name="points"></param>
    /// <returns></returns>
    private Vector3[] RemoveInteriorPointsAndOrder(Vector3[] points)
    {
        // converted to 2d in here for simplicity 
        Vector2[] projectedPoints = points.Select(p => new Vector2(p.x, p.z)).ToArray();

        // Use a convex hull algorithm to find the outline points
        List<Vector2> hull = ComputeConvexHull(projectedPoints);

        // Convert the 2D hull points back to 3D
        Vector3[] orderedPoints = hull.Select(p => new Vector3(p.x, 0, p.y)).ToArray();

        return orderedPoints;
    }

    /// <summary>
    /// Computes the convex hull of a set of 2D points using Graham's scan algorithm.
    /// time complexity is O(n^2)
    /// </summary>
    /// <param name="points"></param>
    /// <returns></returns>
    private List<Vector2> ComputeConvexHull(Vector2[] points)
    {
        List<Vector2> hull = new List<Vector2>();

        // left most point as start  
        Vector2 start = points.OrderBy(p => p.y).ThenBy(p => p.x).First();

        // Sort points by polar angle with respect to the start point  
        var sortedPoints = points.OrderBy(p => Mathf.Atan2(p.y - start.y, p.x - start.x)).ToList();

        foreach (var point in sortedPoints)
        {
            while (hull.Count >= 2 && CrossProduct(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(point);
        }

        // start point is added back to close the hull
        // not doing this cause a bug that start is removed sometimes when object is parallel to y axis
        if (!hull.Contains(start))
        {
            hull.Add(start);
        }

        return hull;
    }

    /// <summary>
    /// cross product utility
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    /// <returns></returns>
    private float CrossProduct(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }


    /// <summary>
    /// Checks if the transform of the object has changed since the last update. used for optimization
    /// </summary>
    /// <returns></returns>
    private bool TransformHasChanged()
    {
        return previousPosition != transform.position || previousRotation != transform.rotation || previousScale != transform.localScale;
    }

    /// <summary>
    /// Creates a 3D mesh from the ground collider and assigns it to the mesh collider.
    /// </summary>
    private void Make3DMesh()
    {
        if (GeneratedMesh != null) Destroy(GeneratedMesh);
        GeneratedMesh = groundCollider.CreateMesh(true, true);
        meshCollider.sharedMesh = GeneratedMesh;
    }


    /// <summary>
    /// Draws the resulting shadow points after convex hull in 3D  in the scene view for debugging purposes.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (shadowPoints == null) return;

        Gizmos.color = Color.red;
        //Debug.Log(shadowPoints.Length);
        foreach (var point in shadowPoints)
        {
            Vector3 currShadowPoint = shadowTransform.TransformPoint(point);
            currShadowPoint.y = 0;
            Gizmos.DrawSphere(currShadowPoint, 0.05f);
        }
    }


}
