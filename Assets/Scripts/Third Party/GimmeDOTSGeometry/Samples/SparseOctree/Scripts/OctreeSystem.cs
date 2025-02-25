

using System.Collections.Generic;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class OctreeSystem : MonoBehaviour
    {

        #region Public Variables

        public float searchRadius;

        public GameObject diamond;

        public int initialDiamonds;

        public LineRenderer fieldBoundaryRenderer;
        public LineRenderer octreeBoundaryRenderer;

        public Bounds octreeBounds;
        public Bounds wrapAroundBounds;

        #endregion

        #region Private Variables

        private List<GameObject> diamonds = new List<GameObject>();

        private NativeSparseOctree<int> octree;

        #endregion

        public NativeSparseOctree<int> Octree => this.octree;

        public List<GameObject> Diamonds => this.diamonds;


        void Start()
        {
            var center = this.octreeBounds.center;
            this.octree = new NativeSparseOctree<int>(center, this.octreeBounds.size, 6, 128, Unity.Collections.Allocator.Persistent);

            this.fieldBoundaryRenderer.SetPositionsFromBounds(this.wrapAroundBounds);
            this.octreeBoundaryRenderer.SetPositionsFromBounds(this.octreeBounds);

            this.AddDiamonds(this.initialDiamonds);
        }

        public void AddDiamonds(int nr)
        {
            var octreeMin = this.octreeBounds.min;
            var octreeMax = this.octreeBounds.max;

            for (int i = 0; i < nr; i++)
            {
                float posX = UnityEngine.Random.Range(octreeMin.x, octreeMax.x);
                float posY = UnityEngine.Random.Range(octreeMin.y, octreeMax.y);
                float posZ = UnityEngine.Random.Range(octreeMin.z, octreeMax.z);

                var diamond = GameObject.Instantiate(this.diamond);

                diamond.transform.parent = this.transform;
                diamond.transform.position = new Vector3(posX, posY, posZ);
                diamond.transform.rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(0.0f, 360.0f), Vector3.up);

                this.diamonds.Add(diamond);

                this.octree.Insert(diamond.transform.position, this.diamonds.Count - 1);
            }
        }

        public void MoveDiamonds(int nr)
        {
            var octreeMin = this.octreeBounds.min;
            var octreeMax = this.octreeBounds.max;

            for (int i = 0; i < nr; i++)
            {
                int diamondIdx = UnityEngine.Random.Range(0, this.diamonds.Count);

                float posX = UnityEngine.Random.Range(octreeMin.x, octreeMax.x);
                float posY = UnityEngine.Random.Range(octreeMin.y, octreeMax.y);
                float posZ = UnityEngine.Random.Range(octreeMin.z, octreeMax.z);

                var oldDiamondPosition = this.diamonds[diamondIdx].transform.position;
                var newDiamondPosition = new Vector3(posX, posY, posZ);
                this.diamonds[diamondIdx].transform.position = newDiamondPosition;

                this.octree.Update(diamondIdx, oldDiamondPosition, newDiamondPosition);
            }
        }

        private void OnDestroy()
        {
            if (this.octree.IsCreated)
            {
                this.octree.Dispose();
            }
        }
    }
}