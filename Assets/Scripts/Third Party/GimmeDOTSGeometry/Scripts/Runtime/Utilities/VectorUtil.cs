using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;


namespace GimmeDOTSGeometry
{

    public static class VectorUtil
    {



        /// <summary>
        /// Returns the cosine between the angle of the two vectors. This is useful for applying the law of cosines sometimes
        /// (because it avoids cos(acos(...)) with vectors)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static float Cos(float2 a, float2 b)
        {
            float dot = math.dot(a, b);
            if (dot < 10e-7f) return 0.0f;
            return dot / math.sqrt(math.lengthsq(a) * math.lengthsq(b));
        }

        /// <summary>
        /// Returns the cosine between the angle of the two vectors. This is useful for applying the law of cosines sometimes
        /// (because it avoids cos(acos(...)) with vectors)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static float Cos(float2 a, float2 b, float lengthA, float lengthB)
        {
            float dot = math.dot(a, b);
            if (dot == 10e-7f) return 0.0f; 
            return dot / math.sqrt(lengthA * lengthB);
        }

        /// <summary>
        /// Returns the cosine between the angle of the two vectors. This is useful for applying the law of cosines sometimes
        /// (because it avoids cos(acos(...)) with vectors)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static float Cos(float3 a, float3 b)
        {
            float dot = math.dot(a, b);
            if (dot == 10e-7f) return 0.0f;
            return dot / math.sqrt(math.lengthsq(a) * math.lengthsq(b));
        }

        /// <summary>
        /// Returns the cosine between the angle of the two vectors. This is useful for applying the law of cosines sometimes
        /// (because it avoids cos(acos(...)) with vectors)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static float Cos(float3 a, float3 b, float lengthA, float lengthB)
        {
            float dot = math.dot(a, b);
            if (dot == 10e-7f) return 0.0f;
            return dot / math.sqrt(lengthA * lengthB);
        }

        public static float Angle(float2 a, float2 b)
        {
            float dot = math.dot(a, b);
            if (dot == 10e-7f) return math.PI / 2.0f;
            float cos = dot / math.sqrt(math.lengthsq(a) * math.lengthsq(b));
            return math.acos(cos);
        }

        public static float Angle(float2 a, float2 b, float lengthA, float lengthB)
        {
            float dot = math.dot(a, b);
            if (dot == 10e-7f) return math.PI / 2.0f;
            float cos = dot / (lengthA * lengthB);
            return math.acos(cos);
        }

        public static float Angle(float3 a, float3 b)
        {
            float dot = math.dot(a, b);
            if (dot == 10e-7f) return math.PI / 2.0f;
            float cos = dot / math.sqrt(math.lengthsq(a) * math.lengthsq(b));
            return math.acos(cos);
        }

        public static float Angle(float3 a, float3 b, float lengthA, float lengthB)
        {
            float dot = math.dot(a, b);
            if (dot == 10e-7f) return math.PI / 2.0f;
            float cos = dot / (lengthA * lengthB);
            return math.acos(cos);
        }

        /// <summary>
        /// Checks wether a direction is left or right from a given orientation
        /// </summary>
        /// <param name="forward"></param>
        /// <param name="up"></param>
        /// <param name="targetDirection"></param>
        /// <returns>Returns -1 if targetDirection is left of orientation, 1 if it is right, 
        /// and 0 if it is either the same direction or the opposite direction</returns>
        public static int CompareDirection(Vector3 forward, Vector3 up, Vector3 targetDirection)
        {
            var cross = Vector3.Cross(forward, targetDirection);
            float dot = Vector3.Dot(cross, up);

            if (dot == 0.0f)
            {
                return 0;
            }
            else if (dot > 0.0f)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }


        /// <summary>
        /// Checks wether three points (in a row) make a left or a right turn
        /// </summary>
        /// <param name="forward"></param>
        /// <param name="up"></param>
        /// <param name="targetDirection"></param>
        /// <returns>Returns -1 if it is a left turn, 1 if it is a right turn, 
        /// and 0 if they are colinear</returns>
        public static int TurnDirection(Vector2 a, Vector2 b, Vector2 c)
        {
            var dirA = a - b;
            var dirB = c - b;
            
            float angle = Vector2.SignedAngle(dirB, dirA);
            
            return -(int)math.sign(angle);
        }


        /// <summary>
        /// Checks wether a point is left or right from a line given by start and end
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="point"></param>
        /// <returns>Return -1 if the point is left of the line, 1 if it is right from it
        /// and 0 if it is on the line (stretched to infinity)</returns>
        public static int CompareLineDirection(Vector2 start, Vector2 end, Vector2 point)
        {
            //3x3 Matrix determinant. First column is all 1s
            float det = ((end.x * point.y) - (point.x * end.y))
                - start.x * (point.y - end.y)
                + start.y * (point.x - end.x);
            return -(int)Mathf.Sign(det);
        }

        /// <summary>
        /// Checks wether a point is left or right from a line given by start and end
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="point"></param>
        /// <returns>Return -1 if the point is left of the line, 1 if it is right from it
        /// and 0 if it is on the line (stretched to infinity)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareLineDirection(float2 start, float2 end, float2 point)
        {
            //3x3 Matrix determinant. First column is all 1s
            float det = ((end.x * point.y) - (point.x * end.y))
                - start.x * (point.y - end.y)
                + start.y * (point.x - end.x);
            return -(int)math.sign(det);
        }

        public static float ScalarProjection(Vector2 dirA, Vector2 dirB)
        {
            return (Vector2.Dot(dirA, dirB) / (Vector2.Dot(dirB, dirB)));
        }

        public static Vector2 VectorProjection(Vector2 dirA, Vector2 dirB)
        {
            float scalar = (Vector2.Dot(dirA, dirB) / Vector2.Dot(dirB, dirB));
            return scalar * dirB;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ScalarProjection(float2 dirA, float2 dirB)
        {
            return math.dot(dirA, dirB) / math.dot(dirB, dirB);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ScalarProjection(float3 dirA, float3 dirB)
        {
            return math.dot(dirA, dirB) / math.dot(dirB, dirB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 VectorProjection(float2 dirA, float2 dirB)
        {
            return (math.dot(dirA, dirB) / math.dot(dirB, dirB)) * dirB;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 VectorProjection(float3 dirA, float3 dirB)
        {
            return (math.dot(dirA, dirB) / math.dot(dirB, dirB)) * dirB;
        }
    }
}