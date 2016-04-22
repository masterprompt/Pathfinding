using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Endboss.Navigation
{

    public class Analyst
    {
        #region Properties
        private Map map;
        public List<Waypoint> waypointsList = new List<Waypoint>();
        public List<Connection> connectionList = new List<Connection>();
        public List<Map.Path> pathList = new List<Map.Path>();
        #endregion

        #region Analyze
        public void Analyze(Map map)
        {
            Debug.Log("Generating navigation map...");
            this.map = map;
            Waypoints_Find();
            Connections_Generate();
            Paths_Generate();
            Debug.Log("Navigation map complete!");
        }
        #endregion

        #region Waypoints
        private void Waypoints_Find()
        {
            Waypoint[] waypoints = map.GetComponentsInChildren<Waypoint>();
            int nameIndex = 0;
            foreach (Waypoint w in waypoints)
            {
                w.name = "W" + nameIndex.ToString().PadLeft(3, '0');
                nameIndex++;
            }
            waypointsList = new List<Waypoint>(waypoints);
            Debug.Log("Waypoints:" + waypointsList.Count);
        }
        #endregion

        #region Connections
        private void Connections_Generate()
        {
            List<Connection> cl = new List<Connection>();
            foreach (Waypoint wA in waypointsList)
                foreach (Waypoint wB in waypointsList)
                    if (wA != wB)
                        if (Waypoint.IsVisible(wA, wB, map.layerMask))
                        {
                            float width = Waypoint.VisibilityWidth(wA, wB, Mathf.Min(wA.radius, wB.radius) * 2, map.minDistance, map.layerMask);
                            if (width >= map.minDistance)
                            {
                                Connection cs = new Connection(map, wA, wB, width);
                                bool isDuplicate = false;
                                foreach (Connection ct in cl)
                                    if (Connection.IsDuplicate(ct, cs))
                                        isDuplicate = true;
                                if (!isDuplicate) cl.Add(cs);
                            }

                        }
            this.connectionList = new List<Connection>(cl.ToArray());
            Debug.Log("Connections:" + this.connectionList.Count);
        }
        #endregion

        #region WaypointPaths
        private void Paths_Generate()
        {
            //  Working waypointPath array
            List<WaypointPath> wpl = new List<WaypointPath>();
            List<Map.Path> pl = new List<Map.Path>();

            //  Trace out our paths
            foreach (Waypoint waypoint in waypointsList)
                wpl.AddRange(WaypointPath.Trace(waypoint, null, connectionList.ToArray()));

            Debug.Log("Temporary waypoint paths:" + wpl.Count);

            foreach (WaypointPath wp in wpl)
                pl.Add(wp.GetPath(connectionList.ToArray(), map));

            pathList = new List<Map.Path>(Map.Path.Optimize(pl.ToArray()));

            Debug.Log("Paths:" + pathList.Count);


        }
        #endregion

        #region Connection
        [System.Serializable]
        public class Connection
        {
            #region Properties
            //  Waypoints for start and end
            public Waypoint A, B;
            //  Edges (chord points on circle) for each waypoint
            public Vector3[] AE = new Vector3[2];
            public Vector3[] BE = new Vector3[2];
            //  Width of connection (widest allowed connection)
            public float width = 0;
            #endregion

            #region Constructor
            public Connection(Map m, Waypoint A, Waypoint B, float w)
            {
                this.A = A;
                this.B = B;
                this.width = w;

                //  Create the chords
                Waypoint.Chord cA = new Waypoint.Chord(A.transform.position, A.radius, (B.transform.position - A.transform.position).normalized, width);
                Waypoint.Chord cB = new Waypoint.Chord(B.transform.position, B.radius, (A.transform.position - B.transform.position).normalized, width);

                this.AE[0] = m.ToLocal(cA.pointA);
                this.AE[1] = m.ToLocal(cA.pointB);

                this.BE[1] = m.ToLocal(cB.pointA);
                this.BE[0] = m.ToLocal(cB.pointB);

            }
            #endregion

            #region Drawing
            public static void DrawAll(Map m, Connection[] ca)
            {
                foreach (Connection c in ca)
                    c.Draw(m);
            }
            public void Draw(Map m)
            {
                if (AE.Length < 2) return;
                if (BE.Length < 2) return;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(m.ToWorld(AE[0]), m.ToWorld(BE[0]));
                Gizmos.DrawLine(m.ToWorld(AE[1]), m.ToWorld(BE[1]));
            }
            #endregion

            #region Edge
            public Vector3[] EdgesFor(Waypoint w)
            {
                return (w == A ? AE : BE);
            }
            #endregion

            #region Duplicate
            public static bool IsDuplicate(Connection a, Connection b)
            {
                if (a == b) return false;
                if (a.A == b.B && a.B == b.A) return true;
                if (a.A == b.A && a.B == b.B) return true;
                return false;
            }
            #endregion

            #region PointsBetween
            public static Vector3[] PointsBetween(Connection a, Connection b, Map m)
            {
                Waypoint sharedPoint = SharedWaypoint(a, b);
                if (sharedPoint == null) return new Vector3[0];

                //  Need entry points of non-shared a
                Vector3[] pA = (sharedPoint == a.B ? a.BE : a.AE);
                //  Need entry points of non-shared b
                Vector3[] pB = (sharedPoint == b.B ? b.BE : b.AE);

                return ShortestJoint(pA, m.ToLocal(sharedPoint.transform.position), pB);
            }
            public static Waypoint SharedWaypoint(Connection a, Connection b)
            {
                if (IsDuplicate(a, b)) return null;
                if (a.A == b.A) return a.A;
                if (a.A == b.B) return a.A;
                if (a.B == b.A) return a.B;
                if (a.B == b.B) return a.B;
                return null;
            }
            public static Vector3[] ShortestJoint(Vector3[] a, Vector3 center, Vector3[] b)
            {
                if (a.Length == 2 && b.Length == 2)
                {
                    Vector3 aMid = Vector3.Lerp(a[0], a[1], 0.5f);
                    Vector3 aVec = aMid - center;
                    Vector3 bMid = Vector3.Lerp(b[0], b[1], 0.5f);
                    Vector3 bVec = bMid - center;
                    float angle = Vector3.Angle(aVec.normalized, bVec.normalized);
                    if (angle >= 170)
                    {
                        return new Vector3[] { aMid, bMid };
                    }

                    return ShortsPoints(a, b);

                }
                return new Vector3[0];
            }
            /*
            public static Vector3 ShortestPoint(Vector3[] a, Vector3 center, Vector3 target)
            {
                if (a.Length == 2)
                {
                    Vector3 aMid = Vector3.Lerp(a[0], a[1], 0.5f);
                    Vector3 aVec = aMid - center;
                    Vector3 bMid = target;
                    Vector3 bVec = bMid - center;

                    float angle = Vector3.Angle(aVec.normalized, bVec.normalized);
                    if (angle >= 170)
                    {
                        return aMid;
                    }

                    return ShortsPoints(a, b);
                }
                return Vector3.zero;
            }*/
            public static Vector3[] ShortsPoints(Vector3[] a, Vector3[] b)
            {
                Vector3[] p = new Vector3[2];
                float d = Mathf.Infinity;
                foreach (Vector3 pa in a)
                    foreach (Vector3 pb in b)
                        if (Vector3.Distance(pa, pb) < d)
                        {
                            d = Vector3.Distance(pa, pb);
                            p[0] = pa;
                            p[1] = pb;
                        }

                return p;
            }
            #endregion

            #region Find
            public static Connection Find(Waypoint a, Waypoint b, Connection[] connections)
            {
                foreach (Connection c in connections)
                {
                    if (c.A == a && c.B == b)
                        return c;
                    if (c.A == b && c.B == a)
                        return c;
                }
                return null;
            }
            #endregion


        }
        #endregion

        #region WaypointPath
        public class WaypointPath
        {
            #region Properties
            public Waypoint A { get { return waypoints[0]; } }
            public Waypoint B { get { return waypoints[waypoints.Count - 1]; } }
            public List<Waypoint> waypoints = new List<Waypoint>();
            #endregion

            #region Copy
            public WaypointPath Copy()
            {
                WaypointPath path = new WaypointPath();
                path.waypoints = new List<Waypoint>(this.waypoints);
                return path;
            }
            #endregion

            #region Trace
            public static WaypointPath[] Trace(Waypoint ws, WaypointPath wp, Connection[] ca)
            {
                //  Create a basic path
                if (wp == null) wp = new WaypointPath();

                //  Create the local list
                List<WaypointPath> wpl = new List<WaypointPath>();

                //  If we already have this point, we dont need to continue on it's path
                if (wp.waypoints.Contains(ws)) return wpl.ToArray();

                wp = wp.Copy();
                wp.waypoints.Add(ws);

                //  If we have more points than 1, add it to the list so it's returned
                if (wp.waypoints.Count > 1) wpl.Add(wp);

                //  Go through each connection and find ones that have our node
                foreach (Connection c in ca)
                {
                    if (c.A == ws) wpl.AddRange(Trace(c.B, wp, ca));
                    if (c.B == ws) wpl.AddRange(Trace(c.A, wp, ca));
                }

                //  Give our list back
                return wpl.ToArray();
            }
            #endregion

            #region Path
            public Map.Path GetPath(Connection[] ca, Map m)
            {
                Map.Path p = new Map.Path();
                List<Vector3> pl = new List<Vector3>();
                //  Start and end points
                p.A = A;
                p.B = B;

                //  Get first connection
                Connection cs = Connection.Find(waypoints[0], waypoints[1], ca);
                if (cs != null) p.AE = (cs.EdgesFor(waypoints[0]));

                if (waypoints.Count > 2)
                {
                    Waypoint lw = null;
                    Connection lc = null;
                    foreach (Waypoint ws in waypoints)
                    {
                        if (lw != null && lw != ws)
                        {
                            cs = Connection.Find(lw, ws, ca);
                            if (lc != null && cs != lc && cs != null)
                            {
                                Vector3[] jointPoints = Connection.PointsBetween(lc, cs, m);
                                pl.AddRange(jointPoints);
                            }
                            lc = cs;
                        }
                        lw = ws;
                    }
                }

                //  Get last connection
                cs = Connection.Find(waypoints[waypoints.Count - 1], waypoints[waypoints.Count - 2], ca);
                if (cs != null) p.BE = (cs.EdgesFor(waypoints[waypoints.Count - 1]));

                p.points = (pl.ToArray());

                return p;
            }
            #endregion
        }
        #endregion


    }
}
