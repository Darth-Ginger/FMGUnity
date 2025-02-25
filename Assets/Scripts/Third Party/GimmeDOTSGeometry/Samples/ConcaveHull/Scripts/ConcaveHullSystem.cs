using System.Collections.Generic;
using System.IO;
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
    public class ConcaveHullSystem : MonoBehaviour
    {

        public enum Mode
        {
            CONVEX = 0,
            CONCAVE = 1,
        }

        public enum DataSet
        {

            ROTATING = 0,
            DOLPHIN = 1,
            TREE = 2,
        }


        #region Public Fields

        public DataSet dataSet = DataSet.DOLPHIN;

        public float concavity = 2.0f;
        public float radius;
        public float trailingChance;

        public float minAngularVelocity;
        public float maxAngularVelocity;

        public GameObject regularPoint;
        public GameObject trailingPoint;
        public GameObject clearPoint;
        public GameObject hull;

        public int initialPointSize;

        public Mode mode = Mode.CONCAVE;

        public string dolphinPath;
        public string treePath;

        public Vector2 scaleVariance;
        public Vector2 radialSpeedVariance;
        public Vector2 radialOffsetVariance;

        #endregion

        #region Private Fields

        private List<GameObject> points = new List<GameObject>();

        private List<GameObject> dolphinPointGOs = new List<GameObject>();
        private List<GameObject> treePointGOs = new List<GameObject>();

        private LineRenderer hullRenderer = null;

        private NativeList<float> radialVelocities;
        private NativeList<float> angularVelocities;
        private NativeList<float> radialOffsets;
        private NativeList<float> radialOffsetVariances;
        private NativeList<float2> positions;
        private NativeList<float2> polarCoordinates;

        private NativeList<float3> dolphinPoints;
        private NativeList<float3> treePoints;

        private JobHandle updateMovementHandle = default;

        private TransformAccessArray pointsAccessArray;

        private static readonly ProfilerMarker concaveHullMarker = new ProfilerMarker("ConcaveHull");

        private Sampler concaveHullSampler = null;

        #endregion

        private struct PolarCoordinateComparer : IComparer<float2>
        {
            public int Compare(float2 a, float2 b)
            {
                return a.x.CompareTo(b.x);
            }
        }

        public int GetNrOfPoints()
        {
            int pointsLength = 0;
            if (this.dataSet == DataSet.ROTATING)
            {
                this.updateMovementHandle.Complete();
                pointsLength = this.positions.Length;
            }
            else if (this.dataSet == DataSet.DOLPHIN)
            {
                pointsLength = this.dolphinPoints.Length;
            }
            else
            {
                pointsLength = this.treePoints.Length;
            }
            return pointsLength;
        }

        public Sampler GetConcaveHullSampler() => this.concaveHullSampler;

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

        public void SetMode(Mode mode)
        {
            this.mode = mode;
        }

        public void DisableAllPoints()
        {
            for(int i = 0; i < this.points.Count; i++)
            {
                this.points[i].SetActive(false);
            }

            for(int i = 0; i < this.dolphinPointGOs.Count; i++)
            {
                this.dolphinPointGOs[i].SetActive(false);
            }

            for(int i = 0; i < this.treePointGOs.Count; i++)
            {
                this.treePointGOs[i].SetActive(false);
            }
        }

        public void SetDataSet(DataSet dataSet)
        {
            this.dataSet = dataSet;
            this.DisableAllPoints();
            switch(dataSet)
            {
                case DataSet.DOLPHIN:
                    for (int i = 0; i < this.dolphinPointGOs.Count; i++) this.dolphinPointGOs[i].SetActive(true);
                    break;
                case DataSet.TREE:
                    for (int i = 0; i < this.treePointGOs.Count; i++) this.treePointGOs[i].SetActive(true);
                    break;
                case DataSet.ROTATING:
                    for (int i = 0; i < this.points.Count; i++) this.points[i].SetActive(true);
                    break;
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

        private void CreateGameObjectsForObj(NativeList<float3> positions, List<GameObject> pointList)
        {
            for(int i = 0; i < positions.Length; i++)
            {
                var point = GameObject.Instantiate(this.clearPoint);
                point.transform.parent = this.transform;
                point.transform.position = positions[i];
                pointList.Add(point);
            }
        }

        private void Awake()
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

            if (!string.IsNullOrEmpty(this.dolphinPath))
            {
                var path = Application.dataPath + this.dolphinPath;
                if (File.Exists(path))
                {
                    this.dolphinPoints = ObjUtil.ImportObjAsPointCloud(path);
                    this.CreateGameObjectsForObj(this.dolphinPoints, this.dolphinPointGOs);
                }
            }

            if(!string.IsNullOrEmpty(this.treePath))
            {
                var path = Application.dataPath + this.treePath;
                if(File.Exists(path))
                {
                    this.treePoints = ObjUtil.ImportObjAsPointCloud(path);
                    this.CreateGameObjectsForObj(this.treePoints, this.treePointGOs);
                }
            }

            this.SetDataSet(this.dataSet);
        }

        private void Update()
        {
            this.updateMovementHandle.Complete();
            
            //"Double Buffering", so we don't have a dependency to the rotation job when calculating the convex hull
            var positionCopy = new NativeList<float2>(this.GetNrOfPoints(), Allocator.TempJob);
            positionCopy.Resize(this.GetNrOfPoints(), NativeArrayOptions.UninitializedMemory);

            if (this.dataSet == DataSet.ROTATING)
            {
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
            } else if(this.dataSet == DataSet.DOLPHIN)
            {
                for(int i = 0; i < this.dolphinPoints.Length; i++)
                {
                    positionCopy[i] = new float2(this.dolphinPoints[i].x, this.dolphinPoints[i].z);
                }
            } else
            {
                for (int i = 0; i < this.treePoints.Length; i++)
                {
                    positionCopy[i] = new float2(this.treePoints[i].x, this.treePoints[i].z);
                }
            }

            var polygon = new NativePolygon2D(Allocator.TempJob, 0);

            if (this.mode == Mode.CONVEX)
            {
                concaveHullMarker.Begin();
                var convexHullJob = HullAlgorithms.CreateConvexHull(positionCopy.AsArray(),
                    ref polygon, true);

                convexHullJob.Complete();
                concaveHullMarker.End();
            }
            else
            {
                concaveHullMarker.Begin();
                var concaveHullJob = HullAlgorithms.CreateConcaveHull(positionCopy.AsArray(),
                    ref polygon, this.concavity);

                concaveHullJob.Complete();
                concaveHullMarker.End();
            }

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

            if (this.concaveHullSampler == null || !this.concaveHullSampler.isValid)
            {
                this.concaveHullSampler = Sampler.Get("ConcaveHull");
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

            this.dolphinPoints.DisposeIfCreated();
            this.treePoints.DisposeIfCreated();

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
