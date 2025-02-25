using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe class NativeQuadtreeHandle<T> where T : unmanaged
    {

        #region Public Variables

        #endregion

        #region Private Variables


        private Color boundsColor;
        private Color hierarchyColor;
        private Color squareColor;
        private Color densityColor;

        private float height = 0.0f;

        private IQuadtree<T> quadtree;

        #endregion

        public float GetHeight() => this.height;

        public NativeQuadtreeHandle(IQuadtree<T> quadtree, Color boundsColor, Color hierarchyColor, Color densityColor, Color squareColor, float height = 0.0f)
        {
            this.quadtree = quadtree;
            this.height = height;
            this.boundsColor = boundsColor;
            this.hierarchyColor = hierarchyColor;
            this.squareColor = squareColor;
            this.densityColor = densityColor;
        }

        private void DrawBorder()
        {
            var oldColor = Handles.color;
            Handles.color = boundsColor;

            var bottomLeft = this.quadtree.GetBottomLeftPosition();
            var scale = this.quadtree.GetScale();

            Handles.DrawAAPolyLine(new Vector3(bottomLeft.x, this.height, bottomLeft.z),
                new Vector3(bottomLeft.x + scale.x, this.height, bottomLeft.z),
                new Vector3(bottomLeft.x + scale.x, this.height, bottomLeft.z + scale.y),
                new Vector3(bottomLeft.x, this.height, bottomLeft.z + scale.y),
                new Vector3(bottomLeft.x, this.height, bottomLeft.z));

            Handles.color = oldColor;
        }

        private void DrawSquare(uint code, Color outlineColor, Color areaColor)
        {
            var globalMin = this.quadtree.GetBottomLeftPosition();
            int maxDepth = this.quadtree.GetMaxDepth();
            var scale = this.quadtree.GetScale();
            int axisDivisions = 1 << maxDepth;

            Vector2 squareSize = scale / (float)axisDivisions;

            var position = MathUtil.QuadtreeCellToCoord(code);

            float percentX = position.x / (float)axisDivisions;
            float percentY = position.y / (float)axisDivisions;

            Vector3 worldPosMin = (Vector3)globalMin + new Vector3(scale.x * percentX, this.height, scale.y * percentY);
            Vector3 worldPosMax = worldPosMin + new Vector3(squareSize.x, 0.0f, squareSize.y);

            Handles.color = outlineColor;
            Handles.DrawAAPolyLine(worldPosMin,
                new Vector3(worldPosMax.x, worldPosMin.y, worldPosMin.z),
                worldPosMax,
                new Vector3(worldPosMin.x, worldPosMin.y, worldPosMax.z),
                worldPosMin);

            Handles.color = areaColor;
            Handles.DrawAAConvexPolygon(worldPosMin,
                new Vector3(worldPosMax.x, worldPosMin.y, worldPosMin.z),
                worldPosMax,
                new Vector3(worldPosMin.x, worldPosMin.y, worldPosMax.z));
        }

        private void DrawHierarchyRecursion(QuadtreeNode node, float3 min, float3 max, int depth)
        {
            Handles.color = this.hierarchyColor;

            Handles.DrawAAPolyLine(min,
                new Vector3(max.x, min.y, min.z),
                max,
                new Vector3(min.x, min.y, max.z),
                min);

            int maxDepth = this.quadtree.GetMaxDepth();
            float percentage = depth / (float)maxDepth;
            var color = this.densityColor;
            color.a = percentage;
            Handles.color = color;

            Handles.DrawAAConvexPolygon(min,
                new Vector3(max.x, min.y, min.z),
                max,
                new Vector3(min.x, min.y, max.z));

            var scale = max - min;
            var halfScale = scale * 0.5f;

            var bottomLeftMin = min;
            var bottomLeftMax = min + halfScale;

            var topLeftMin = min + new float3(0.0f, 0.0f, halfScale.z);
            var topLeftMax = topLeftMin + halfScale;

            var topRightMin = min + halfScale;
            var topRightMax = topRightMin + halfScale;

            var bottomRightMin = min + new float3(halfScale.x, 0.0f, 0.0f);
            var bottomRightMax = bottomRightMin + halfScale;

            var nodes = this.quadtree.GetNodes();
            
            if (node.children[0] != 0) this.DrawHierarchyRecursion(nodes[node.children[0]], bottomLeftMin, bottomLeftMax, depth + 1);
            if (node.children[1] != 0) this.DrawHierarchyRecursion(nodes[node.children[1]], bottomRightMin, bottomRightMax, depth + 1);
            if (node.children[2] != 0) this.DrawHierarchyRecursion(nodes[node.children[2]], topLeftMin, topLeftMax, depth + 1);
            if (node.children[3] != 0) this.DrawHierarchyRecursion(nodes[node.children[3]], topRightMin, topRightMax, depth + 1);
        }

        private void DrawHierarchy()
        {
            var oldColor = Handles.color;

            var root = this.quadtree.GetRoot();

            
            var min = this.quadtree.GetBottomLeftPosition();
            var scale = this.quadtree.GetScale();
            var scale3D = new float3(scale.x, 0.0f, scale.y);
            var halfScale3D = scale3D * 0.5f;
            min.y = this.height;

            var bottomLeftMin = min;
            var bottomLeftMax = min + halfScale3D;

            var topLeftMin = min + new float3(0.0f, 0.0f, halfScale3D.z);
            var topLeftMax = topLeftMin + halfScale3D;

            var topRightMin = min + halfScale3D;
            var topRightMax = topRightMin + halfScale3D;

            var bottomRightMin = min + new float3(halfScale3D.x, 0.0f, 0.0f);
            var bottomRightMax = bottomRightMin + halfScale3D;

            if (root != null)
            {
                var nodes = this.quadtree.GetNodes();
                if (root->children[0] != 0)
                {
                    this.DrawHierarchyRecursion(nodes[root->children[0]], bottomLeftMin, bottomLeftMax, 0);
                }

                if (root->children[1] != 0)
                {
                    this.DrawHierarchyRecursion(nodes[root->children[1]], bottomRightMin, bottomRightMax, 0);
                }

                if (root->children[2] != 0)
                {
                    this.DrawHierarchyRecursion(nodes[root->children[2]], topLeftMin, topLeftMax, 0);
                }

                if (root->children[3] != 0)
                {
                    this.DrawHierarchyRecursion(nodes[root->children[3]], topRightMin, topRightMax, 0);
                }
            }

            Handles.color = oldColor;
        }

        private void DrawBuckets()
        {
            var dataBuckets = this.quadtree.GetDataBuckets();
            var min = this.quadtree.GetBottomLeftPosition();
            int maxDepth = this.quadtree.GetMaxDepth();
            var scale = this.quadtree.GetScale();
            int axisDivisions = 1 << maxDepth;

            Vector2 squareSize = scale / (float)axisDivisions;

            var oldColor = Handles.color;
            Handles.color = this.squareColor;

            foreach(var dataBucket in dataBuckets)
            {

                uint code = dataBucket.Key;

                var position = MathUtil.QuadtreeCellToCoord(code);

                float percentX = position.x / (float)axisDivisions;
                float percentY = position.y / (float)axisDivisions;

                Vector3 worldPosMin = min + new float3(scale.x * percentX, this.height, scale.y * percentY);
                Vector3 worldPosMax = worldPosMin + new Vector3(squareSize.x, 0.0f, squareSize.y);

                Handles.DrawAAConvexPolygon(worldPosMin,
                    new Vector3(worldPosMax.x, worldPosMin.y, worldPosMin.z),
                    worldPosMax,
                    new Vector3(worldPosMin.x, worldPosMin.y, worldPosMax.z));
            }


            Handles.color = oldColor;
        }

        private void Draw()
        {
            this.DrawBorder();
            this.DrawHierarchy();
            this.DrawBuckets();
        }


        public void DrawCellsInRadius(float3 worldPos, float searchRadius, Color circleColor, Color cellColor)
        {
            NativeList<uint> cells = new NativeList<uint>(1, Allocator.TempJob);
            var job = this.quadtree.GetCellsInRadius(new float2(worldPos.x, worldPos.z), searchRadius, ref cells, default);
            job.Complete();

            var oldColor = Handles.color;
            Handles.color = circleColor;

            Handles.DrawWireDisc(worldPos, Vector3.up, searchRadius);

            Handles.color = cellColor;
            for(int i = 0; i < cells.Length; i++)
            {
                this.DrawSquare(cells[i], Color.white, cellColor);
            }

            Handles.color = oldColor;
            cells.Dispose();
        }

        public void DrawCellsInRectangle(Rect rectangle, Color rectangleColor, Color cellColor)
        {
            NativeList<uint> cells = new NativeList<uint>(1, Allocator.TempJob);
            var job = this.quadtree.GetCellsInRectangle(rectangle, ref cells, default);
            job.Complete();

            var oldColor = Handles.color;
            Handles.color = rectangleColor;

            var center = rectangle.center;

            Handles.DrawWireCube(new Vector3(center.x, this.height, center.y), new Vector3(rectangle.width, 0.0f, rectangle.height));

            for(int i = 0; i < cells.Length; i++)
            {
                this.DrawSquare(cells[i], Color.white, cellColor);
            }

            Handles.color = cellColor;

            Handles.color = oldColor;
            cells.Dispose();
        }

        public void OnSceneGUI()
        {

            this.Draw();
        }

    }
}
