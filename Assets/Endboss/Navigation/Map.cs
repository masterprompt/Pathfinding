using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Endboss.Navigation
{
    [ExecuteInEditMode]
    [AddComponentMenu("Endboss/Navigation/Map")]
    public class Map : MonoBehaviour
    {



        #region Properties
        public static Map Current = null;
        private Transform Transform;
        public LayerMask layerMask = 1;
        public float minDistance = 1;
        public bool showWaypoints = true;
        public bool showConnections = false;
        public bool showPaths = true;
        public bool showAllPaths = true;
        public int drawPathIndex = 0;
        public Waypoint[] waypoints = new Waypoint[0];
        public Analyst.Connection[] connections = new Analyst.Connection[0];
        public Path[] paths = new Path[0];
        private Dictionary<Waypoint, Dictionary<Waypoint, Path>> pathTable = new Dictionary<Waypoint, Dictionary<Waypoint, Path>>();
        #endregion

        #region MonoBehaviour
        void Awake()
        {
            if (Application.isPlaying) enabled = false;
            this.Transform = transform;
            Current = this;
            
            //  Catelog all the paths
            foreach (Path path in paths)
            {
                if (!pathTable.ContainsKey(path.A)) pathTable.Add(path.A, new Dictionary<Waypoint, Path>());
                if (!pathTable[path.A].ContainsKey(path.B)) pathTable[path.A].Add(path.B, path);
            }

        }
        void Start()
        {

        }
        void Update()
        {
            if (!Application.isEditor) return;
            Waypoint[] waypoints = GetComponentsInChildren<Waypoint>();
            Waypoint.MaximizeWaypoints(waypoints, layerMask, minDistance);
        }
        void OnDrawGizmos()
        {
            if (showWaypoints)
            {
                Waypoint[] waypoints = GetComponentsInChildren<Waypoint>();
                foreach (Waypoint w in waypoints)
                    w.DrawGizmos();
            }

            if (showConnections)
            {
                Analyst.Connection.DrawAll(this, connections);
            }

            if (showPaths)
            {
                if (showAllPaths)
                {
                    Gizmos.color = Color.yellow;
                    Path.DrawAll(this, paths);
                }

                Gizmos.color = Color.red;
                Path.Draw(this, paths, drawPathIndex);
            }

        }
        #endregion

        #region FindPath
        public Path Find(Waypoint A, Waypoint B)
        {
            return Path.Find(A, B, pathTable);
        }
        #endregion

        #region PointsVisible
        public bool PointsVisible(Vector3 p1, Vector3 p2)
        {
            Ray ray = new Ray(p1, (p2 - p1));
            return !Physics.SphereCast(ray, minDistance, (p2 - p1).magnitude, layerMask);
        }
        #endregion

        #region Conversion
        public Vector3[] ToLocal(Vector3[] p)
        {
            Transform t = (this.Transform != null ? this.Transform : this.transform);
            Vector3[] np = new Vector3[p.Length];
            for (int i = 0; i < p.Length; i++)
                np[i] = t.InverseTransformPoint(p[i]);
            return np;
        }
        public Vector3[] ToWorld(Vector3[] p)
        {
            Transform t = (this.Transform != null ? this.Transform : this.transform);
            Vector3[] np = new Vector3[p.Length];
            for (int i = 0; i < p.Length; i++)
                np[i] = t.TransformPoint(p[i]);
            return np;
        }
        public Vector3 ToLocal(Vector3 p)
        {
            Transform t = (this.Transform != null ? this.Transform : this.transform);
            return t.InverseTransformPoint(p);
        }
        public Vector3 ToWorld(Vector3 p)
        {
            Transform t = (this.Transform != null ? this.Transform : this.transform);
            return t.TransformPoint(p);
        }
        #endregion

        #region Path
        [System.Serializable]
        public class Path
        {
            #region Properties
            //  Nodes to use when constructing path table
            public Waypoint A, B;
            //  Distance of path
            public float distance, distanceSqrd;
            //  Points used as waypoints
            public Vector3[] points = new Vector3[0];
            //  Possible entrances and exits that can be added on to the path for first and last node (usually chord points)
            public Vector3[] AE = new Vector3[0];
            public Vector3[] BE = new Vector3[0];
            #endregion

            #region Optimizing
            //  Optimizes paths by calculating distance, triming to shotest paths and removing reverse routes
            public static Path[] Optimize(Path[] pa)
            {
                //  Generate distance measurements
                CalculateDistances(pa);

                //  Trim out longer paths and just leave us with shortest
                pa = Trim(pa);

                //  Remove duplicates (reverse paths)
                pa = Strip(pa);

                return pa;
            }

            public static bool IsDuplicate(Path p1, Path p2)
            {
                if (p1 == p2) return false;
                if (p1.A == p2.A && p1.B == p2.B) return true;
                if (p1.A == p2.B && p1.B == p2.A) return true;
                return false;
            }
            //  Strip duplicates (reverse paths)
            private static Path[] Strip(Path[] pa)
            {
                //  Holding list
                List<Path> pl = new List<Path>();

                foreach (Path ps in pa)
                {
                    bool f = false;
                    foreach (Path pt in pl)
                        if (ps != pt)
                            if (IsDuplicate(ps, pt))
                                f = true;
                    if (!f) pl.Add(ps);
                }
                return pl.ToArray();

            }

            //  Trim the longer paths out
            public static Path[] Trim(Path[] pa)
            {
                //  Catelog paths by same end points
                Dictionary<Waypoint, Dictionary<Waypoint, List<Path>>> t = new Dictionary<Waypoint, Dictionary<Waypoint, List<Path>>>();
                foreach (Path p in pa)
                {
                    if (!t.ContainsKey(p.A)) t.Add(p.A, new Dictionary<Waypoint, List<Path>>());
                    if (!t[p.A].ContainsKey(p.B)) t[p.A].Add(p.B, new List<Path>());
                    t[p.A][p.B].Add(p);
                }
                //  Holding list
                List<Path> pl = new List<Path>();

                //  Sort each by distance, then grab shortest
                foreach (KeyValuePair<Waypoint, Dictionary<Waypoint, List<Path>>> kvp1 in t)
                    foreach (KeyValuePair<Waypoint, List<Path>> kvp2 in kvp1.Value)
                    {
                        Path[] pa2 = kvp2.Value.OrderBy(x => x.distance).ToArray();
                        if (pa2.Length > 0) pl.Add(pa2[0]);
                    }


                return pl.ToArray();

            }
            #endregion

            #region Distance
            public static void CalculateDistances(Path[] pa)
            {
                foreach (Path p in pa)
                    p.CalculateDistance();
            }
            public void CalculateDistance()
            {
                distance = 0;
                for (int i = 1; i < points.Length; i++)
                    distance += Vector3.Distance(points[i - 1], points[i]);
                distanceSqrd = Mathf.Pow(distance, 2);
            }
            #endregion

            #region Drawing
            public static void Draw(Map m, Path[] pa, int pi)
            {
                if (pi < 0) return;
                if (pi >= pa.Length) return;
                pa[pi].Draw(m);
            }
            public static void DrawAll(Map m, Path[] pa)
            {
                foreach (Path p in pa)
                    p.Draw(m);
            }
            public void Draw(Map map)
            {
                Vector3[] p = GenericPath();
                for (int pIndex = 1; pIndex < p.Length; pIndex++)
                    Gizmos.DrawLine(map.ToWorld(p[pIndex - 1]), map.ToWorld(p[pIndex]));
            }
            public Vector3[] GenericPath()
            {
                List<Vector3> list = new List<Vector3>();

                if (AE.Length == 2) list.Add(Vector3.Lerp(AE[0], AE[1], 0.5f));
                list.AddRange(points);
                if (BE.Length == 2) list.Add(Vector3.Lerp(BE[0], BE[1], 0.5f));

                return list.ToArray();
            }
            #endregion

            #region Find
            public static Path Find(Waypoint A, Waypoint B, Dictionary<Waypoint, Dictionary<Waypoint, Path>> pt)
            {
                //  First find normal path
                if (pt.ContainsKey(A))
                    if (pt[A].ContainsKey(B))
                        return pt[A][B];
                //  Find flipped
                if (pt.ContainsKey(B))
                    if (pt[B].ContainsKey(A))
                        return pt[B][A];
                return null;
            }
            #endregion

            #region PointArray
            public Vector3[] PointsArray(Waypoint A)
            {
                if(points.Length == 0 ) return new Vector3[0];
                if(A == this.A ) return points;
                Vector3[] pa = new Vector3[points.Length];
                Array.Copy(points,pa,points.Length);
                Array.Reverse(pa);
                return pa;
            }
            #endregion

            #region Closest Entrance
            public Vector3 Entrance(Waypoint A)
            {
                if (A == this.A) return Vector3.Lerp(AE[0], AE[1], 0.5f) ;
                return Vector3.Lerp(BE[0], BE[1], 0.5f);
            }
            #endregion


        }
        #endregion

    }
}
