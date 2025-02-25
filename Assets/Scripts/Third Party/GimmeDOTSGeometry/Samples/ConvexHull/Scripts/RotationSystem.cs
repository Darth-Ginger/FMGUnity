
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;


namespace GimmeDOTSGeometry.Samples
{

    public class RotationSystem : MonoBehaviour
    {

        #region Public Variables

        public float radius;
        public float trailingChance;

        public float minAngularVelocity;
        public float maxAngularVelocity;

        public GameObject regularPoint;
        public GameObject trailingPoint;
        public GameObject hull;

        public int initialPointSize;

        public Vector2 scaleVariance;
        public Vector2 radialSpeedVariance;
        public Vector2 radialOffsetVariance;

        #endregion

        #region Private Variables

        private List<GameObject> points = new List<GameObject>();

        private LineRenderer hullRenderer = null;

        private NativeList<float> radialVelocities;
        private NativeList<float> angularVelocities;
        private NativeList<float> radialOffsets;
        private NativeList<float> radialOffsetVariances;
        private NativeList<float2> positions;
        private NativeList<float2> polarCoordinates;

        private JobHandle updateMovementHandle = default;

        private static readonly ProfilerMarker convexHullMarker = new ProfilerMarker("ConvexHull");

        private Sampler convexHullSampler = null;

        private TransformAccessArray pointsAccessArray;

        #endregion

        public int GetNrOfPoints() => this.points.Count;

        public Sampler GetConvexHullSampler() => this.convexHullSampler;

        private struct PolarCoordinateComparer : IComparer<float2>
        {
            public int Compare(float2 a, float2 b)
            {
                return a.x.CompareTo(b.x);
            }
        }

        [BurstCompile]
        private struct UpdatePointsJob : IJobParallelForTransform
        {
            public float deltaTime;

            public Vector3 center;


            [NoAlias, ReadOnly]
            public NativeList<float> angularVelocities;

            [NoAlias, ReadOnly]
            public NativeList<float> radialVelocities;

            [NoAlias, ReadOnly]
            public NativeList<float> radialOffsetVariances;


            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float> radialOffsets;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float2> polarCoordinates;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float2> positions;

            public void Execute(int index, TransformAccess transform)
            {
                var polarCoord = this.polarCoordinates[index];
                polarCoord.y += this.angularVelocities[index] * this.deltaTime;
                this.polarCoordinates[index] = polarCoord;

                var offset = this.radialOffsets[index];
                offset += this.deltaTime * this.radialVelocities[index];
                this.radialOffsets[index] = offset;

                float radialOffset = math.sin(offset) * this.radialOffsetVariances[index];

                math.sincos(polarCoord.y, out float sin, out float cos);

                float2 pos2D = new float2(this.center.x + sin * (polarCoord.x + radialOffset), this.center.z + cos * (polarCoord.x + radialOffset));

                transform.position = new Vector3(pos2D.x, 0.0f /*math.sin(offset + Mathf.PI * 0.25f) * this.radialOffsetVariances[index]*/, pos2D.y);
                this.positions[index] = pos2D;

                var eulerAngles = transform.rotation.eulerAngles;
                eulerAngles.y = polarCoord.y * Mathf.Rad2Deg;
                var rot = transform.rotation;
                rot.eulerAngles = eulerAngles;
                transform.rotation = rot;
            }
        }

        public void AddPoints(int nrOfPoints)
        {
            this.updateMovementHandle.Complete();

            for (int i = 0; i < nrOfPoints; i++)
            {
                float rnd = UnityEngine.Random.value;
                GameObject point = null;
                if (rnd < trailingChance)
                {
                    point = GameObject.Instantiate(this.trailingPoint);
                }
                else
                {
                    point = GameObject.Instantiate(this.regularPoint);
                }

                float angle = UnityEngine.Random.value * Constants.TAU;
                float r = Mathf.Pow(UnityEngine.Random.value, 1.0f) * this.radius;

                float offset = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
                float radialVariance = UnityEngine.Random.Range(this.radialOffsetVariance.x, this.radialOffsetVariance.y);
                float radialOffset = Mathf.Sin(offset) * radialVariance;

                float radialVelocity = UnityEngine.Random.Range(this.radialSpeedVariance.x, this.radialSpeedVariance.y);

                this.radialOffsets.Add(offset);
                this.radialVelocities.Add(radialVelocity);
                this.radialOffsetVariances.Add(radialVariance);

                float2 pos2D = new float2(Mathf.Cos(angle) * (r + radialOffset), Mathf.Sin(angle) * (r + radialOffset));
                Vector3 worldPos = new Vector3(pos2D.x, 0.0f, pos2D.y);
                point.transform.position = worldPos;
                point.transform.parent = this.transform;

                this.positions.Add(pos2D);

                var eulerAngles = point.transform.eulerAngles;
                eulerAngles.y = -angle * Mathf.Rad2Deg;
                point.transform.eulerAngles = eulerAngles;
                point.transform.localScale = Vector3.one * UnityEngine.Random.Range(this.scaleVariance.x, this.scaleVariance.y) * (Mathf.Sqrt((r + 0.1f) / (this.radius + 0.1f)));

                this.pointsAccessArray.Add(point.transform);

                float angularVelocity = UnityEngine.Random.Range(this.minAngularVelocity, this.maxAngularVelocity);
                this.angularVelocities.Add(angularVelocity);
                this.polarCoordinates.Add(new float2(r, angle));

                this.points.Add(point);
            }
        }

        public void Awake()
        {

            this.pointsAccessArray = new TransformAccessArray(this.initialPointSize);
            this.angularVelocities = new NativeList<float>(Allocator.Persistent);
            this.polarCoordinates = new NativeList<float2>(Allocator.Persistent);
            this.positions = new NativeList<float2>(Allocator.Persistent);
            this.radialOffsets = new NativeList<float>(Allocator.Persistent);
            this.radialOffsetVariances = new NativeList<float>(Allocator.Persistent);
            this.radialVelocities = new NativeList<float>(Allocator.Persistent);

            this.AddPoints(this.initialPointSize);

            var hullObj = GameObject.Instantiate(this.hull);
            hullObj.transform.parent = this.transform;

            this.hullRenderer = hullObj.GetComponentInChildren<LineRenderer>();


        }


        private void Update()
        {
            this.updateMovementHandle.Complete();

            //"Double Buffering", so we don't have a dependency to the rotation job when calculating the convex hull
            var positionCopy = new NativeList<float2>(this.positions.Length, Allocator.TempJob);
            positionCopy.CopyFrom(this.positions);

            var updatePointsJob = new UpdatePointsJob()
            {
                angularVelocities = this.angularVelocities,
                center = this.transform.position,
                deltaTime = Time.deltaTime,
                polarCoordinates = this.polarCoordinates,
                radialOffsets = this.radialOffsets,
                radialVelocities = this.radialVelocities,
                radialOffsetVariances = this.radialOffsetVariances,
                positions = this.positions
            };

            this.updateMovementHandle = IJobParallelForTransformExtensions.Schedule(updatePointsJob, this.pointsAccessArray);

            var polygon = new NativePolygon2D(Allocator.TempJob, 0);

            convexHullMarker.Begin();
            var convexHullJob = HullAlgorithms.CreateConvexHull(positionCopy.AsArray(),
                ref polygon, true);

            convexHullJob.Complete();
            convexHullMarker.End();

            var lineRenderer = this.hullRenderer;
            Vector3[] linePositions = new Vector3[polygon.points.Length + 1];
            for (int j = 0; j < polygon.points.Length; j++)
            {
                var polyPoint = polygon.points.ElementAt(j);
                linePositions[j] = new Vector3(polyPoint.x, 0.0f, polyPoint.y);
            }

            linePositions[polygon.points.Length] = linePositions[0];
            lineRenderer.positionCount = polygon.points.Length + 1;
            lineRenderer.SetPositions(linePositions);

            positionCopy.Dispose();
            polygon.Dispose();

            if (this.convexHullSampler == null || !this.convexHullSampler.isValid)
            {
                this.convexHullSampler = Sampler.Get("ConvexHull");
            }
        }

        private void Dispose()
        {
            this.angularVelocities.DisposeIfCreated();
            this.polarCoordinates.DisposeIfCreated();
            this.radialOffsets.DisposeIfCreated();
            this.radialVelocities.DisposeIfCreated();
            this.radialOffsetVariances.DisposeIfCreated();
            this.positions.DisposeIfCreated();

            if (this.pointsAccessArray.isCreated)
            {
                this.pointsAccessArray.Dispose();
            }
        }

        private void OnDestroy()
        {
            this.updateMovementHandle.Complete();
            this.Dispose();
        }

    }
}