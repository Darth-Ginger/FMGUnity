

using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;


namespace GimmeDOTSGeometry.Samples
{

    public class PointSystem : MonoBehaviour
    {

        #region Public Variables

        public Color insideColor;
        public Color outsideColor;

        public float yOffset = 0.01f;
        public float pointSize = 0.1f;

        public int initialPointSize;

        public GameObject point;

        public Material polyMaterial;
        public Material rectMaterial;

        public Polygon2DWrapper polygonWrapper;

        public Vector2 maxVelocity;

        #endregion

        #region Private Variables

        private bool changeColor = true;

        private GameObject polygonGO;
        private GameObject rectangleGO;

        private MaterialPropertyBlock insideMPB;
        private MaterialPropertyBlock outsideMPB;

        private int positionCount = 0;

        private List<MeshRenderer> pointRenderers = new List<MeshRenderer>();

        private JobHandle updateMovementHandle = default;

        private NativeList<bool> pointLocationResults;
        private NativeList<float2> positions;
        private NativeList<float2> velocities;

        private Rect bounds;

        private Sampler pointLocationSampler = null;

        private TransformAccessArray pointsAccessArray;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";
        private static readonly ProfilerMarker pointLocationMarker = new ProfilerMarker("PointLocation");


        public Sampler GetPointLocationSampler() => this.pointLocationSampler;

        public int GetNrOfPoints() => this.positionCount;


        void Start()
        {
            this.polygonWrapper.Init();

            var poly = this.polygonWrapper.polygon;

            var simplePoly = NativePolygon2D.MakeSimple(Allocator.TempJob, poly);
            var triangulation = Polygon2DTriangulation.EarClippingTriangulate(simplePoly);

            var mesh = MeshUtil.CreatePolygonMesh(simplePoly, triangulation);

            this.polygonGO = new GameObject("Polygon");
            this.polygonGO.transform.parent = this.transform;

            var meshFilter = this.polygonGO.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            var meshRenderer = this.polygonGO.AddComponent<MeshRenderer>();
            meshRenderer.material = this.polyMaterial;

            var rectMesh = MeshUtil.CreateRectangle(simplePoly.GetBoundingRect()); 

            this.rectangleGO = new GameObject("Rectangle");
            this.rectangleGO.transform.parent = this.transform;
            this.rectangleGO.transform.position = new Vector3(0.0f, -this.yOffset, 0.0f);

            var rectMeshFilter = this.rectangleGO.AddComponent<MeshFilter>();
            rectMeshFilter.mesh = rectMesh;

            var rectMeshRenderer = this.rectangleGO.AddComponent<MeshRenderer>();
            rectMeshRenderer.material = this.rectMaterial;

            simplePoly.Dispose();

            this.bounds = poly.GetBoundingRect();

            this.insideMPB = new MaterialPropertyBlock();
            this.outsideMPB = new MaterialPropertyBlock();

            this.insideMPB.SetColor(SHADER_COLOR, this.insideColor);
            this.outsideMPB.SetColor(SHADER_COLOR, this.outsideColor);

            this.pointsAccessArray = new TransformAccessArray(this.initialPointSize);
            this.positions = new NativeList<float2>(this.initialPointSize, Allocator.Persistent);
            this.velocities = new NativeList<float2>(this.initialPointSize, Allocator.Persistent);
            this.pointLocationResults = new NativeList<bool>(this.initialPointSize, Allocator.Persistent);

            this.AddPoints(this.initialPointSize);

            simplePoly.Dispose();
        }

        public void AddPoints(int nrOfPoints)
        {
            this.updateMovementHandle.Complete();

            this.positionCount += nrOfPoints;
            float pointHalf = this.pointSize / 2.0f;
            for (int i = 0; i < nrOfPoints; i++)
            {
                float rndX = UnityEngine.Random.Range(this.bounds.xMin + pointHalf, this.bounds.xMax - pointHalf);
                float rndY = UnityEngine.Random.Range(this.bounds.yMin + pointHalf, this.bounds.yMax - pointHalf);

                var worldPos = new Vector3(rndX, this.yOffset, rndY);

                var point = GameObject.Instantiate(this.point);
                point.transform.parent = this.transform;
                point.transform.position = worldPos;

                var meshRenderer = point.GetComponentInChildren<MeshRenderer>();

                var pos2D = new Vector2(rndX, rndY);

                this.positions.Add(pos2D);

                if (this.polygonWrapper.polygon.IsPointInside(pos2D))
                {
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                    this.pointLocationResults.Add(true);

                }
                else
                {
                    meshRenderer.SetPropertyBlock(this.outsideMPB);
                    this.pointLocationResults.Add(false);
                }

                this.pointRenderers.Add(meshRenderer);

                float velX = UnityEngine.Random.Range(-this.maxVelocity.x, this.maxVelocity.x);
                float velY = UnityEngine.Random.Range(-this.maxVelocity.y, this.maxVelocity.y);

                this.velocities.Add(new float2(velX, velY));

                this.pointsAccessArray.Add(point.transform);
            }
        }

        [BurstCompile]
        private struct UpdatePointsJob : IJobParallelForTransform
        {
            public Rect bounds;

            public float deltaTime;
            public float pointSize;

            [NoAlias]
            public NativeArray<float2> positions;

            [NoAlias]
            public NativeArray<float2> velocities;

            public void Execute(int index, TransformAccess transform)
            {
                var pos = this.positions[index];

                var velocity = this.velocities[index];

                float nextPosX = math.mad(velocity.x, this.deltaTime, pos.x);
                float nextPosY = math.mad(velocity.y, this.deltaTime, pos.y);

                float xMax = this.bounds.xMax - this.pointSize;
                float xMin = this.bounds.xMin + this.pointSize;
                float yMax = this.bounds.yMax - this.pointSize;
                float yMin = this.bounds.yMin + this.pointSize;

                if (Hint.Unlikely(nextPosX > xMax))
                {
                    velocity = math.reflect(velocity, new float2(-1.0f, 0.0f));
                    nextPosX -= (nextPosX - xMax) * 2.0f;
                }
                else if (Hint.Unlikely(nextPosX < xMin))
                {
                    velocity = math.reflect(velocity, new float2(1.0f, 0.0f));
                    nextPosX += (xMin - nextPosX) * 2.0f;
                }

                if (Hint.Unlikely(nextPosY > yMax))
                {
                    velocity = math.reflect(velocity, new float2(0.0f, -1.0f));
                    nextPosY -= (nextPosY - yMax) * 2.0f;
                }
                else if (Hint.Unlikely(nextPosY < yMin))
                {
                    velocity = math.reflect(velocity, new float2(0.0f, 1.0f));
                    nextPosY += (yMin - nextPosY) * 2.0f;
                }

                this.velocities[index] = velocity;
                this.positions[index] = new float2(nextPosX, nextPosY);

                transform.position = new Vector3(nextPosX, transform.position.y, nextPosY);
            }
        }

        [BurstCompile]
        private struct DetectPointMovement : IJobParallelFor
        {
            [ReadOnly, NoAlias]
            public NativeList<bool> oldLocations;

            [ReadOnly, NoAlias]
            public NativeList<bool> newLocations;

            [WriteOnly]
            public NativeList<int>.ParallelWriter differences;

            public void Execute(int index)
            {
                if (this.oldLocations[index] != this.newLocations[index])
                {
                    if (this.newLocations[index])
                    {
                        this.differences.AddNoResize(++index);
                    }
                    else
                    {
                        this.differences.AddNoResize(-(++index));
                    }
                }
            }
        }

        public void ToggleChangeColor()
        {
            this.changeColor = !this.changeColor;
        }

        public bool IsChangingColor() => this.changeColor;

        void Update()
        {
            this.updateMovementHandle.Complete();

            //"Double Buffering", so we don't have a dependency to the movement job when calculating the point locations
            var positionCopy = new NativeList<float2>(this.positions.Length, Allocator.TempJob);
            positionCopy.CopyFrom(this.positions);

            var updatePointsJob = new UpdatePointsJob()
            {
                deltaTime = Time.deltaTime,
                bounds = this.bounds,
#if COLLECTIONS_2_OR_NEWER
                positions = this.positions.AsArray(),
                velocities = this.velocities.AsArray(),
#else
                positions = this.positions,
                velocities = this.velocities,
#endif

                pointSize = this.pointSize
            };

            this.updateMovementHandle = IJobParallelForTransformExtensions.Schedule(updatePointsJob, this.pointsAccessArray);

            //Copy previous point locations to detect the difference later
            var queryCopy = new NativeList<bool>(this.pointLocationResults.Length, Allocator.TempJob);
            queryCopy.CopyFrom(this.pointLocationResults);

            var pointLocations = this.pointLocationResults.AsArray();

            pointLocationMarker.Begin();
            var arePointsInsideJob = Polygon2DPointLocation.ArePointsInPolygonParallel(this.polygonWrapper.polygon, positionCopy.AsArray(), 
                ref pointLocations);

            arePointsInsideJob.Complete();

            pointLocationMarker.End();

            var differenceList = new NativeList<int>(queryCopy.Length, Allocator.TempJob);

            var detectPointMovementJob = new DetectPointMovement()
            {
                oldLocations = queryCopy,
                newLocations = this.pointLocationResults,
                differences = differenceList.AsParallelWriter()
            };

            detectPointMovementJob.Schedule(queryCopy.Length, 128).Complete();

            if (this.changeColor)
            {
                for (int i = 0; i < differenceList.Length; i++)
                {
                    int idx = differenceList[i];

                    if (idx > 0)
                    {
                        idx--;

                        var meshRenderer = this.pointRenderers[idx];
                        meshRenderer.SetPropertyBlock(this.insideMPB);

                    }
                    else
                    {
                        idx = -idx;
                        idx--;

                        var meshRenderer = this.pointRenderers[idx];
                        meshRenderer.SetPropertyBlock(this.outsideMPB);
                    }
                }
            }

            positionCopy.Dispose();
            queryCopy.Dispose();
            differenceList.Dispose();

            if (this.pointLocationSampler == null || !this.pointLocationSampler.isValid)
            {
                this.pointLocationSampler = Sampler.Get("PointLocation");
            }
        }

        private void Dispose()
        {
            if (this.velocities.IsCreated)
            {
                this.velocities.Dispose();
            }

            if (this.positions.IsCreated)
            {
                this.positions.Dispose();
            }

            if (this.pointsAccessArray.isCreated)
            {
                this.pointsAccessArray.Dispose();
            }

            if (this.pointLocationResults.IsCreated)
            {
                this.pointLocationResults.Dispose();
            }
        }

        private void OnDestroy()
        {
            this.updateMovementHandle.Complete();
            this.Dispose();
        }
    }
}