using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe class Native2DKDTreeHandle
    {

        #region Public Variables

        #endregion

        #region Private Variables



        private Color boundsColor;
        private Color hierarchyColor;
        private Color positionsColor;

        private float height = 0.0f;
        private float positionsRadius = 0.1f;

        private Native2DKDTree<Position3D> kdtree;

        #endregion

        public float GetHeight() => this.height;

        public Native2DKDTreeHandle(Native2DKDTree<Position3D> kdtree, Color boundsColor, Color hierarchyColor, Color positionsColor, float positionsRadius = 0.1f, float height = 0.0f)
        {
            this.kdtree = kdtree;
            this.height = height;
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

            float3 bottomLeft, bottomRight, topLeft, topRight;
            bottomLeft = float3.zero;
            bottomRight = float3.zero;
            topLeft = float3.zero;
            topRight = float3.zero;

            int axis0 = this.kdtree.GetAxis0();
            int axis1 = this.kdtree.GetAxis1();

            int remainingAxis = 2;

            if (axis0 == 0 && axis1 == 1) remainingAxis = 2;
            else if (axis0 == 0 && axis1 == 2) remainingAxis = 1;
            else if (axis0 == 1 && axis1 == 2) remainingAxis = 0;

            bottomLeft[axis0] = bounds.xMin;
            bottomLeft[axis1] = bounds.yMin;
            bottomLeft[remainingAxis] = this.height;

            bottomRight[axis0] = bounds.xMax;
            bottomRight[axis1] = bounds.yMin;
            bottomLeft[remainingAxis] = this.height;

            topLeft[axis0] = bounds.xMin;
            topLeft[axis1] = bounds.yMax;
            bottomLeft[remainingAxis] = this.height;

            topRight[axis0] = bounds.xMax;
            topRight[axis1] = bounds.yMax;
            bottomLeft[remainingAxis] = this.height;

            Handles.DrawAAPolyLine(bottomLeft, bottomRight, topRight, topLeft, bottomLeft);
            Handles.color = oldColor;
        }


        private void DrawHierarchyRecursion(int nodeIdx, Rect currentBounds, int depth)
        {

            float3 start = float3.zero;
            float3 end = float3.zero;

            int axis0 = this.kdtree.GetAxis0();
            int axis1 = this.kdtree.GetAxis1();

            int remainingAxis = 2;

            if (axis0 == 0 && axis1 == 1) remainingAxis = 2;
            else if (axis0 == 0 && axis1 == 2) remainingAxis = 1;
            else if (axis0 == 1 && axis1 == 2) remainingAxis = 0;

            start[remainingAxis] = this.height;
            end[remainingAxis] = this.height;

            var nodes = this.kdtree.GetNodes();

            var node = nodes[nodeIdx];
            if (depth % 2 == 0)
            {
                start[axis0] = node.Position[axis0];
                start[axis1] = currentBounds.yMin;

                end[axis0] = node.Position[axis0];
                end[axis1] = currentBounds.yMax;

            } else
            {
                start[axis0] = currentBounds.xMin;
                start[axis1] = node.Position[axis1];

                end[axis0] = currentBounds.xMax;
                end[axis1] = node.Position[axis1];
            }

            Handles.color = this.hierarchyColor;

            Handles.DrawLine(start, end);

            Handles.color = this.positionsColor;

            Vector3 normal = Vector3.zero;
            normal[remainingAxis] = 1.0f;

            Vector3 position = node.Position;
            position[remainingAxis] = this.height;

            Handles.DrawWireDisc(position, normal, this.positionsRadius); ;


            int axis = depth % 2 == 0 ? axis0 : axis1;

            float splitPlane = node.Position[axis];

            var bounds0 = currentBounds;
            var bounds1 = currentBounds;

            if (axis == 0)
            {
                bounds0.xMax = splitPlane;
                bounds1.xMin = splitPlane;
            }
            else
            {
                bounds0.yMax = splitPlane;
                bounds1.yMin = splitPlane;
            }

            int left = nodeIdx * 2 + 1;
            int right = nodeIdx * 2 + 2;
            if (left < nodes.Length)
            {
                this.DrawHierarchyRecursion(left, bounds0, depth + 1);
            }

            if(right < nodes.Length)
            {
                this.DrawHierarchyRecursion(right, bounds1, depth + 1);
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

            var oldColor = Handles.color;
            Handles.color = circleColor;

            int axis0 = this.kdtree.GetAxis0();
            int axis1 = this.kdtree.GetAxis1();


            int remainingAxis = 2;

            if (axis0 == 0 && axis1 == 1) remainingAxis = 2;
            else if (axis0 == 0 && axis1 == 2) remainingAxis = 1;
            else if (axis0 == 1 && axis1 == 2) remainingAxis = 0;

            Vector3 normal = Vector3.zero;
            normal[remainingAxis] = 1.0f;


            Vector3 centerPos = worldPos;
            centerPos[remainingAxis] = this.height;

            Handles.DrawWireDisc(centerPos, normal, searchRadius);

            Handles.color = resultColor;

            for (int i = 0; i < result.Length; i++)
            {
                var position = result[i];
                float3 discPos = position.Position;
                discPos[remainingAxis] = this.height;
                Handles.DrawWireDisc(discPos, normal, this.positionsRadius);
            }

            Handles.color = oldColor;
            result.Dispose();
        }

        public void DrawPositionsInRectangle(Rect rectangle, Color rectangleColor, Color resultColor)
        {
            NativeList<Position3D> result = new NativeList<Position3D>(1, Allocator.TempJob);
            var job = this.kdtree.GetPointsInRectangle(rectangle, ref result);
            job.Complete();

            var oldColor = Handles.color;
            Handles.color = rectangleColor;

            int axis0 = this.kdtree.GetAxis0();
            int axis1 = this.kdtree.GetAxis1();

            int remainingAxis = 2;

            if (axis0 == 0 && axis1 == 1) remainingAxis = 2;
            else if (axis0 == 0 && axis1 == 2) remainingAxis = 1;
            else if (axis0 == 1 && axis1 == 2) remainingAxis = 0;

            Vector3 normal = Vector3.zero;
            normal[remainingAxis] = 1.0f;

            var center = rectangle.center;

            Vector3 centerPos = Vector3.zero;
            centerPos[axis0] = center.x;
            centerPos[axis1] = center.y;
            centerPos[remainingAxis] = this.height;

            Vector3 size = Vector3.zero;
            size[axis0] = rectangle.width;
            size[axis1] = rectangle.height;
            size[remainingAxis] = 0.0f;

            Handles.DrawWireCube(centerPos, size);

            Handles.color = resultColor;
            for (int i = 0; i < result.Length; i++)
            {
                var position = result[i];
                float3 discPos = position.Position;
                discPos[remainingAxis] = this.height;
                Handles.DrawWireDisc(discPos, normal, this.positionsRadius); ;
            }

            Handles.color = oldColor;
            result.Dispose();
        }

        public void OnSceneGUI()
        {
            this.Draw();
        }
        
    }
}
