
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class QuadtreeWalker : MonoBehaviour
    {
        #region Public Variables

        public float movementSpeed;
        public float lightningRodYOffset;

        public int circlePoints;

        public LineRenderer searchRadiusRenderer;

        public ParticleSystem electricityFX;

        public QuadtreeCamera quadtreeCamera;
        public QuadtreeSystem quadtreeSystem;

        #endregion

        #region Private Variables

        private NativeList<uint> searchResults;

        private static readonly ProfilerMarker quadtreeMarker = new ProfilerMarker("QuadtreeSearch");

        private Sampler quadtreeSampler = null;


        #endregion

        public Sampler GetQuadtreeSampler() => this.quadtreeSampler;

        private void UpdateSearchRadiusLineRenderer()
        {
            float incPerAngle = Constants.TAU / (float)this.circlePoints;
            float currentAngle = 0.0f;

            Vector3[] positions = new Vector3[this.circlePoints];

            for (int i = 0; i < this.circlePoints; i++)
            {
                float x = Mathf.Cos(currentAngle) * this.quadtreeSystem.searchRadius;
                float y = Mathf.Sin(currentAngle) * this.quadtreeSystem.searchRadius;

                positions[i] = this.transform.position + new Vector3(x, 0.15f, y);

                currentAngle += incPerAngle;
            }

            this.searchRadiusRenderer.SetPositions(positions);
        }

        private void Start()
        {
            this.searchRadiusRenderer.positionCount = this.circlePoints;
            this.searchResults = new NativeList<uint>(Allocator.Persistent);

            this.UpdateSearchRadiusLineRenderer();
        }

        private void OnDestroy()
        {
            if (this.searchResults.IsCreated)
            {
                this.searchResults.Dispose();
            }
        }

        private void Update()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            var forward = Vector3.ProjectOnPlane(this.quadtreeCamera.transform.forward, Vector3.up).normalized;
            var perp = new Vector3(forward.z, 0.0f, -forward.x).normalized;
            this.transform.position += (forward * vertical + horizontal * perp) * this.movementSpeed * Time.deltaTime;

            var boundary = this.quadtreeSystem.boundaryRect;
            var boundsMin = boundary.min;
            var boundsMax = boundary.max;

            Vector3 clampedPosition = this.transform.position;

            if (clampedPosition.x < boundsMin.x) clampedPosition.x = boundsMin.x;
            if (clampedPosition.z < boundsMin.y) clampedPosition.z = boundsMin.y;
            if (clampedPosition.x > boundsMax.x) clampedPosition.x = boundsMax.x;
            if (clampedPosition.z > boundsMax.y) clampedPosition.z = boundsMax.y;

            this.transform.position = clampedPosition;

            this.UpdateSearchRadiusLineRenderer();

            var quadtree = this.quadtreeSystem.Quadtree;

            this.searchResults.Clear();
            quadtreeMarker.Begin();
            var job = quadtree.GetCellsInRadius(new float2(this.transform.position.x, this.transform.position.z),
                this.quadtreeSystem.searchRadius,
                ref this.searchResults);
            job.Complete();
            quadtreeMarker.End();

            var rods = this.quadtreeSystem.Rods;
            var data = quadtree.GetDataBuckets();

            var emitParams = new ParticleSystem.EmitParams()
            {
                position = this.transform.position,
            };
            var psLifettime = this.electricityFX.main.startLifetime.constant;

            for (int i = 0; i < this.searchResults.Length; i++)
            {
                uint cell = this.searchResults[i];
                if (data.TryGetFirstValue(cell, out int rod, out var it))
                {
                    var dir = (rods[rod].transform.position + Vector3.up * this.lightningRodYOffset - this.transform.position);
                    emitParams.velocity = dir / psLifettime;
                    this.electricityFX.Emit(emitParams, 1);

                    while (data.TryGetNextValue(out rod, ref it))
                    {
                        dir = (rods[rod].transform.position + Vector3.up * this.lightningRodYOffset - this.transform.position);
                        emitParams.velocity = dir / psLifettime;
                        this.electricityFX.Emit(emitParams, 1);
                    }
                }


            }

            if (this.quadtreeSampler == null || !this.quadtreeSampler.isValid)
            {
                this.quadtreeSampler = Sampler.Get("QuadtreeSearch");
            }
        }
    }
}