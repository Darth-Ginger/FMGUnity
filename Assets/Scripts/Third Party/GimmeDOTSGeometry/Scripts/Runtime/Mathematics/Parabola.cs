namespace GimmeDOTSGeometry
{
    public struct Parabola : IGraphFunction
    {
        public float a;
        public float b;
        public float c;

        public Parabola(float a, float b, float c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public float Evaluate(float x)
        {
            return this.a * x * x 
                 + this.b * x 
                 + this.c;
        }
    }
}
