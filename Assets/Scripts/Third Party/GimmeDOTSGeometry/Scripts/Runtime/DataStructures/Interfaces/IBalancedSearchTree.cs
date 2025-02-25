using Unity.Collections;

namespace GimmeDOTSGeometry
{
    public interface IBalancedSearchTree<T>
    {
        public int RootIdx { get; }

        public IBalancedSearchTreeNode<T> GetNodeAt(int index);

        public void Insert(T value);

        public bool Remove(T value);

        public int GetHeight();

        public int GetElementIdx(T value);

        public int GetPredecessor(int nodeIdx);
        public int GetSuccessor(int nodeIdx);

        public void GetAllTreeElements(ref NativeList<int> nodeIndices, TreeTraversal traversal);
    }
}
