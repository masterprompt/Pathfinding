using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Endboss.Navigation
{
    [CustomEditor(typeof(Map))]
    public class Map_Inspector : Editor
    {
        #region Properties
        private Map targetScript = null;
        #endregion

        #region Editor
        public override void OnInspectorGUI()
        {
            targetScript = (Map)target;

            List<string> layerNames = new List<string>();
            for (int index = 0; index < 32; index++)
                if (LayerMask.LayerToName(index).Length != 0)
                    layerNames.Add(LayerMask.LayerToName(index));

            targetScript.layerMask = EditorGUILayout.MaskField("Layers:", targetScript.layerMask, layerNames.ToArray());
            targetScript.minDistance = EditorGUILayout.FloatField("Min Distance:", targetScript.minDistance);
            

            if (GUILayout.Button("Generate"))
            {
                Analyst a = new Analyst();
                a.Analyze(targetScript);
                targetScript.waypoints = a.waypointsList.ToArray();
                targetScript.paths = a.pathList.ToArray();
                targetScript.connections = a.connectionList.ToArray();

                //targetScript.Analyze();
                Repaint();
                EditorUtility.SetDirty(targetScript);
            }
            EditorGUILayout.BeginHorizontal();
            targetScript.showWaypoints = EditorGUILayout.Toggle("Show Waypoints:", targetScript.showWaypoints);
            EditorGUILayout.IntField(targetScript.waypoints.Length);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            targetScript.showConnections = EditorGUILayout.Toggle("Show Connections:", targetScript.showConnections);
            EditorGUILayout.IntField(targetScript.connections.Length);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            targetScript.showPaths = EditorGUILayout.Toggle("Show Paths:", targetScript.showPaths);
            EditorGUILayout.IntField(targetScript.paths.Length);
            EditorGUILayout.EndHorizontal();

            if (targetScript.showPaths)
            {
                targetScript.showAllPaths = EditorGUILayout.Toggle("All:", targetScript.showAllPaths);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("-"))
                    targetScript.drawPathIndex--;

                targetScript.drawPathIndex = EditorGUILayout.IntField(targetScript.drawPathIndex, GUILayout.MaxWidth(40));

                if (GUILayout.Button("+"))
                    targetScript.drawPathIndex++;

                targetScript.drawPathIndex = Mathf.Clamp(targetScript.drawPathIndex, 0, targetScript.paths.Length - 1);

                Repaint();
                EditorUtility.SetDirty(targetScript);

                EditorGUILayout.EndHorizontal();
            }

            Repaint();
            EditorUtility.SetDirty(targetScript);

            //DrawDefaultInspector();

        }
        #endregion
    }
}
