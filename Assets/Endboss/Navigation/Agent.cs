using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Endboss.Navigation
{
    [System.Serializable]
    public class Agent
    {

        #region Properties
        public float speed = 1;
        private Transform target;
        private Transform transform;
        private Waypoint waypointSource = null;
        private Waypoint waypointTarget = null;
        public float waypointUpdateInterval = 0.1f;
        public float pathingUpdateInterval = 0.1f;
        public float pointMinDistance = 1f;
        private int frameSample = 0;
        #endregion

        #region Waypoints
        public bool HasWayoint(Transform transform, Transform target)
        {
            Update(transform, target);
            return (points.Count > 0);
        }
        public Vector3 Vector3ToPoint(Transform transform, Transform target)
        {
            Update(transform, target);
            if (points.Count == 0) return Vector3.zero;
            return points[0] - transform.position;
        }
        public Vector3 waypoint
        {
            get
            {
                if (points.Count == 0) return Vector3.zero;
                return points[0];
            }
        }
        #endregion

        #region Update
        public void Update(Transform transform, Transform target)
        {
            Update(transform, target, false);
        }
        public void Update(Transform transform, Transform target, bool overrideFrame)
        {
            //  Only update once per frame
            if (Time.frameCount == frameSample && !overrideFrame) return;
            frameSample = Time.frameCount;

            if (transform == null || target == null)
            {
                points.Clear();
                return;
            }

            //  Set source and target for later
            this.transform = transform;
            this.target = target;

            //  Update waypoints and pathing based on time allowance
            Waypoint_Update();
            Pathing_Update();

            //  If no points exist, no reason to check any further
            if (points.Count <= 0) return;

            //  Are we close to the first point?
            if ((this.transform.position - points[0]).sqrMagnitude > Mathf.Pow(pointMinDistance, 2)) return;

            //  Remove first point
            points.RemoveAt(0);
        }
        #endregion

        #region Drawing
        public void Draw(Transform transform)
        {
            if(!Application.isPlaying) return;
            Gizmos.color = Color.green;
            Waypoint_Draw(this.transform, this.waypointSource);
            Waypoint_Draw(target, this.waypointTarget);
            Gizmos.color = Color.red;
            Pathing_Draw(this.transform);
        }
        #endregion

        #region Waypoint
        private float waypointLastUpdateTime = 0;
        private void Waypoint_Update()
        {

            if (Map.Current == null)
            {
                this.waypointSource = null;
                this.waypointTarget = null;
                return;
            }

            if (Time.time - waypointLastUpdateTime < waypointUpdateInterval) return;
            waypointLastUpdateTime = Time.time;
            this.waypointSource = Waypoint_Find(this.transform);
            this.waypointTarget = Waypoint_Find(target);

            

        }
        private Waypoint Waypoint_Find(Transform t)
        {
            if (t == null) return null;

            //  Buffer for testing
            List<WaypointDistance> wdl = new List<WaypointDistance>();

            //  Find what waypoint we are in or near
            foreach (Waypoint waypoint in Map.Current.waypoints)
                wdl.Add(new WaypointDistance(waypoint, (t.position - waypoint.Transform.position).sqrMagnitude - Mathf.Pow(waypoint.radius, 2)));

            //  Resort list based on distance
            wdl = wdl.OrderBy(x => x.distance).ToList();

            //  Test each until we find ours
            foreach (WaypointDistance wd in wdl)
                if (!Physics.Linecast(t.position, wd.waypoint.Transform.position, Map.Current.layerMask))
                    return wd.waypoint;
            return null;
        }
        private void Waypoint_Draw(Transform t, Waypoint w)
        {
            if (t == null) return;
            if (w == null) return;
            Gizmos.DrawLine(t.position, w.Transform.position); 
        }
        #endregion

        #region Pathing
        private Waypoint sharedWaypoint = null;
        private float pathingLastUpdateTime = 0;
        private Map.Path mapPath = null;
        public List<Vector3> points = new List<Vector3>();
        private void Pathing_Update()
        {
            if (Map.Current == null || this.waypointSource == null || this.waypointTarget==null || target==null )
            {
                sharedWaypoint = null;
                mapPath = null;
                points.Clear();
                return;
            }

            if (Time.time - pathingLastUpdateTime < pathingUpdateInterval) return;
            pathingLastUpdateTime = Time.time;

            //  Can we see target?
            if (Map.Current.PointsVisible(this.transform.position, target.position))
            {
                //Debug.Log("Visible");
                points.Clear();
                points.Add(target.position); 
                return;
            }

            //  Can we just connect via a single waypoint?
            if (this.waypointSource == this.waypointTarget && sharedWaypoint != this.waypointSource)
            {
                sharedWaypoint = this.waypointSource;

                points.Clear();

                //  Are we outside the waypoint? first get to waypoint edge
                if (!this.waypointSource.IsInside(this.transform.position))
                    points.Add(this.waypointSource.EdgePoint(this.transform.position));

                //  Is target inside waypoint?

                if (!this.waypointSource.IsInside(target.position))
                     points.Add(this.waypointSource.EdgePoint(target.position));

                points.Add(target.position);
                
                return;
            }
            sharedWaypoint = null;
            
            Map.Path path = Map.Current.Find(this.waypointSource, this.waypointTarget);
            if (this.mapPath == path) return;

            this.mapPath = path;
            points.Clear();
            if (this.mapPath == null) return;


            //  First add our point into waypoint if we are outside it
            if (!this.waypointSource.IsInside(this.transform.position))
                points.Add(this.waypointSource.EdgePoint(this.transform.position));

            //  Get middle of entrance
            points.Add(Map.Current.ToWorld(mapPath.Entrance(this.waypointSource)));

            //  Add our points
            points.AddRange(Map.Current.ToWorld(this.mapPath.PointsArray(this.waypointSource)));

            //  Get middle of entrance
            points.Add(Map.Current.ToWorld(mapPath.Entrance(this.waypointTarget)));

            //  Add our last point out of waypoint if target is outside of it
            if (!this.waypointTarget.IsInside(target.position))
                points.Add(this.waypointTarget.EdgePoint(target.position));

        }
        public void Pathing_Draw(Transform t)
        {
            if (t == null) return;
            if (points.Count <= 0) return;
            Vector3 p = this.transform.position;
            foreach (Vector3 pn in points)
            {
                Gizmos.DrawLine(p, pn);
                p = pn;
            }


        }
        #endregion

        #region Path
        public class Path
        {
            public Queue<Vector3> pointQueue = new Queue<Vector3>();
            public List<Vector3> pointList = new List<Vector3>();
            public Vector3 point = Vector3.zero;
            public bool isValid = false;

            public bool NextPoint()
            {
                if (pointQueue.Count <= 0)
                {
                    isValid = false;
                    return false;
                }
                point = pointQueue.Dequeue();
                pointList.RemoveAt(0);
                isValid = true;
                return true;
            }

            public void Fill(Vector3[] points)
            {
                pointQueue = new Queue<Vector3>(points);
                pointList = new List<Vector3>(points);
                NextPoint();
            }

            public void Add(Vector3 point)
            {
                pointQueue.Enqueue(point);
                pointList.Add(point);
            }

            public void Clear()
            {
                pointQueue.Clear();
                pointList.Clear();
                isValid = false;
            }
        }
        #endregion
        #region WaypointDistance
        public struct WaypointDistance
        {
            public Waypoint waypoint;
            public float distance;

            public WaypointDistance(Waypoint waypoint, float distance)
            {
                this.waypoint = waypoint;
                this.distance = distance;
            }

        }
        #endregion
    }
}
