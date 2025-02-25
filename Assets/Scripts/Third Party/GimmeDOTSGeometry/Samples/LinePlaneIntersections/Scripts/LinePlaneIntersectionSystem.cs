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
    public class LinePlaneIntersectionSystem : MonoBehaviour
    {

        #region Public Fields

        public Bounds bounds;

        public float initialPlaneDist;
        public float pointsRadius;

        public int initialSegments;

        public Material planeMaterial;
        public Material boundsMaterial;

        public Material pointsMaterial;
        public Material intersectionMaterial;
        public Material linesMaterial;

        public Vector2 velocityRange;
        public Vector3 initialPlaneNormal;


        #endregion

        #region Private Fields

        private GameObject planeObject;

        private GraphicsBuffer pointPositionBuffer;
        private GraphicsBuffer intersectionPositionBuffer;

        private int numberOfSegments;

        private Mesh pointMesh;
        private MeshFilter planeFilter;
        private MeshRenderer planeRenderer;

        private NativeList<float3> intersections;

        private NativeList<float3> positions;
        private NativeList<float3> velocities;

        private NativeList<LineSegment3D> segments;

        private Plane currentPlane;

        private RenderParams pointsParams;
        private RenderParams intersectionParams;
        private RenderParams linesParams;

        private Sampler linePlaneIntersectionSampler = null;

        #endregion

        private static readonly ProfilerMarker linePlaneIntersectionMarker = new ProfilerMarker("LinePlaneIntersection");

        public Sampler GetLinePlaneIntersectionSampler() => this.linePlaneIntersectionSampler;

        public int NrOfSegments => this.numberOfSegments;

        public int NrOfIntersections => this.intersections.Length;

        public void SetPlane(Vector3 normal, float dist)
        {
            this.currentPlane = new Plane(normal, dist);
        }

        public Plane GetPlane() => this.currentPlane;


        private void Start()
        {
            this.currentPlane = new Plane(this.initialPlaneNormal, this.initialPlaneDist);

            var boundsGo = new GameObject("Bounds");
            var mr = boundsGo.AddComponent<MeshRenderer>();
            var mf = boundsGo.AddComponent<MeshFilter>();

            mr.material = this.boundsMaterial;

            var expandedBounds = this.bounds;
            expandedBounds.Expand(0.1f);

            mf.mesh = MeshUtil.CreateBoxOutline(expandedBounds, 0.1f);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var sphereFilter = sphere.GetComponentInChildren<MeshFilter>();

            var mesh = sphereFilter.mesh;

            var vertices = mesh.vertices;

            for(int i = 0; i < vertices.Length; i++)
            {
                vertices[i] *= this.pointsRadius;
            }

            mesh.SetVertices(vertices);
            this.pointMesh = mesh;

            GameObject.Destroy(sphere);

            this.pointsParams = new RenderParams()
            {
                material = this.pointsMaterial,
                worldBounds = expandedBounds,
            };

            this.linesParams = new RenderParams()
            {
                material = this.linesMaterial,
                worldBounds = expandedBounds,
            };

            this.intersectionParams = new RenderParams()
            {
                material = this.intersectionMaterial,
                worldBounds = expandedBounds,
            };

            this.velocities = new NativeList<float3>(this.initialSegments * 2, Allocator.Persistent);
            this.positions = new NativeList<float3>(this.initialSegments * 2, Allocator.Persistent);
            this.segments = new NativeList<LineSegment3D>(this.initialSegments, Allocator.Persistent);

            this.intersections = new NativeList<float3>(this.initialSegments, Allocator.Persistent);

            this.AddSegments(this.initialSegments);

            this.planeObject = new GameObject("Plane");
            this.planeObject.transform.parent = this.transform.parent;
            this.planeObject.transform.position = Vector3.zero;

            this.planeFilter = this.planeObject.AddComponent<MeshFilter>();
            this.planeRenderer = this.planeObject.AddComponent<MeshRenderer>();

            this.planeRenderer.material = this.planeMaterial;
        }

        public unsafe void AddSegments(int nrOfSegments)
        {
            if (this.pointPositionBuffer != null) this.pointPositionBuffer.Dispose();

            this.velocities.Clear();
            this.positions.Clear();
            this.segments.Clear();

            this.numberOfSegments += nrOfSegments;

            var min = this.bounds.min;
            var max = this.bounds.max;

            min.x += this.pointsRadius;
            min.y += this.pointsRadius;
            min.z += this.pointsRadius;

            max.x -= this.pointsRadius;
            max.y -= this.pointsRadius;
            max.z -= this.pointsRadius;

            
            for (int i = 0; i < this.numberOfSegments; i++)
            {
                var pos0 = float3.zero; var pos1 = float3.zero;

                pos0.x = UnityEngine.Random.Range(min.x, max.x);
                pos0.y = UnityEngine.Random.Range(min.y, max.y);
                pos0.z = UnityEngine.Random.Range(min.z, max.z);

                pos1.x = UnityEngine.Random.Range(min.x, max.x);
                pos1.y = UnityEngine.Random.Range(min.y, max.y);
                pos1.z = UnityEngine.Random.Range(min.z, max.z);

                float3 vel0 = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(this.velocityRange.x, this.velocityRange.y);
                float3 vel1 = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(this.velocityRange.x, this.velocityRange.y);

                this.positions.Add(pos0);
                this.positions.Add(pos1);

                this.segments.Add(new LineSegment3D()
                {
                    a = pos0,
                    b = pos1,
                });

                this.velocities.Add(vel0);
                this.velocities.Add(vel1);
            }

            this.pointPositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, this.numberOfSegments * 2, sizeof(float3));
            this.pointPositionBuffer.SetData(this.positions.AsArray());

            this.pointsMaterial.SetBuffer("_Positions", this.pointPositionBuffer);
            this.linesMaterial.SetBuffer("_Positions", this.pointPositionBuffer);
        }

        [BurstCompile]
        private struct UpdateSegmentsJob : IJobParallelFor
        {
            public Bounds bounds;

            public float deltaTime;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float3> velocities;

            [NoAlias, NativeDisableParallelForRestriction]  
            public NativeList<LineSegment3D> segments;

            [NoAlias, NativeDisableParallelForRestriction]  
            public NativeList<float3> positions;

            private void CheckVelocity(float3 min, float3 max, ref float3 pos, ref float3 velocity)
            {
                if (pos.x > max.x)
                {
                    velocity = math.reflect(velocity, new float3(-1.0f, 0.0f, 0.0f));
                    pos.x -= (pos.x - max.x) * 2.0f;
                }
                else if (pos.x < min.x)
                {
                    velocity = math.reflect(velocity, new float3(1.0f, 0.0f, 0.0f));
                    pos.x += (min.x - pos.x) * 2.0f;
                }

                if (pos.y > max.y)
                {
                    velocity = math.reflect(velocity, new float3(0.0f, -1.0f, 0.0f));
                    pos.y -= (pos.y - max.y) * 2.0f;
                }
                else if (pos.y < min.y)
                {
                    velocity = math.reflect(velocity, new float3(0.0f, 1.0f, 0.0f));
                    pos.y += (min.y - pos.y) * 2.0f;
                }

                if (pos.z > max.z)
                {
                    velocity = math.reflect(velocity, new float3(0.0f, 0.0f, -1.0f));
                    pos.z -= (pos.z - max.z) * 2.0f;
                }
                else if (pos.z < min.z)
                {
                    velocity = math.reflect(velocity, new float3(0.0f, 0.0f, 1.0f));
                    pos.z += (min.z - pos.z) * 2.0f;
                }
            }

            public void Execute(int index)
            {
                var min = this.bounds.min;
                var max = this.bounds.max;

                var vel0 = this.velocities[index * 2];
                var vel1 = this.velocities[index * 2 + 1];

                var segment = this.segments[index];

                var nextPosA = (float3)segment.a + vel0 * this.deltaTime;
                var nextPosB = (float3)segment.b + vel1 * this.deltaTime;

                this.CheckVelocity(min, max, ref nextPosA, ref vel0);
                this.CheckVelocity(min, max, ref nextPosB, ref vel1);

                this.velocities[index * 2 + 0] = vel0;
                this.velocities[index * 2 + 1] = vel1;

                segment.a = nextPosA;
                segment.b = nextPosB;

                this.segments[index] = segment;
                this.positions[index * 2 + 0] = nextPosA;
                this.positions[index * 2 + 1] = nextPosB;
            }
        }

        private void DrawPlaneMesh()
        {
            this.planeFilter.sharedMesh = IntersectionMeshUtil.PlaneCuboidIntersectionMesh(this.currentPlane, this.bounds);
        }

        private unsafe void Update()
        {

            var updateSegmentsJob = new UpdateSegmentsJob()
            {
                deltaTime = Time.deltaTime,
                bounds = this.bounds,
                segments = this.segments,
                velocities = this.velocities,
                positions = this.positions,
            };
            updateSegmentsJob.Schedule(this.numberOfSegments, 64).Complete();

            this.pointPositionBuffer.SetData(this.positions.AsArray());

            linePlaneIntersectionMarker.Begin();

            var intersectionJob = LineIntersection.FindLSPlaneIntersectionsCombinatorialParallel(this.currentPlane, this.segments, ref this.intersections);
            intersectionJob.Complete();

            linePlaneIntersectionMarker.End();

            Graphics.RenderMeshPrimitives(this.pointsParams, this.pointMesh, 0, this.numberOfSegments * 2);
            Graphics.RenderPrimitives(this.linesParams, MeshTopology.Lines, 2, this.numberOfSegments);

            if (this.intersectionPositionBuffer != null) this.intersectionPositionBuffer.Release();

            if (this.intersections.Length > 0)
            {
                this.intersectionPositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, this.intersections.Length, sizeof(float3));
                this.intersectionPositionBuffer.SetData(this.intersections.AsArray());

                this.intersectionMaterial.SetBuffer("_Positions", this.intersectionPositionBuffer);

                Graphics.RenderMeshPrimitives(this.intersectionParams, this.pointMesh, 0, this.intersections.Length);
            }

            this.DrawPlaneMesh();

            if(this.linePlaneIntersectionSampler == null || !this.linePlaneIntersectionSampler.isValid)
            {
                this.linePlaneIntersectionSampler = Sampler.Get("LinePlaneIntersection");
            }
        }

        private void Dispose()
        {
            this.segments.Dispose();
            this.velocities.DisposeIfCreated();
            this.positions.DisposeIfCreated();

            this.intersections.DisposeIfCreated();

            this.intersectionPositionBuffer?.Dispose();
            this.pointPositionBuffer.Dispose();
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
