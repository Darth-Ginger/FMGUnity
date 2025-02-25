using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class MeshUVUtil 
    {
        /// <summary>
        /// Creates a set of four UV coordinates, for each corner of a rectangle
        /// </summary>
        /// <returns></returns>
        public static Vector2[] DefaultRectangleUVs()
        {
            var uvs = new Vector2[4];

            uvs[0] = new Vector2(0, 0);
            uvs[1] = new Vector2(0, 1);
            uvs[2] = new Vector2(1, 1);
            uvs[3] = new Vector2(1, 0);

            return uvs;
        }

    }
}
