using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class LineRendererExtensions 
    {


        public static void SetPositionsFromBounds(this LineRenderer lineRenderer, Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            Vector3[] positions = new Vector3[]
            {
                min,
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z),
                min,
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z),
                max,
                new Vector3(max.x, min.y, max.z),
                max,
                new Vector3(min.x, max.y, max.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(min.x, max.y, min.z),
            };

            lineRenderer.positionCount = positions.Length;
            lineRenderer.SetPositions(positions);
        }

    }
}
