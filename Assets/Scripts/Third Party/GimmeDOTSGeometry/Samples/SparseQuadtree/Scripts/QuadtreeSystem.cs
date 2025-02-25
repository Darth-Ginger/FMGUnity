
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class QuadtreeSystem : MonoBehaviour
    {

        #region Public Variables

        public float searchRadius;

        public GameObject lightningRod;

        public int initialRods;

        public LineRenderer fieldBoundaryRenderer;
        public LineRenderer quadtreeBoundaryRenderer;

        public Material floorMaterial;

        public Rect quadtreeRect;
        public Rect boundaryRect;

        #endregion

        #region Private Variables

        private List<GameObject> rods = new List<GameObject>();

        private NativeSparseQuadtree<int> quadtree;


        #endregion

        public NativeSparseQuadtree<int> Quadtree => this.quadtree;

        public List<GameObject> Rods => this.rods;


        void Start()
        {
            var center = this.quadtreeRect.center;
            this.quadtree = new NativeSparseQuadtree<int>(new float3(center.x, 0.0f, center.y), this.quadtreeRect.size, 6, 1024, Unity.Collections.Allocator.Persistent);

            this.fieldBoundaryRenderer.positionCount = 5;
            this.quadtreeBoundaryRenderer.positionCount = 5;

            var boundaryMin = this.boundaryRect.min;
            var boundaryMax = this.boundaryRect.max;

            var quadtreeMin = this.quadtreeRect.min;
            var quadtreeMax = this.quadtreeRect.max;

            this.fieldBoundaryRenderer.SetPositions(new Vector3[]
            {
                new Vector3(boundaryMin.x, 0.0f, boundaryMin.y),
                new Vector3(boundaryMax.x, 0.0f, boundaryMin.y),
                new Vector3(boundaryMax.x, 0.0f, boundaryMax.y),
                new Vector3(boundaryMin.x, 0.0f, boundaryMax.y),
                new Vector3(boundaryMin.x, 0.0f, boundaryMin.y)
            });

            this.quadtreeBoundaryRenderer.SetPositions(new Vector3[]
            {
                new Vector3(quadtreeMin.x, 0.0f, quadtreeMin.y),
                new Vector3(quadtreeMax.x, 0.0f, quadtreeMin.y),
                new Vector3(quadtreeMax.x, 0.0f, quadtreeMax.y),
                new Vector3(quadtreeMin.x, 0.0f, quadtreeMax.y),
                new Vector3(quadtreeMin.x, 0.0f, quadtreeMin.y)
            });

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.parent = this.transform;
            cube.transform.localScale = new Vector3(this.quadtreeRect.width, 1.0f, this.quadtreeRect.height);
            cube.transform.position = new Vector3(center.x, -0.5f, center.y);

            var cubeRenderer = cube.GetComponent<MeshRenderer>();
            cubeRenderer.sharedMaterial = this.floorMaterial;
            this.floorMaterial.SetTextureScale("_MainTex", new Vector2(this.quadtreeRect.width, this.quadtreeRect.height));

            this.AddLightningRods(this.initialRods);


        }

        public void AddLightningRods(int nr)
        {
            var quadtreeMin = this.quadtreeRect.min;
            var quadtreeMax = this.quadtreeRect.max;

            for (int i = 0; i < nr; i++)
            {
                float posX = UnityEngine.Random.Range(quadtreeMin.x, quadtreeMax.x);
                float posZ = UnityEngine.Random.Range(quadtreeMin.y, quadtreeMax.y);
                float posY = 0.0f;

                var rod = GameObject.Instantiate(this.lightningRod);

                rod.transform.parent = this.transform;
                rod.transform.position = new Vector3(posX, posY, posZ);

                this.rods.Add(rod);

                this.quadtree.Insert(rod.transform.position, this.rods.Count - 1);
            }
        }

        public void MoveRandomLightningRods(int nr)
        {
            var quadtreeMin = this.quadtreeRect.min;
            var quadtreeMax = this.quadtreeRect.max;

            for(int i = 0; i < nr; i++)
            {
                int rodIdx = UnityEngine.Random.Range(0, this.rods.Count);

                float posX = UnityEngine.Random.Range(quadtreeMin.x, quadtreeMax.x);
                float posZ = UnityEngine.Random.Range(quadtreeMin.y, quadtreeMax.y);
                float posY = 0.0f;

                var oldRodPosition = this.rods[rodIdx].transform.position;
                var newRodPosition = new Vector3(posX, posY, posZ);
                this.rods[rodIdx].transform.position = newRodPosition;

                this.quadtree.Update(rodIdx, oldRodPosition, newRodPosition);
            }
        }


        private void OnDestroy()
        {
            if (this.quadtree.IsCreated)
            {
                this.quadtree.Dispose();
            }
        }


    }
}