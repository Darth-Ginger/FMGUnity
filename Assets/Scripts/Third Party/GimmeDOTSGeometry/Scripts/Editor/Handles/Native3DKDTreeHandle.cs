using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe class Native3DKDTreeHandle
    {

        #region Public Variables

        #endregion

        #region Private Variables



        private Color boundsColor;
        private Color hierarchyColor;
        private Color positionsColor;


        private float positionsRadius = 0.1f;

        private Native3DKDTree<Position3D> kdtree;

        #endregion


        public Native3DKDTreeHandle(Native3DKDTree<Position3D> kdtree, Color boundsColor, Color hierarchyColor, Color positionsColor, float positionsRadius = 0.1f)
        {
            this.kdtree = kdtree;
            this.boundsColor = boundsColor;
            this.hierarchyColor = hierarchyColor;
            this.positionsColor = positionsColor;
            this.positionsRadius = positionsRadius;
        }

        private void DrawBorder()
        {
            var oldColor = Handles.color;
            Handles.color = boundsColor;

            var bounds = this.kdtree.GetBounds();

            Handles.DrawWireCube(bounds.center, bounds.size);

            Handles.color = oldColor;
        }


        private void DrawHierarchyRecursion(int currentNodeIdx, Bounds currentBounds, int depth)
        {
            var nodes = this.kdtree.GetNodes();
            float3 node = nodes[currentNodeIdx].Position;
            int axis = depth % 3;

            float splitPlane = node[axis];

            float3 minLeft = currentBounds.min;
            float3 maxLeft = currentBounds.max;
            float3 minRight = currentBounds.min;
            float3 maxRight = currentBounds.max;

            maxLeft[axis] = splitPlane;
            minRight[axis] = splitPlane;

            var color = this.hierarchyColor;
            color.a = color.a * Mathf.Pow(1.0f - (1.0f / Mathf.Log(this.kdtree.GetNodes().Length)), depth);
            Handles.color = color;

            Vector3[] planePoints = new Vector3[5];
            Vector3 normal = Vector3.zero;

            switch(axis)
            {
                case 0:
                    planePoints[0] = new Vector3(maxLeft.x, minLeft.y, minLeft.z);
                    planePoints[1] = new Vector3(maxLeft.x, maxLeft.y, minLeft.z);
                    planePoints[2] = new Vector3(maxLeft.x, maxLeft.y, maxLeft.z);
                    planePoints[3] = new Vector3(maxLeft.x, minLeft.y, maxLeft.z);
                    normal = Vector3.right;
                    break;
                case 1:
                    planePoints[0] = new Vector3(minLeft.x, maxLeft.y, minLeft.z);
                    planePoints[1] = new Vector3(maxLeft.x, maxLeft.y, minLeft.z);
                    planePoints[2] = new Vector3(maxLeft.x, maxLeft.y, maxLeft.z);
                    planePoints[3] = new Vector3(minLeft.x, maxLeft.y, maxLeft.z);
                    normal = Vector3.up;
                    break;
                case 2:
                    planePoints[0] = new Vector3(minLeft.x, minLeft.y, maxLeft.z);
                    planePoints[1] = new Vector3(maxLeft.x, minLeft.y, maxLeft.z);
                    planePoints[2] = new Vector3(maxLeft.x, maxLeft.y, maxLeft.z);
                    planePoints[3] = new Vector3(minLeft.x, maxLeft.y, maxLeft.z);
                    normal = Vector3.forward;
                    break;
            }

            planePoints[4] = planePoints[0];

            Handles.DrawAAPolyLine(planePoints);

            Handles.color = this.positionsColor;

            Handles.DrawWireDisc(node, normal, this.positionsRadius); ;


            var boundsLeft = new Bounds();
            var boundsRight = new Bounds();

            boundsLeft.SetMinMax(minLeft, maxLeft);
            boundsRight.SetMinMax(minRight, maxRight);

            int left = currentNodeIdx * 2 + 1;
            int right = currentNodeIdx * 2 + 2;
            if (left < nodes.Length)
            {
                this.DrawHierarchyRecursion(left, boundsLeft, depth + 1);
            }

            if(right < nodes.Length)
            {
                this.DrawHierarchyRecursion(right, boundsRight, depth + 1);
            }
        }

        private void DrawHierarchy()
        {
            var oldColor = Handles.color;


            var bounds = this.kdtree.GetBounds();
            this.DrawHierarchyRecursion(0, bounds, 0);
            

            Handles.color = oldColor;
        }
        
        private void Draw()
        {
            this.DrawBorder();
            this.DrawHierarchy();
        }

        
        public void DrawPositionsInRadius(float3 worldPos, float searchRadius, Color circleColor, Color resultColor)
        {

            NativeList<Position3D> result = new NativeList<Position3D>(1, Allocator.TempJob);
            var job = this.kdtree.GetPointsInRadius(worldPos, searchRadius, ref result);
            job.Complete();

            if (SceneView.currentDrawingSceneView != null && SceneView.currentDrawingSceneView.camera != null)
            {
                var camera = SceneView.currentDrawingSceneView.camera;
                var oldColor = Handles.color;
                Handles.color = circleColor;

                Vector3 normal = -camera.transform.forward;

                Handles.DrawWireDisc(worldPos, normal, searchRadius);

                Handles.color = resultColor;

                for (int i = 0; i < result.Length; i++)
                {
                    var position = result[i];
                    Handles.DrawWireDisc(position.Position, normal, this.positionsRadius);
                }

                Handles.color = oldColor;
            }
            result.Dispose();
        }

        public void DrawPositionsInRectangle(Bounds bounds, Color boundsColor, Color resultColor)
        {
            NativeList<Position3D> result = new NativeList<Position3D>(1, Allocator.TempJob);
            var job = this.kdtree.GetPointsInBounds(bounds, ref result);
            job.Complete();

            if (SceneView.currentDrawingSceneView != null && SceneView.currentDrawingSceneView.camera != null)
            {
                var camera = SceneView.currentDrawingSceneView.camera;
                var oldColor = Handles.color;
                Handles.color = boundsColor;

                Vector3 normal = -camera.transform.forward;

                Handles.DrawWireCube(bounds.center, bounds.size);

                Handles.color = resultColor;
                for (int i = 0; i < result.Length; i++)
                {
                    var position = result[i];
                    Handles.DrawWireDisc(position.Position, normal, this.positionsRadius);
                }

                Handles.color = oldColor;

            }
            result.Dispose();
        }

        public void OnSceneGUI()
        {
            this.Draw();
        }
        
    }
}
