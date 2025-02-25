
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class ComputationalGeometrySystem : MonoBehaviour
    {

        #region Public Variables

        public float lineWidth;
        public float startDelay = 0.5f;

        public float lineMaterialSweepTime = 6.0f;
        public float lineMaterialSweepStart = -15.0f;
        public float lineMaterialSweepDistance = 40.0f;

        public float primaryLineSweepDelay = 1.0f;
        public float secondaryLineSweepTime = 6.0f;

        public float lightTimeDelay = 5.0f;

        public GameObject intersectionStructure;

        public List<LineSegment2D> lineSegments = new List<LineSegment2D>();
        public List<LineSegment2D> secondaryLineSegments = new List<LineSegment2D>();

        public Material lineRendererMaterial;
        public Material secondaryLineRendererMaterial;

        public ParticleSystem lightsParticles;

        #endregion

        #region Private Variables

        private GameObject lineParent;
        private GameObject structureParent;

        private List<GameObject> lineRenderers;
        private List<Structure> intersectionStructures;
        private List<float> segmentLightTimes;

        private NativeList<float2> intersections;
        private NativeList<LineSegment2D> nativeSegments;


        #endregion

        private struct IntersectionXComparer : IComparer<float2>
        {
            public int Compare(float2 a, float2 b)
            {
                return a.x.CompareTo(b.x);
            }
        }

        private void CreateIntersectionStructure(float2 intersection)
        {
            var go = GameObject.Instantiate(this.intersectionStructure);
            go.transform.parent = this.structureParent.transform;

            go.transform.position = new Vector3(intersection.x, 0.05f, intersection.y);
            go.transform.rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(0.0f, 360.0f), Vector3.up);

            var structureComp = go.GetComponent<Structure>();

            if (structureComp != null)
            {
                this.intersectionStructures.Add(structureComp);
            }
        }

        private void CreateLine(LineSegment2D segment, float height, float lineMul, Material material)
        {
            var go = new GameObject();
            go.transform.parent = this.lineParent.transform;

            var lineRenderer = go.AddComponent<LineRenderer>();
            lineRenderer.material = material;

            lineRenderer.positionCount = 2;
            lineRenderer.numCapVertices = 2;
            lineRenderer.widthMultiplier = this.lineWidth * lineMul;

            lineRenderer.SetPositions(new Vector3[]
            {
            new Vector3(segment.a.x, height, segment.a.y),
            new Vector3(segment.b.x, height, segment.b.y),
            });

            this.lineRenderers.Add(go);
        }

        private void Awake()
        {
            this.lineRendererMaterial.SetFloat("_XLimit", this.lineMaterialSweepStart);
            this.secondaryLineRendererMaterial.SetFloat("_XLimit", this.lineMaterialSweepStart);
        }

        private void Start()
        {
            this.lineParent = new GameObject("Line Parent");
            this.lineParent.transform.parent = this.transform;

            this.structureParent = new GameObject("Structure Parent");
            this.structureParent.transform.parent = this.transform;

            this.nativeSegments = new NativeList<LineSegment2D>(this.lineSegments.Count, Allocator.Persistent);
            this.intersections = new NativeList<float2>(Allocator.Persistent);

            this.nativeSegments.CopyFromNBC(this.lineSegments.ToArray());

            var intersectionJob = LineIntersection.FindLineSegmentIntersectionsCombinatorial(this.nativeSegments, ref this.intersections);
            intersectionJob.Complete();

            var sortJob = this.intersections.SortJob(new IntersectionXComparer());
            sortJob.Schedule().Complete();

            this.lineRenderers = new List<GameObject>();
            for (int i = 0; i < this.nativeSegments.Length; i++)
            {
                this.CreateLine(this.nativeSegments[i], 0.0f, 1.0f, this.lineRendererMaterial);
            }


            this.secondaryLineSegments.AddRange(this.lineSegments);
            for (int i = 0; i < this.secondaryLineSegments.Count; i++)
            {
                this.CreateLine(this.secondaryLineSegments[i], -0.05f, 2.0f, this.secondaryLineRendererMaterial);
            }


            this.intersectionStructures = new List<Structure>();
            for (int i = 0; i < this.intersections.Length; i++)
            {
                this.CreateIntersectionStructure(this.intersections[i]);
            }


            this.segmentLightTimes = new List<float>();
            for (int i = 0; i < this.nativeSegments.Length; i++)
            {
                this.segmentLightTimes.Add(UnityEngine.Random.Range(0.0f, this.lightTimeDelay));
            }

            this.StartCoroutine(this.BlendInSecondaryLines());
            this.StartCoroutine(this.BlendInLines());
        }

        private IEnumerator BlendInSecondaryLines()
        {
            yield return new WaitForSeconds(this.startDelay);

            float timer = 0.0f;
            this.secondaryLineRendererMaterial.SetFloat("_XLimit", this.lineMaterialSweepStart);
            float x = this.lineMaterialSweepStart;

            float speed = this.lineMaterialSweepDistance / this.lineMaterialSweepTime;

            while (timer < this.lineMaterialSweepTime)
            {
                x += speed * Time.deltaTime;
                this.secondaryLineRendererMaterial.SetFloat("_XLimit", x);

                yield return null;
                timer += Time.deltaTime;
            }
        }

        private IEnumerator BlendInLines()
        {
            yield return new WaitForSeconds(this.startDelay);

            yield return new WaitForSeconds(this.primaryLineSweepDelay);

            float timer = 0.0f;
            this.lineRendererMaterial.SetFloat("_XLimit", this.lineMaterialSweepStart);
            float x = this.lineMaterialSweepStart;

            float speed = this.lineMaterialSweepDistance / this.lineMaterialSweepTime;

            while (timer < this.lineMaterialSweepTime)
            {
                x += speed * Time.deltaTime;
                this.lineRendererMaterial.SetFloat("_XLimit", x);

                yield return null;
                timer += Time.deltaTime;
            }
        }

        private void HandleSegments(float x)
        {
            Dictionary<int, LineSegment2D> potentialSegments = new Dictionary<int, LineSegment2D>();

            for (int i = 0; i < this.nativeSegments.Length; i++)
            {
                var segment = this.nativeSegments[i];
                if (segment.a.x < x - 5.0f && segment.b.x < x - 5.0f)
                {
                    potentialSegments.Add(i, segment);
                }
            }

            foreach (var segment in potentialSegments)
            {
                var time = this.segmentLightTimes[segment.Key];

                this.segmentLightTimes[segment.Key] += Time.deltaTime;
                if (time > this.lightTimeDelay)
                {

                    bool startA = UnityEngine.Random.value < 0.5f;

                    Vector2 start = startA ? segment.Value.a : segment.Value.b;
                    Vector2 end = startA ? segment.Value.b : segment.Value.a;

                    Vector2 dir = end - start;
                    Vector3 dir3D = new Vector3(dir.x, 0.0f, dir.y);

                    var emitParams = new ParticleSystem.EmitParams()
                    {
                        velocity = dir3D,
                        position = new Vector3(start.x, 0.0f, start.y)
                    };

                    this.lightsParticles.Emit(emitParams, 1);

                    this.segmentLightTimes[segment.Key] = 0.0f;
                }

            }
        }

        private void HandleStructures(float x)
        {
            for (int i = 0; i < this.intersectionStructures.Count; i++)
            {
                var structure = this.intersectionStructures[i];

                if (structure.transform.position.x < x - 5.0f)
                {
                    structure.StartFX();
                }
            }
        }

        private void Update()
        {

            float x = this.lineRendererMaterial.GetFloat("_XLimit");

            this.HandleSegments(x);
            this.HandleStructures(x);


        }

        private void OnDestroy()
        {
            if (this.intersections.IsCreated)
            {
                this.intersections.Dispose();
            }

            if (this.nativeSegments.IsCreated)
            {
                this.nativeSegments.Dispose();
            }
        }

    }
}