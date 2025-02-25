using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public struct Matrix3x3
    {
        private float3x3 data;

        private static readonly Matrix3x3 zeroMatrix = new Matrix3x3(Vector3.zero, Vector3.zero, Vector3.zero);
        private static readonly Matrix3x3 identityMatrix = new Matrix3x3(Vector3.right, Vector3.up, Vector3.forward);

        public static implicit operator float3x3(Matrix3x3 m)
        {
            return m.data;
        }

        public static implicit operator Matrix4x4(Matrix3x3 m)
        {
            float3 mC0 = m.data.c0;
            float3 mC1 = m.data.c1;
            float3 mC2 = m.data.c2;

            var column0 = new Vector4(mC0.x, mC0.y, mC0.z, 0.0f);
            var column1 = new Vector4(mC1.x, mC1.y, mC1.z, 0.0f);
            var column2 = new Vector4(mC2.x, mC2.y, mC2.z, 0.0f);
            var column3 = Vector4.zero;

            return new Matrix4x4(column0, column1, column2, column3);
        }

        public Matrix3x3(Vector3 column0, Vector3 column1, Vector3 column2)
        {
            this.data.c0 = column0;
            this.data.c1 = column1;
            this.data.c2 = column2; 
        }


        public static Matrix3x3 zero => zeroMatrix;

        public static Matrix3x3 identity => identityMatrix;

        //https://en.wikipedia.org/wiki/Transformation_matrix
        public static Matrix3x3 TRS(float2 translation, float rotation, float2 scale)
        {
            Matrix3x3 m = new Matrix3x3();
            math.sincos(rotation, out float sin, out float cos);

            float3 c0, c1, c2;

            c0.x = scale.x * cos;
            c0.y = scale.x * sin;
            c0.z = 0.0f;

            c1.x = -scale.y * sin;
            c1.y = scale.y * cos;
            c1.z = 0.0f;

            c2.x = translation.x;
            c2.y = translation.y;
            c2.z = 1.0f;

            m.data = new float3x3(c0, c1, c2);
            

            return m;
        }

        public Vector2 MultiplyPoint(Vector2 point)
        {
            Vector2 result = new Vector2();
            Vector3 p = new Vector3(point.x, point.y, 1.0f);

            p = math.mul(this.data, p);
            result.x = p.x;
            result.y = p.y;

            return result;
        }

    }
}
