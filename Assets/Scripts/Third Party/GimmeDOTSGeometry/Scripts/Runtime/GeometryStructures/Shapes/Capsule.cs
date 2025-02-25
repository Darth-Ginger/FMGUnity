using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public struct Capsule
    {

        public float3 a;
        public float3 b;

        public float radius;

        public Capsule(float3 a, float3 b, float radius)
        {
            this.a = a;
            this.b = b;
            this.radius = radius;
        }

        public Capsule(LineSegment3D ls, float radius)
        {
            this.a = ls.a;
            this.b = ls.b;
            this.radius = radius;
        }

        public float Area()
        {
            float height = math.distance(this.a, this.b);
            return 2.0f * this.radius * math.PI * height + 4 * math.PI * this.radius * this.radius;
        }

        public float Volume()
        {
            return (4.0f / 3.0f) * math.pow(this.radius, 3.0f) * math.PI 
                + this.radius * this.radius * math.PI * math.distance(this.a, this.b);
        }
    }
}
