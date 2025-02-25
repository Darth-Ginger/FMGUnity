using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class MathUtilDOTS 
    {
        /// <summary>
        /// Returns the distances between a and b in modulo space n
        /// </summary>
        /// <param name="k"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        // Small Example:
        // I.e. if n = 8, a = 1, b = 7
        // Then d0 = b - a = 6
        // Then d1 = n - d0 = 2
        // d0 is positive -> d1 = -2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 ModDist(int a, int b, int n)
        {
            int inA = Mod(a, n);
            int inB = Mod(b, n);

            int2 d = new int2();
            d.x = inB - inA;
            d.y = (n - math.abs(d.x));
            d.y = math.select(-d.y, d.y, d.x <= 0);

            return d;
        }

        /// <summary>
        /// Same as the %-operator, but handles negative numbers (on either side of the operation)
        /// </summary>
        /// <param name="k"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mod(int k, int n)
        {
            int r = k % n;
            return math.select(r, r + n, n * r < 0);
        }

        /// <summary>
        /// Converts a value [****xxxx] -> [*x*x*x*x]
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PositionToMortonCode(int2 value)
        {
            return SplitBitsBy2((uint)value.x) | (SplitBitsBy2((uint)value.y) << 1);
        }

        /// <summary>
        /// Combines, then transforms the x-, y- and z-coordinates from [zzzyyyxxx] to [zyxzyxzyx] (for 32 bits) (conceptually)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PositionToMortonCode(int3 value)
        {
            return SplitBitsBy3((uint)value.x) | (SplitBitsBy3((uint)value.z) << 1) | (SplitBitsBy3((uint)value.y) << 2);
        }

        //Morton Decode
        //Used for converting quadtree depth-first traversal ids to (x, y, z) - Coordinates
        /// <summary>
        /// Converts a value [*x*x*x*x] -> [****xxxx]
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// Decodes a binary value of form [yxyxyxyx] to [yyyyxxxx] and stores the result into a vector
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 Morton2CodeToPosition(uint value)
        {
            return new int2((int)CompactBitsBy2(value), (int)CompactBitsBy2(value >> 1));
        }

        /// <summary>
        /// Decodes a binary value of form [zyxzyxzyx] to [zzzyyyxxx] and stores the result into a vector
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 Morton3CodeToPosition(uint value)
        {
            return new int3((int)CompactBitsBy3(value), (int)CompactBitsBy3(value >> 2), (int)CompactBitsBy3(value >> 1));
        }

        public static int BinomialCoefficient(int n, int k)
        {
            float coefficient = n;
            for(int i = 2; i <= k; i++)
            {
                coefficient *= ((n + 1 - i) / (float)i);
            }
            return (int)math.round(coefficient);
        }

        public static int EdgeToHash(int edgeA, int edgeB)
        {
            if(edgeA < edgeB)
            {
                return (edgeB | (edgeA << 16));
            } else
            {
                return (edgeA | (edgeB << 16));
            }
        }


        /// <summary>
        /// Solves a quadtratic equation with coefficients a, b, and c
        /// If there is no solution, false is returned. If there is only
        /// one solution, both x0 and x1 are equal
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="x0"></param>
        /// <param name="x1"></param>
        /// <returns></returns>
        public static bool SolveQuadtratic(float a, float b, float c, out float x0, out float x1, float epsilon = 10e-5f)
        {
            x0 = float.NaN;
            x1 = float.NaN;

            if(math.abs(a) < epsilon)
            {
                if (math.abs(b) < epsilon) return false;
                else
                {
                    x0 = -c / b;
                    x1 = x0;
                    return true;
                }
            }

            float bSquared = b * b;
            float ac = 4.0f * a * c;
            float diff = bSquared - ac;

            if (diff < 0.0f) return false;

            float sqrt = math.sqrt(diff);
            x0 = (-b + sqrt) / (2.0f * a);
            x1 = (-b - sqrt) / (2.0f * a);

            return true;
        }

    }
}
