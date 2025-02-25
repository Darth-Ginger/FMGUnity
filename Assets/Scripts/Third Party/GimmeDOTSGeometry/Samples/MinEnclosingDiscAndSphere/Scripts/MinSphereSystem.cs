using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class MinSphereSystem : MonoBehaviour
    {
        #region Public Variables

        public float attractorStrength = 0.5f;
        public float attractorA = 2.07f;
        public float initialRadius = 5.0f;
        public float drag = 0.1f;
        public float trailsPercentage = 0.15f;
        public float ringThickness = 0.1f;

        public GameObject point;
        public GameObject trailPrefab;

        public int initialNrOfPoints = 16;

        public Material minRingMaterial;
        public Material minSphereMaterial;

        public MinDiscAndSphereCamera cam;

        public Vector3 sprott = Vector3.zero;
        public Vector3 jitter = Vector3.zero;

        #endregion

        #region Private Variables

        private bool trailsRenderersEnabled = true;
        private bool calculateSphere = false;

        private Dictionary<GameObject, TrailRenderer> pointToTrailRenderer = new Dictionary<GameObject, TrailRenderer>();

        private GameObject minRing = null;
        private GameObject minSphere = null;

        private int nrOfPoints = 0;

        private List<GameObject> points = new List<GameObject>();

        private NativeList<float3> velocities;
        private NativeReference<float3> center3D;
        private NativeReference<float2> center2D;
        private NativeReference<float> radius;

        private Sampler minDiscSampler = null;

        private TransformAccessArray pointsAccessArray;

        #endregion


        private static readonly ProfilerMarker minDiskMarker = new ProfilerMarker("MinDisc");

        public bool IsCalculatingSphere() => this.calculateSphere;


        public int GetNrOfPoints() => this.nrOfPoints;


        public Sampler GetMinDiscSampler() => this.minDiscSampler;

        public Vector3 GetCurrentCenter()
        {
            if (this.calculateSphere)
            {
                return this.center3D.Value;
            }
            else
            {
                return new Vector3(this.center2D.Value.x, 0.0f, this.center2D.Value.y);
            }
        }

        public float GetCurrentRadius() => this.radius.Value;

        public void AddRandomPoints(int nrOfPoints)
        {

            for(int i = 0; i < nrOfPoints; i++)
            {
                Vector3 rndPos = UnityEngine.Random.insideUnitSphere * this.initialRadius;

                var point = GameObject.Instantiate(this.point);
                point.transform.parent = this.transform;
                point.transform.position = rndPos;


                if (UnityEngine.Random.value < this.trailsPercentage)
                {
                    var trail = GameObject.Instantiate(this.trailPrefab);
                    trail.transform.parent = point.transform;
                    trail.transform.localPosition = Vector3.zero;

                    var trailRenderer = trail.GetComponent<TrailRenderer>();
                    trailRenderer.enabled = this.trailsRenderersEnabled;
                    this.pointToTrailRenderer.Add(point, trailRenderer);
                }

                this.velocities.Add(UnityEngine.Random.insideUnitSphere);

                this.pointsAccessArray.Add(point.transform);

                this.points.Add(point);
            }
            this.nrOfPoints += nrOfPoints;
        }

        public void RemoveRandomPoints(int nrOfPoints)
        {
            for(int i = 0; i < nrOfPoints; i++)
            {
                if (this.nrOfPoints <= 0) break;

                var rndPoint = UnityEngine.Random.Range(0, this.nrOfPoints);

                this.pointsAccessArray.RemoveAtSwapBack(rndPoint);
                this.velocities.RemoveAtSwapBack(rndPoint);

                var point = this.points[rndPoint];
                if (this.pointToTrailRenderer.ContainsKey(point))
                {
                    var renderer = this.pointToTrailRenderer[point];
                    GameObject.Destroy(renderer);
                    this.pointToTrailRenderer.Remove(point);
                }

                GameObject.Destroy(point);
                this.points.RemoveAtSwapBack(rndPoint);

                this.nrOfPoints--;
            }
        }

        public void SetSphereMode(bool enable)
        {
            this.calculateSphere = !this.calculateSphere;
        }

        public void ToggleTrailRenderers()
        {
            this.trailsRenderersEnabled = !this.trailsRenderersEnabled;
            foreach(var entry in this.pointToTrailRenderer)
            {
                var renderer = entry.Value;
                renderer.enabled = this.trailsRenderersEnabled;
            }
        }



        private void Start()
        {
            this.pointsAccessArray = new TransformAccessArray(this.initialNrOfPoints);
            this.velocities = new NativeList<float3>(this.initialNrOfPoints, Allocator.Persistent);
            this.center2D = new NativeReference<float2>(Allocator.Persistent);
            this.center3D = new NativeReference<float3>(Allocator.Persistent);
            this.radius = new NativeReference<float>(Allocator.Persistent);

            this.AddRandomPoints(this.initialNrOfPoints);

            this.minRing = new GameObject("Minimum Ring");
            this.minRing.transform.parent = this.transform;
            this.minRing.transform.localPosition = Vector3.zero;

            var mr = this.minRing.AddComponent<MeshRenderer>();
            var mf = this.minRing.AddComponent<MeshFilter>();

            var ringMesh = MeshUtil.CreateRing(1.0f, this.ringThickness, 64);

            mf.sharedMesh = ringMesh;
            mr.sharedMaterial = this.minRingMaterial;

            this.minSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            this.minSphere.transform.parent = this.transform;

            mr = this.minSphere.GetComponent<MeshRenderer>();
            mr.material = this.minSphereMaterial;

            this.minSphere.SetActive(false);
        }

        [BurstCompile]
        private struct UpdatePointsJob : IJobParallelForTransform
        {

            public float attractorStrength;
            public float attractorA;
            public float deltaTime;
            public float drag;

            public float3 sprott;
            public float3 jitter;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float3> velocities;

            public void Execute(int index, TransformAccess transform)
            {
                var position = (float3)transform.position;
                var velocity = this.velocities[index];

                //Halvorsen + Sprott -> https://www.dynamicmath.xyz/strange-attractors/
                float3 nextAttrPos = float3.zero;
                nextAttrPos.x = -this.attractorA * position.x - 4.0f * position.y - 4.0f * position.z - position.y * position.y 
                    + this.sprott.x * math.sin(position.y * this.jitter.x) - this.sprott.y * math.cos(position.z * this.jitter.y) - this.sprott.z * math.sin(position.x * this.jitter.z);
                nextAttrPos.y = -this.attractorA * position.y - 4.0f * position.z - 4.0f * position.x - position.z * position.z 
                    + this.sprott.x * math.sin(position.z * this.jitter.x) - this.sprott.y * math.cos(position.x * this.jitter.y) - this.sprott.z * math.sin(position.y * this.jitter.z);
                nextAttrPos.z = -this.attractorA * position.z - 4.0f * position.x - 4.0f * position.y - position.x * position.x 
                    + this.sprott.x * math.sin(position.x * this.jitter.x) - this.sprott.y * math.cos(position.y * this.jitter.y) - this.sprott.z * math.sin(position.z * this.jitter.z);


                velocity = (velocity * (1 - this.attractorStrength)) + (nextAttrPos - position) * this.attractorStrength;
                velocity *= (1.0f - this.drag);

                position += velocity * this.deltaTime;
                transform.position = position;


                this.velocities[index] = velocity;



            }
        }

        private void Update()
        {
            var updatePointsJob = new UpdatePointsJob()
            {
                attractorA = this.attractorA,
                attractorStrength = this.attractorStrength,
                deltaTime = Time.deltaTime,
                velocities = this.velocities,
                drag = this.drag,
                sprott = this.sprott,
                jitter = this.jitter
            };

            updatePointsJob.Schedule(this.pointsAccessArray).Complete();


            if(this.calculateSphere)
            {
                this.minSphere.SetActive(true);
                var points3D = new NativeArray<float3>(this.pointsAccessArray.length, Allocator.TempJob);
                for (int i = 0; i < this.pointsAccessArray.length; i++)
                {
                    var pointTransform = this.pointsAccessArray[i];
                    points3D[i] = pointTransform.position;
                }

                minDiskMarker.Begin();

                var minSphereJob = HullAlgorithms.FindMinimumEnclosingSphere(points3D, this.center3D, this.radius);
                minSphereJob.Complete();

                minDiskMarker.End();

                points3D.Dispose();

                this.minSphere.transform.position = this.center3D.Value;
                this.minSphere.transform.localScale = Vector3.one * this.radius.Value * 2.0f;

                this.minRing.transform.position = this.center3D.Value;
                this.minRing.transform.up = -this.cam.transform.forward;
                this.minRingMaterial.SetVector("_RingCenter", new Vector4(this.center3D.Value.x, this.center3D.Value.y, this.center3D.Value.z, 0.0f));

            }
            else
            {
                this.minSphere.SetActive(false);
                var points2D = new NativeArray<float2>(this.pointsAccessArray.length, Allocator.TempJob);
                for (int i = 0; i < this.pointsAccessArray.length; i++)
                {
                    var pointTransform = this.pointsAccessArray[i];
                    float2 pos;
                    pos.x = pointTransform.position.x;
                    pos.y = pointTransform.position.z;
                    points2D[i] = pos;
                }

                minDiskMarker.Begin();

                var minDiscJob = HullAlgorithms.FindMinimumEnclosingDisc(points2D, this.center2D, this.radius);
                minDiscJob.Complete();

                minDiskMarker.End();

                points2D.Dispose();

                Vector3 flatPos = new Vector3(this.center2D.Value.x, 0.0f, this.center2D.Value.y);
                this.minRing.transform.position = flatPos;
                this.minRing.transform.up = Vector3.up;
                this.minRingMaterial.SetVector("_RingCenter", new Vector4(flatPos.x, flatPos.y, flatPos.z, 0.0f));

            }

            this.minRingMaterial.SetFloat("_RingRadius", this.radius.Value);

            this.minRing.transform.localScale = Vector3.one * this.radius.Value;

            if (this.minDiscSampler == null || !this.minDiscSampler.isValid)
            {
                this.minDiscSampler = Sampler.Get("MinDisc");
            }
        }

        private void Dispose()
        {
            if (this.pointsAccessArray.isCreated)
            {
                this.pointsAccessArray.Dispose();
            }

            if(this.center2D.IsCreated)
            {
                this.center2D.Dispose();
            }

            if(this.center3D.IsCreated)
            {
                this.center3D.Dispose();
            }

            if(this.radius.IsCreated)
            {
                this.radius.Dispose();
            }

            this.velocities.DisposeIfCreated();
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
