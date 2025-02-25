using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    //Not many methods here right now, but I am pretty sure at some point in the future there will be more
    //(e.g. I might need to do intersections of some polynomials or sine-curves... I am pretty sure... sigh)
    public static class GraphFunctionIntersections 
    {
        public static bool ParabolaIntersections(Parabola parA, Parabola parB, out float2 intersection0, out float2 intersection1)
        {
            float a = (parB.a - parA.a);
            float b = (parB.b - parA.b);
            float c = (parB.c - parA.c);

            intersection0 = float.NaN;
            intersection1 = float.NaN;

            bool exists = MathUtilDOTS.SolveQuadtratic(a, b, c, out float sX, out float tX);
            if (!exists) return false;

            float sY = parA.Evaluate(sX);
            float tY = parA.Evaluate(tX);

            intersection0 = new float2(sX, sY);
            intersection1 = new float2(tX, tY);

            return true;
        }

    }
}
