using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe class NativeOctreeHandle<T> where T : unmanaged
    {

        #region Public Variables

        #endregion

        #region Private Variables


        private Color boundsColor;
        private Color hierarchyColor;
        private Color boxColor;
        private Color bucketColor;

        private IOctree<T> octree;

        #endregion

        public NativeOctreeHandle(IOctree<T> octree, Color boundsColor, Color hierarchyColor, Color boxColor, Color bucketColor)
        {
            this.octree = octree;
            this.boundsColor = boundsColor;
            this.hierarchyColor = hierarchyColor;
            this.boxColor = boxColor;
            this.bucketColor = bucketColor;
        }

        private void DrawBorder()
        {
            var oldColor = Handles.color;
            Handles.color = boundsColor;

            var bottomLeft = this.octree.GetBottomLeftPosition();
            var scale = this.octree.GetScale();

            Handles.DrawWireCube(bottomLeft + scale * 0.5f, scale);

            Handles.color = oldColor;
        }

        private void DrawBox(uint code, Color outlineColor)
        {
            var globalMin = this.octree.GetBottomLeftPosition();
            int maxDepth = this.octree.GetMaxDepth();
            var scale = this.octree.GetScale();
            int axisDivisions = 1 << maxDepth;

            Vector3 boxSize = scale / (float)axisDivisions;

            var position = MathUtil.OctreeCellToCoord(code);

            float percentX = position.x / (float)axisDivisions;
            float percentY = position.y / (float)axisDivisions;
            float percentZ = position.z / (float)axisDivisions;

            Vector3 worldPosMin = (Vector3)globalMin + new Vector3(scale.x * percentX, scale.y * percentY, scale.z * percentZ);
            Vector3 worldPosMax = worldPosMin + boxSize;

            Handles.color = outlineColor;
            Handles.DrawWireCube((worldPosMin + worldPosMax) * 0.5f, boxSize);
        }

        private void DrawHierarchyRecursion(OctreeNode node, float3 min, float3 max, int depth)
        {
            Handles.color = this.hierarchyColor;

            Handles.DrawWireCube((min + max) * 0.5f, max - min);

            var bounds = new Bounds((min + max) * 0.5f, max - min);
            var subdividedBounds = bounds.Subdivide();

            var nodes = this.octree.GetNodes();

            for (int i = 0; i < subdividedBounds.Length; i++)
            {
                var subBounds = subdividedBounds[i];
                if (node.children[i] != 0)
                {
                    this.DrawHierarchyRecursion(nodes[node.children[i]], subBounds.min, subBounds.max, depth + 1);
                }
            }
        }

        private void DrawHierarchy()
        {
            var oldColor = Handles.color;

            var root = this.octree.GetRoot();

            
            var min = this.octree.GetBottomLeftPosition();
            var scale = this.octree.GetScale();
            var halfScale = scale * 0.5f;

            var bounds = new Bounds(min + halfScale, scale);
            var subdividedBounds = bounds.Subdivide();

            if (root != null)
            {
                var nodes = this.octree.GetNodes();
                for(int i = 0; i < subdividedBounds.Length; i++)
                {
                    var subBounds = subdividedBounds[i];
                    if (root->children[i] != 0)
                    {
                        this.DrawHierarchyRecursion(nodes[root->children[i]], subBounds.min, subBounds.max, 0);
                    }
                }
            }

            Handles.color = oldColor;
        }

        private void DrawBuckets()
        {
            Vector3 normal = Vector3.up;
            if(SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                normal = -SceneView.lastActiveSceneView.camera.transform.forward;
            }

            var dataBuckets = this.octree.GetDataBuckets();
            var min = this.octree.GetBottomLeftPosition();
            int maxDepth = this.octree.GetMaxDepth();
            var scale = this.octree.GetScale();
            int axisDivisions = 1 << maxDepth;

            Vector3 boxSize = scale / (float)axisDivisions;

            var oldColor = Handles.color;
            Handles.color = this.bucketColor;

            foreach(var dataBucket in dataBuckets)
            {

                uint code = dataBucket.Key;

                var position = MathUtil.OctreeCellToCoord(code);

                float percentX = position.x / (float)axisDivisions;
                float percentY = position.y / (float)axisDivisions;
                float percentZ = position.z / (float)axisDivisions;

                Vector3 worldPosCenter = min + new float3(scale.x * percentX, scale.y * percentY, scale.z * percentZ) + 0.5f * (float3)boxSize;

                Handles.DrawWireDisc(worldPosCenter, normal, math.cmax(boxSize) * 0.5f);
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

            Vector3 normal = Vector3.up;
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                normal = -SceneView.lastActiveSceneView.camera.transform.forward;
            }
            
            NativeList<uint> cells = new NativeList<uint>(1, Allocator.TempJob);
            var job = this.octree.GetCellsInRadius(worldPos, searchRadius, ref cells, default);
            job.Complete();

            var oldColor = Handles.color;
            Handles.color = circleColor;

            Handles.DrawWireDisc(worldPos, normal, searchRadius);

            Handles.color = cellColor;
            for(int i = 0; i < cells.Length; i++)
            {
                this.DrawBox(cells[i], Color.white);
            }

            Handles.color = oldColor;
            cells.Dispose();
        }

        public void DrawCellsInBounds(Bounds bounds, Color boundsColor, Color cellColor)
        {
            NativeList<uint> cells = new NativeList<uint>(1, Allocator.TempJob);
            var job = this.octree.GetCellsInBounds(bounds, ref cells, default);
            job.Complete();

            var oldColor = Handles.color;
            Handles.color = boundsColor;

            var center = bounds.center;

            Handles.DrawWireCube(center, bounds.size);

            for(int i = 0; i < cells.Length; i++)
            {
                this.DrawBox(cells[i], Color.white);
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
