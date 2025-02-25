using Unity.Collections;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class PolygonTriangulationLinesDrawer : MonoBehaviour
    {

        private NativePolygon2D polygon;
        private NativeList<int> triangulation;

        public void Init(NativePolygon2D polygon, NativeList<int> triangulation)
        {
            this.polygon = polygon;
            this.triangulation = triangulation;
        }

        //Make sure to enable Gizmos in playmode when running tests
        private void OnDrawGizmos()
        {
            var oldColor = Gizmos.color;

            Gizmos.color = Color.black;
            for(int i = 0; i < this.triangulation.Length; i+=3)
            {
                int idxA = this.triangulation[i];
                int idxB = this.triangulation[i + 1];
                int idxC = this.triangulation[i + 2];

                var a = this.polygon.points[idxA];
                var b = this.polygon.points[idxB];
                var c = this.polygon.points[idxC];

                var a3D = new Vector3(a.x, 0.0f, a.y);
                var b3D = new Vector3(b.x, 0.0f, b.y);
                var c3D = new Vector3(c.x, 0.0f, c.y);

                Gizmos.DrawLine(a3D, b3D);
                Gizmos.DrawLine(b3D, c3D);
                Gizmos.DrawLine(c3D, a3D);
            }
            Gizmos.color = oldColor;
        }

    }
}
