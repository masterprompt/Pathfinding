using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;


namespace Endboss.Navigation
{
    [AddComponentMenu("Endboss/Navigation/Waypoint")]
    public class Waypoint : MonoBehaviour
    {

        

        #region Properties
        public float radius = 1;
        [System.NonSerialized]
        public Transform Transform;
        #endregion

        #region MonoBehaviour
        void Awake()
        {
            this.Transform = this.transform;
        }
        public void DrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
        #endregion

        #region Visiblity
        public static bool IsVisible(Waypoint a, Waypoint b, LayerMask layerMask)
        {
            return !Physics.Linecast(a.transform.position, b.transform.position, layerMask);
        }
        public static float VisibilityWidth(Waypoint a, Waypoint b, float width, float minDistance, LayerMask layerMask)
        {
            do
            {
                Ray ray = new Ray(a.transform.position, (b.transform.position-a.transform.position).normalized);
                float distance = Vector3.Distance(a.transform.position,b.transform.position);

                if (!Physics.SphereCast(ray, (width * 0.5f) + (minDistance * 0.5f), distance, layerMask))
                    return width - (minDistance);

                width -= 0.2f;
            } while (width >= minDistance);
            return width;
        }
        #endregion

        #region IsInside
        public bool IsInside(Vector3 p)
        {
            return ((this.Transform.position - p).sqrMagnitude - Math.Pow(radius, 2)) <= 0;
        }
        #endregion

        #region EdgePoint
        public Vector3 EdgePoint(Vector3 p)
        {
            return this.Transform.position + ((p - this.Transform.position).normalized * (radius - 0.05f));
        }
        #endregion


        #region Chord
        public class Chord
        {
            public Vector3 pointA
            {
                get { return points[0]; }
            }
            public Vector3 pointB
            {
                get { return points[1]; }
            }
            public Vector3[] points = new Vector3[0];
            public float amplitude;
            public Chord(Vector3 origin, float radius, Vector3 direction, float length)
            {
                //  We can get chord points by using an isosceles triangle
                //  http://www.mathopenref.com/isosceles.html

                //  get our leg length
                float L = radius;

                //  Get our base length
                float B = length;

                //  Get the altitude
                float A = Mathf.Sqrt(Mathf.Pow(L, 2) - Mathf.Pow(B / 2, 2));
                amplitude = A;

                //  Now get our 2 normals
                Vector3 normalA = Vector3.Cross(direction, Vector3.up);
                Vector3 normalB = Vector3.Cross(direction, Vector3.down);

                points = new Vector3[2];

                //  Setup points at the altitude first
                points[0] = origin + (direction * A);
                points[1] = origin + (direction * A);

                //  Move out on the normal by half the length
                points[0] += (normalA * (B * 0.5f));
                points[1] += (normalB * (B * 0.5f));

            }
        }
        #endregion

        #region Maximizing
        public static void MaximizeWaypoints(Waypoint[] waypoints, LayerMask layerMask, float minDistance)
        {
            foreach (Waypoint waypoint in waypoints)
            {
                MaximizeWithOthers(waypoint, waypoints);
                SizeToCollisions(waypoint, layerMask, minDistance);
            }
            foreach (Waypoint waypoint in waypoints)
                SizeWithoutOverlap(waypoint, waypoints);
        }
        //  Resize so they dont cross centers
        private static void MaximizeWithOthers(Waypoint source, Waypoint[] waypoints)
        {
            //  Temp radius set in case we dont have enough waypoints (dont want some huge radius to do collision checks on later)
            source.radius = 1;
            if (waypoints.Length <= 1) return;

            //  Largest possible radius
            source.radius = Mathf.Infinity;

            //  Prevent them from overlapping also
            foreach (Waypoint waypoint in waypoints)
                if (waypoint != source)
                    source.radius = Mathf.Min(source.radius, Vector3.Distance(source.transform.position, waypoint.transform.position));
            source.radius = Mathf.Max(0.2f, source.radius);
        }
        //  Resize so they dont collide with things
        private static void SizeToCollisions(Waypoint source, LayerMask layerMask, float minDistance)
        {
            //  Recursive check against collisions
            bool hasCollisions = false;
            source.radius += minDistance;
            do
            {
                hasCollisions = false;
                if (Physics.CheckSphere(source.transform.position, source.radius, layerMask))
                {
                    hasCollisions = true;
                    source.radius -= 0.2f;
                }
            } while (hasCollisions && source.radius > 0.2f);
            source.radius = Mathf.Max(0.2f, source.radius - minDistance);
        }
        //  Resize so they dont overlap
        private static void SizeWithoutOverlap(Waypoint source, Waypoint[] waypoints)
        {
            if (waypoints.Length <= 1) return;

            //  Prevent them from overlapping also
            foreach (Waypoint waypoint in waypoints)
                if (waypoint != source)
                {
                    waypoint.radius = Mathf.Min(waypoint.radius, Vector3.Distance(source.transform.position, waypoint.transform.position) - source.radius);
                    waypoint.radius = Mathf.Max(0.2f, waypoint.radius);
                }

        }
        #endregion
    }
}
