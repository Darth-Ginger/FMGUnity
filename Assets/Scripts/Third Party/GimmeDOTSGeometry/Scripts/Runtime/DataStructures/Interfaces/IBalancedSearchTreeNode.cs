namespace GimmeDOTSGeometry
{
    public interface IBalancedSearchTreeNode<T>
    {
        public int Parent { get; set; }
        public int Left { get; set; }
        public int Right { get; set; }

        public T Value { get; set; }
    }
}
