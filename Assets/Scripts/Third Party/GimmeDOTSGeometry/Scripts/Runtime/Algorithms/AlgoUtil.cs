namespace GimmeDOTSGeometry
{
    public static class AlgoUtil 
    {
        public static void Swap<T>(ref T a, ref T b)
        {
            var tmp = a;
            a = b; 
            b = tmp;
        }
    }
}
