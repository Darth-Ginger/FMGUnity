using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

namespace FMGUnity.Utility
{

    [RequireComponent(typeof(MeshRenderer))]
    public class VoronoiTester : MonoBehaviour
    {
        [Header("Settings")]
        public int pointCount = 100;      // Number of points for the Voronoi diagram
        public Vector2Int size = new(256, 256); // Size of the texture/map (width and height)
        public bool RandomSeed = false;
        public int seed = 42; // Random seed for point generation

        [SerializeField]
        private VoronoiDiagram voronoiMap;    // The Voronoi map
        private Texture2D voronoiTexture; // Texture to render the map
        private MeshRenderer meshRenderer; // Optional MeshRenderer for the plane
        private List<VoronoiCell> voronoiCells; // List of generated Voronoi cells

        private Dictionary<Vector2Int, VoronoiCell> pixelToCellMap = new(); // Maps each pixel to a cell
        private VoronoiCell hoveredCell; // The cell currently being hovered over

        void Start() => GenerateVisualization();

        void Update() => HandleMouseHover();

        void OnGUI()
        {
            if (hoveredCell != null)
            {
                // Display information about the hovered cell at the bottom-left of the screen
                string cellInfo = $"Hovered Cell Info:\nSite: {hoveredCell.Site}\nVertices: {hoveredCell.Vertices.Count}";
                GUI.Label(new Rect(10, Screen.height - 50, 400, 50), cellInfo);
            }
        }

        [Button("Generate")]
        void GenerateVisualization()
        {
            if (voronoiMap != null && voronoiMap.IsInitialized()) return;
            // Check if the GameObject has a MeshRenderer for visualization on a plane
            meshRenderer = GetComponent<MeshRenderer>();

            if (RandomSeed) seed = Random.Range(1, 10000);

            // Initialize and generate the Voronoi map
            voronoiMap = new(size, pointCount, seed);

            if (meshRenderer != null)
            {
                // If a MeshRenderer is attached, generate and apply a texture
                GenerateVoronoiTexture();
                ApplyTextureToPlane();
            }
        }

        [Button("Clear Visualization")]
        void ClearVisualization()
        {
            if (meshRenderer != null)
            {
                // Clear the texture from the plane
                meshRenderer.material.mainTexture = null;
            }
        }

        [Button("Clear Diagram")]
        void ClearDiagram()
        {
            voronoiMap?.Clear();
            ClearVisualization();
        }

        void GenerateVoronoiTexture()
        {
            // Reset texture and pixel map
            voronoiTexture = null;
            pixelToCellMap?.Clear();

            Dictionary<VoronoiCell, Color> cellColors = new();
            
            // Create a new texture based on the size
            voronoiTexture = new((int)size.x, (int)size.y)
            {
                filterMode = FilterMode.Point, // Ensure sharp edges
                wrapMode = TextureWrapMode.Clamp // Clamp edges
            };


            // Generate unique colors for each cell
            foreach (var cell in voronoiMap.Cells)
            {
                cellColors[cell] = new Color(Random.value, Random.value, Random.value);
            }

            // Fill the texture based on the Voronoi cells
            for (int x = 0; x < voronoiTexture.width; x++)
            {
                for (int y = 0; y < voronoiTexture.height; y++)
                {
                    Vector2 point = new Vector2(x, y);
                    VoronoiCell closestCell = FindClosestCell(point, voronoiMap.Cells);

                    // Map the pixel to the cell
                    pixelToCellMap[new Vector2Int(x, y)] = closestCell;

                    // Set the pixel color
                    voronoiTexture.SetPixel(x, y, cellColors[closestCell]);
                }
            }

            // Apply the changes to the texture
            voronoiTexture.Apply();
        }

        VoronoiCell FindClosestCell(Vector2 point, List<VoronoiCell> cells)
        {
            VoronoiCell closestCell = null;
            float closestDistance = float.MaxValue;

            foreach (var cell in cells)
            {
                float distance = Vector2.SqrMagnitude(point - cell.Site);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestCell = cell;
                }
            }

            return closestCell;
        }

        void ApplyTextureToPlane()
        {
            if (meshRenderer != null)
            {
                // Create a new material if none exists
                if (meshRenderer.material == null)
                {
                    meshRenderer.material = new Material(Shader.Find("Standard"));
                }

                // Assign the Voronoi texture to the material
                meshRenderer.material.mainTexture = voronoiTexture;
            }
        }

        void HandleMouseHover()
        {
            if (meshRenderer == null) return;

            // Raycast to detect the point on the plane where the mouse is pointing
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 hitPoint = hit.point;

                // Convert the hit point to local space relative to the plane
                Vector3 localPoint = transform.InverseTransformPoint(hitPoint);

                // Adjust for the plane's scale
                float planeWidth = transform.localScale.x * 10; // 10 is the default plane size in Unity
                float planeHeight = transform.localScale.z * 10;

                // Map the localPoint to texture coordinates
                Vector2Int texturePoint = new Vector2Int(
                    Mathf.RoundToInt((planeWidth / 2 - localPoint.x) / planeWidth * size.x),
                    Mathf.RoundToInt((planeHeight / 2 - localPoint.z) / planeHeight * size.y)
                );

                // Check if the point is within the texture bounds
                if (texturePoint.x >= 0 && texturePoint.x < size.x && texturePoint.y >= 0 && texturePoint.y < size.y)
                {
                    // Find the corresponding Voronoi cell
                    pixelToCellMap.TryGetValue(texturePoint, out hoveredCell);

                    // Highlight the hovered cell
                    HighlightCell(hoveredCell);
                }
            }
            else
            {
                hoveredCell = null;
            }
        }

        void HighlightCell(VoronoiCell cell)
        {
            if (cell == null) return;

            // Create a copy of the texture to highlight the hovered cell
            Texture2D highlightedTexture = new Texture2D((int)size.x, (int)size.y);
            highlightedTexture.SetPixels(voronoiTexture.GetPixels());

            // Highlight all pixels belonging to the hovered cell
            foreach (var kvp in pixelToCellMap)
            {
                if (kvp.Value == cell)
                {
                    highlightedTexture.SetPixel(kvp.Key.x, kvp.Key.y, Color.yellow);
                }
            }

            highlightedTexture.Apply();
            meshRenderer.material.mainTexture = highlightedTexture;
        }
    }

}
