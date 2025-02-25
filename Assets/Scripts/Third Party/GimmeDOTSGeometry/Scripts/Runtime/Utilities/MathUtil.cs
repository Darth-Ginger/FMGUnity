using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class MathUtil
    {
        /// <summary>
        /// Same as the %-operator, but handles negative numbers (on either side of the operation)
        /// </summary>
        /// <param name="k"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public static float Mod(float k, float n)
        {
            if (n == 0.0f)
            {
                Debug.LogError("[Maths]: Trying to do (a mod 0), which is undefined.");
            }

            float r = k % n;

            if ((n > 0 && r < 0)
                || (n < 0 && r > 0))
            {
                return r + n;
            }

            return r;
        }

        /// <summary>
        /// Same as the %-operator, but handles negative numbers (on either side of the operation)
        /// </summary>
        /// <param name="k"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public static int Mod(int k, int n)
        {
            if (n == 0)
            {
                Debug.LogError("[Maths]: Trying to do (a mod 0), which is undefined.");
            }

            int r = k % n;

            if ((n > 0 && r < 0)
                || (n < 0 && r > 0))
            {
                return r + n;
            }

            return r;

        }

        //Morton Decode
        //Used for converting quadtree depth-first traversal ids to (x, y, z) - Coordinates
        /// <summary>
        /// Converts a value [*x*x*x*x] -> [****xxxx]
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint CompactBitsBy2(uint value)
        {
            value &= 0b01010101010101010101010101010101;
            value = (value ^ (value >> 1)) & 0x33333333;
            value = (value ^ (value >> 2)) & 0x0f0f0f0f;
            value = (value ^ (value >> 4)) & 0x00ff00ff;
            value = (value ^ (value >> 8)) & 0x0000FFFF;

            return value;
        }


        //Morton Decode
        //Used for converting octree depth-first traversal ids to (x, y, z) - Coordinates
        /// <summary>
        /// Converts a value [*x**x**x] -> [*****xxx]
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint CompactBitsBy3(uint value)
        {
            value &= 0b01001001001001001001001001001001;
            value = (value ^ (value >> 2)) & 0x430C30C3;
            value = (value ^ (value >> 4)) & 0x0700F00F;
            value = (value ^ (value >> 8)) & 0x000700FF;
            value = (value ^ (value >> 8)) & 0x000007FF;

            return value;
        }

        /// <summary>
        /// Converts a value [****xxxx] -> [*x*x*x*x]
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint SplitBitsBy2(uint value)
        {
            value &= 0x0000FFFF;
            value = (value ^ (value << 8)) & 0x00FF00FF;
            value = (value ^ (value << 4)) & 0x0F0F0F0F;
            value = (value ^ (value << 2)) & 0x33333333;
            value = (value ^ (value << 1)) & 0x55555555;

            return value;
        }

        /// <summary>
        /// Converts a value [*****xxx] -> [*x**x**x]
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint SplitBitsBy3(uint value)
        {
            value &= 0x000003FF;
            value = (value ^ (value << 16)) & 0xFF0000FF;
            value = (value ^ (value << 8)) & 0x0300F00F;
            value = (value ^ (value << 4)) & 0x030C30C3;
            value = (value ^ (value << 2)) & 0x09249249;

            return value;
        }

        /// <summary>
        /// Combines, then transforms the x- and y-coordinates from [yyyyxxxx] to [yxyxyxyx] (for 32 bits) (conceptually)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint PositionToMortonCode(Vector2Int value)
        {
            return SplitBitsBy2((uint)value.x) | (SplitBitsBy2((uint)value.y) << 1);
        }

        /// <summary>
        /// Combines, then transforms the x-, y- and z-coordinates from [zzzyyyxxx] to [zyxzyxzyx] (for 32 bits) (conceptually)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint PositionToMortonCode(Vector3Int value)
        {
            return SplitBitsBy3((uint)value.x) | (SplitBitsBy3((uint)value.z) << 1) | (SplitBitsBy3((uint)value.y) << 2);
        }

        /// <summary>
        /// Decodes a binary value of form [yxyxyxyx] to [yyyyxxxx] and stores the result into a vector
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Vector2Int QuadtreeCellToCoord(uint value)
        {
            int x = (int)CompactBitsBy2(value);
            int y = (int)CompactBitsBy2(value >> 1);
            
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Decodes a binary value of form [zyxzyxzyx] to [zzzyyyxxx] and stores the result into a vector
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Vector3Int OctreeCellToCoord(uint value)
        {

            int x = (int)CompactBitsBy3(value);
            int y = (int)CompactBitsBy3(value >> 2);
            int z = (int)CompactBitsBy3(value >> 1);

            return new Vector3Int(x, y, z);
        }



    }
}