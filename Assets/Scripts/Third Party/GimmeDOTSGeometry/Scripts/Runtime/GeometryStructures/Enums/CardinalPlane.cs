using UnityEngine;

namespace GimmeDOTSGeometry
{

    /// <summary>
    /// Describes a plane along a cardinal axis. E.g. if the cardinal axis is Y, the cardinal plane is XZ
    /// </summary>
    public enum CardinalPlane
    {
        XY = 0,
        XZ = 1,
        ZY = 2
    }

    public static class CardinalPlaneExtensions {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sortMode"></param>
        /// <returns>Returns the indices of the two cardinal axis that lie in the cardinal plane</returns>
        public static Vector2Int GetAxisIndices(this CardinalPlane plane)
        {
            Vector2Int axis = new Vector2Int();
            switch (plane)
            {
                case CardinalPlane.XY:
                    axis.x = 0;
                    axis.y = 1;
                    break;
                case CardinalPlane.XZ:
                    axis.x = 0;
                    axis.y = 2;
                    break;
                default:
                case CardinalPlane.ZY:
                    axis.x = 2;
                    axis.y = 1;
                    break;
            }
            return axis;
        }

        public static int GetNormalDirectionIndex(this CardinalPlane plane)
        {
            int direction = 1;
            switch (plane)
            {
                case CardinalPlane.XZ:
                    direction = 1;
                    break;
                case CardinalPlane.XY:
                    direction = 2;
                    break;
                case CardinalPlane.ZY:
                    direction = 0;
                    break;
            }
            return direction;
        }

    }
}
