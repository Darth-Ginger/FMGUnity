// using UnityEditor;
// using UnityEngine;

// public class VoronoiDiagramEditor : EditorWindow
// {
//     private Rect mapBounds = new Rect(0, 0, 100, 100);
//     private int numSites = 10;

//     [MenuItem("Window/Voronoi Diagram Editor")]
//     public static void ShowWindow()
//     {
//         GetWindow<VoronoiDiagramEditor>("Voronoi Diagram Editor");
//     }

//     private void OnGUI()
//     {
//         GUILayout.Label("Map Bounds", EditorStyles.boldLabel);
//         mapBounds = EditorGUILayout.RectField("Map Bounds", mapBounds);

//         GUILayout.Label("Number of Sites", EditorStyles.boldLabel);
//         numSites = EditorGUILayout.IntField("Number of Sites", numSites);

//         if (GUILayout.Button("Generate Diagram"))
//         {
//             var diagram = new VoronoiDiagram(mapBounds, numSites);
//             // Do something with the generated diagram.
//             diagram.Dispose();
//         }
//     }
// }