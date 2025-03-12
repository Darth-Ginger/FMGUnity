using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;



namespace GimmeDOTSGeometry.Samples
{
    public class VoronoiSystem : MonoBehaviour
    {
        #region Public Fields

        public float ringRadius = 0.1f;
        public float ringThickness = 0.02f;
        public float rectThickness = 0.1f;
        public float lineThickness = 0.1f;
        public float spaceshipSize = 0.3f;
        public float spaceshipYOffset = 0.05f;
        public float siteInfluence = 0.2f;

        public GameObject spaceshipPrefab = null;

        public Gradient cellGradient = new Gradient();

        public int nrOfPoints = 5;
        public int voLTWidth = 200;
        public int voLTHeight = 200;
        public int nrOfSpaceShips = 1;

        public Material boundsMaterial = null;
        public Material ringMaterial = null;
        public Material cellMaterial = null;

        public Rect bounds;

        public Vector2 maxVelocity;

        #endregion

        #region Private Fields

        private bool showDelaunay = false;
        private bool useVoLT = false;
        private bool showSpaceships = false;

        private float lastVoronoiCalculationTime = 0.0f;
        private float lastVoLTCalculationTime = 0.0f;

        private int currentSpaceships = 0;

        private List<GameObject> points = new List<GameObject>();

        [SerializeField] private List<GameObject> cells = new List<GameObject>();
        private List<MeshFilter> cellMFs = new List<MeshFilter>();
        [SerializeField] private List<GameObject> cellOutlines = new List<GameObject>();
        private List<MeshFilter> cellOutlineMFs = new List<MeshFilter>();

        [SerializeField] private List<DCEL> cellsDCELs = new List<DCEL>();

        [SerializeField] private List<GameObject> delaunayTriangles = new List<GameObject>();
        private List<MeshFilter> delaunayTrianglesMFs = new List<MeshFilter>();

        private List<GameObject> spaceships = new List<GameObject>();

        private MaterialPropertyBlock cellColorBlock;

        private NativeList<float2> sites;
        private NativeArray<NativePolygon2D> polygons;
        [SerializeField] private NativeArray<int> polygonSites;
        private NativeArray<int> voronoiLookupTable;

        private NativeList<float2> velocities;

        private Sampler voronoiSampler = null;
        private Sampler voLTSampler = null;
        private Sampler spaceshipSampler = null;


        private TransformAccessArray spaceshipTransforms;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";


        private static readonly ProfilerMarker voronoiMarker = new ProfilerMarker("Voronoi");
        private static readonly ProfilerMarker voLTMarker = new ProfilerMarker("voLT");
        private static readonly ProfilerMarker spaceshipsMarker = new ProfilerMarker("Spaceships");

        public bool IsShowingDelaunay => this.showDelaunay;
        public bool IsUsingVoLT => this.useVoLT;
        public bool IsShowingSpaceships => this.showSpaceships;

        public float LastVoronoiCalculationTime => this.lastVoronoiCalculationTime;
        public float LastVoLTCalculationTime => this.lastVoLTCalculationTime;

        public int CurrentSpaceships => this.currentSpaceships;

        public Sampler GetVoronoiSampler() => this.voronoiSampler;
        public Sampler GetVoLTSampler() => this.voLTSampler;
        public Sampler GetSpaceshipSampler() => this.spaceshipSampler;

        public void ShowDelaunayTriangulation(bool enable)
        {
            this.showDelaunay = enable;
        }

        public void UseVoronoiLookupTable(bool enable)
        {
            this.useVoLT = enable;
        }

        public void ShowSpaceships(bool show)
        {
            this.showSpaceships = show;

            for(int i = 0; i < this.spaceships.Count; i++)
            {
                this.spaceships[i].SetActive(show);
            }
        }

        public void AddSpaceShip(int nrOfSpaceships)
        {
            float spaceshipHalf = this.spaceshipSize * 0.5f;
            for(int i = 0; i < nrOfSpaceships; i++)
            {
                float rndX = UnityEngine.Random.Range(this.bounds.xMin + spaceshipHalf, this.bounds.xMax - spaceshipHalf);
                float rndY = UnityEngine.Random.Range(this.bounds.yMin + spaceshipHalf, this.bounds.yMax - spaceshipHalf);

                var worldPos = new Vector3(rndX, this.spaceshipYOffset, rndY);

                var spaceship = GameObject.Instantiate(this.spaceshipPrefab);
                spaceship.transform.parent = this.transform;
                spaceship.transform.position = worldPos;

                var rndVel = UnityEngine.Random.insideUnitCircle;
                rndVel.x *= this.maxVelocity.x;
                rndVel.y *= this.maxVelocity.y;

                this.velocities.Add(rndVel);
                this.spaceships.Add(spaceship);
                this.spaceshipTransforms.Add(spaceship.transform);
                spaceship.SetActive(this.showSpaceships);
            }


            this.currentSpaceships += nrOfSpaceships;
        }

        public void Start()
        {
            var rectMesh = MeshUtil.CreateRectangleOutline(this.bounds, this.rectThickness);

            var rectangleGO = new GameObject("Rectangle");
            rectangleGO.transform.parent = this.transform;
            rectangleGO.transform.position = new Vector3(0.0f, -0.01f, 0.0f);

            var rectMeshFilter = rectangleGO.AddComponent<MeshFilter>();
            rectMeshFilter.mesh = rectMesh;

            var rectMeshRenderer = rectangleGO.AddComponent<MeshRenderer>();
            rectMeshRenderer.material = this.boundsMaterial;

            this.cellColorBlock = new MaterialPropertyBlock();

            this.Create();
        }

        private void CreateSiteRings()
        {

            this.sites = new NativeList<float2>(this.nrOfPoints, Allocator.Persistent);
            this.polygons = new NativeArray<NativePolygon2D>(this.nrOfPoints, Allocator.Persistent);
            for(int i = 0; i < this.nrOfPoints; i++)
            {
                this.polygons[i] = new NativePolygon2D(Allocator.Persistent, 3);
            }

            this.polygonSites = new NativeArray<int>(this.nrOfPoints, Allocator.Persistent);

            for (int i = 0; i < this.points.Count; i++)
            {
                var mf = this.points[i].GetComponent<MeshFilter>();
                GameObject.Destroy(mf.sharedMesh);
                GameObject.Destroy(this.points[i]);
            }

            for(int i = 0; i < this.cells.Count; i++)
            {
                var mf = this.cellMFs[i];
                GameObject.Destroy(mf.sharedMesh);
                GameObject.Destroy(this.cells[i]);
                GameObject.Destroy(this.cellOutlineMFs[i].sharedMesh);
                GameObject.Destroy(this.cellOutlines[i]);
            }
            

            for(int i = 0; i < this.delaunayTriangles.Count; i++)
            {
                var mf = this.delaunayTrianglesMFs[i];
                GameObject.Destroy(mf.sharedMesh);
                GameObject.Destroy(this.delaunayTriangles[i]);
            }

            this.points.Clear();

            this.cells.Clear();
            this.cellMFs.Clear();
            this.cellOutlines.Clear();
            this.cellOutlineMFs.Clear();

            this.delaunayTriangles.Clear();
            this.delaunayTrianglesMFs.Clear();

            var ringMesh = MeshUtil.CreateRing(this.ringRadius, this.ringThickness);

            for (int i = 0; i < this.nrOfPoints; i++)
            {

                float2 rndPos = new float2();
                rndPos.x = UnityEngine.Random.Range(this.bounds.xMin + this.ringRadius * 2, this.bounds.xMax - this.ringRadius * 2);
                rndPos.y = UnityEngine.Random.Range(this.bounds.yMin + this.ringRadius * 2, this.bounds.yMax - this.ringRadius * 2);

                this.sites.Add(rndPos);

                var pointGO = new GameObject($"Point_{i}");
                pointGO.transform.parent = this.transform;
                pointGO.transform.position = new Vector3(rndPos.x, 0.02f, rndPos.y);

                var meshRenderer = pointGO.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = this.ringMaterial;

                var meshFilter = pointGO.AddComponent<MeshFilter>();
                meshFilter.mesh = ringMesh;

                this.points.Add(pointGO);
            }
        }

        private IEnumerator MeasureRoutine()
        {
            yield return null;

            //This recording sadly does not work all the time because, I assume, we sample
            //a random function not in the Update Loop - while the sampler / recorder has
            //the measurements of the previous frame
            var recorder = this.voronoiSampler.GetRecorder();
            if (recorder != null && recorder.elapsedNanoseconds > 0)
            {
                this.lastVoronoiCalculationTime = recorder.elapsedNanoseconds / 10e5f;
            }

            if(this.useVoLT)
            {
                recorder = this.voLTSampler.GetRecorder();
                if(recorder != null && recorder.elapsedNanoseconds > 0)
                {
                    this.lastVoLTCalculationTime = recorder.elapsedNanoseconds / 10e5f;
                }
            }
        }

        public void Create()
        {
            Debug.Log("Creating Voronoi Diagram");
            this.Dispose();

            this.velocities = new NativeList<float2>(Allocator.Persistent);
            this.spaceshipTransforms = new TransformAccessArray(this.nrOfPoints);
            this.voronoiLookupTable = new NativeArray<int>(this.voLTWidth * this.voLTHeight, Allocator.Persistent);

            for (int i = 0; i < this.spaceships.Count; i++)
            {
                GameObject.Destroy(this.spaceships[i]);
            }
            this.spaceships.Clear();

            int lastSpaceshipCount = this.currentSpaceships;
            this.AddSpaceShip(this.currentSpaceships);
            this.currentSpaceships -= lastSpaceshipCount;

            this.CreateSiteRings();

            if (this.voronoiSampler == null || !this.voronoiSampler.isValid)
            {
                this.voronoiSampler = Sampler.Get("Voronoi");
            }

            if(this.voLTSampler == null || !this.voLTSampler.isValid)
            {
                this.voLTSampler = Sampler.Get("voLT");
            }

            voronoiMarker.Begin();

            var voronoiJob = Voronoi2D.CalculateVoronoi(this.bounds, this.sites.AsArray(), ref this.polygons, ref this.polygonSites, out var allocations);
            voronoiJob.Complete();

            voronoiMarker.End();

            if(this.useVoLT)
            {
                voLTMarker.Begin();
                var voLTJob = Voronoi2D.CalculateVoronoiLookupTable(new int2(this.voLTWidth, this.voLTHeight), this.bounds, this.sites.AsArray(), 
                    ref this.voronoiLookupTable, out var voltAllocations);
                voLTJob.Complete();

                voLTMarker.End();

                voltAllocations.Dispose();
            }

            var color = this.ringMaterial.color;
            color.a *= 0.5f;

            //A little bit of a hack, but you're not supposed to get the delaunay triangulation back - just for debugging purposes
            var delaunayTriangulation = (NativeList<int3>)allocations.allocatedMemory[0];
            if (this.showDelaunay)
            {

                var mpb = new MaterialPropertyBlock();

                for (int i = 0; i < delaunayTriangulation.Length; i++) {

                    var triangleIndices = delaunayTriangulation[i];

                    var a = this.sites[triangleIndices.x].AsFloat3(CardinalPlane.XZ);
                    var b = this.sites[triangleIndices.y].AsFloat3(CardinalPlane.XZ);
                    var c = this.sites[triangleIndices.z].AsFloat3(CardinalPlane.XZ);

                    //Unity triangles are clockwise... barbarians (a joke of course)
                    var triangle = new NativeTriangle3D(c, b, a);
                    var triangleMesh = MeshUtil.CreateTriangleOutline(triangle, this.ringThickness);

                    var triangleObj = new GameObject($"Delaunay_Triangle_{i}");
                    triangleObj.transform.position = Vector3.up * 0.02f;

                    var triangleMF = triangleObj.AddComponent<MeshFilter>();
                    var triangleMR = triangleObj.AddComponent<MeshRenderer>();

                    triangleMF.mesh = triangleMesh;
                    triangleMR.sharedMaterial = this.ringMaterial;

                    mpb.SetColor(SHADER_COLOR, color);
                    triangleMR.SetPropertyBlock(mpb);

                    this.delaunayTriangles.Add(triangleObj);
                    this.delaunayTrianglesMFs.Add(triangleMF);
                }
            }

            // #region Voronoi Diagram Conversion

            // // Convert the output to VoronoiDiagram
            // VoronoiDiagram voronoiDiagram = new VoronoiDiagram();
            // voronoiDiagram.InitializeFromGimmeDOTSGeometry(this.bounds, this.points, ref this.polygons, ref this.polygonSites, allocations);
            
            // #endregion


            this.StartCoroutine(this.MeasureRoutine());

            allocations.Dispose();

            for (int i = 0; i < this.polygons.Length; i++)
            {
                //You should always check for the number of points and IsConvex() for fan-triangulated
                //polygons
                if (this.polygons[i].points.Length >= 3 && this.polygons[i].IsConvex())
                {

                    var triangulation = Polygon2DTriangulation.FanTriangulation(this.polygons[i]);
                    var polyMesh = MeshUtil.CreatePolygonMesh(this.polygons[i], triangulation);
                    var polyOutlineMesh = MeshUtil.CreatePolygonOutline(this.polygons[i], this.ringThickness);

                    var cell = new GameObject($"Cell_{i}");
                    var cellMF = cell.AddComponent<MeshFilter>();
                    var cellMR = cell.AddComponent<MeshRenderer>();


                    cellMF.mesh = polyMesh;
                    cellMR.sharedMaterial = this.cellMaterial;

                    var rndColor = this.cellGradient.Evaluate(UnityEngine.Random.value);
                    float a = rndColor.a;
                    rndColor *= UnityEngine.Random.value;
                    rndColor.a = a;
                    this.cellColorBlock.SetColor(SHADER_COLOR, rndColor);

                    cellMR.SetPropertyBlock(this.cellColorBlock);

                    var cellOutline = new GameObject($"Cell_Outline_{i}");
                    cellOutline.transform.position = Vector3.up * 0.01f;
                    var cellOutlineMF = cellOutline.AddComponent<MeshFilter>();
                    var cellOutlineMR = cellOutline.AddComponent<MeshRenderer>();

                    cellOutlineMF.mesh = polyOutlineMesh;
                    cellOutlineMR.sharedMaterial = this.ringMaterial;

                    this.cellColorBlock.SetColor(SHADER_COLOR, color);
                    cellOutlineMR.SetPropertyBlock(this.cellColorBlock);

                    this.cells.Add(cell);
                    this.cellMFs.Add(cellMF);
                    this.cellOutlines.Add(cellOutline);
                    this.cellOutlineMFs.Add(cellOutlineMF);

                    //@note : Added myself
                    var cellDCEL = NativePolygon2D.ConvertToDCEL(this.polygons[i], i);
                    var face = cellDCEL.faces[0];
                    face.id = i;
                    cellDCEL.faces[0] = face;
                    cellsDCELs.Add(cellDCEL);
                } else
                {
                    Debug.LogWarning($"Polygon {i} of the Voronoi Diagram could not be calculated due to floating-point precision problems. Please refer to the manual for more information");
                }
            }

        }

        [BurstCompile]
        private struct UpdateSpaceshipsWithoutVoLTJob : IJobParallelForTransform
        {

            public Rect bounds;

            public float deltaTime;
            public float spaceshipSize;
            public float yOffset;
            public float siteInfluence;

            public ProfilerMarker voronoiMarker;

            [NoAlias]
            public NativeArray<float2> velocities;

            [NoAlias, ReadOnly]
            public NativeArray<float2> sites;

            public void Execute(int index, TransformAccess transform)
            {
                var currentPos = (float3)transform.position;
                var velocity = this.velocities[index];

                float2 nextPos = math.mad(velocity, this.deltaTime, currentPos.xz);

                float xMax = this.bounds.xMax - this.spaceshipSize;
                float xMin = this.bounds.xMin + this.spaceshipSize;
                float yMax = this.bounds.yMax - this.spaceshipSize;
                float yMin = this.bounds.yMin + this.spaceshipSize;

                if (Hint.Unlikely(nextPos.x > xMax))
                {
                    velocity = math.reflect(velocity, new float2(-1.0f, 0.0f));
                    nextPos.x -= (nextPos.x - xMax) * 2.0f;
                }
                else if (Hint.Unlikely(nextPos.x < xMin))
                {
                    velocity = math.reflect(velocity, new float2(1.0f, 0.0f));
                    nextPos.x += (xMin - nextPos.x) * 2.0f;
                }

                if (Hint.Unlikely(nextPos.y > yMax))
                {
                    velocity = math.reflect(velocity, new float2(0.0f, -1.0f));
                    nextPos.y -= (nextPos.y - yMax) * 2.0f;
                }
                else if (Hint.Unlikely(nextPos.y < yMin))
                {
                    velocity = math.reflect(velocity, new float2(0.0f, 1.0f));
                    nextPos.y += (yMin - nextPos.y) * 2.0f;
                }

                float velLength = math.length(velocity);

                int closestSite = -1;
                float closestDistSq = float.PositiveInfinity;

                voronoiMarker.Begin();

                //This is the expensive part that a VoLT can solve
                //A SAS might give you better times, but never O(1)
                for(int i = 0; i < this.sites.Length; i++)
                {
                    float distSq = math.distancesq(this.sites[i], nextPos);
                    if(distSq < closestDistSq)
                    {
                        closestSite = i;
                        closestDistSq = distSq;
                    }
                }

                var sitePos = this.sites[closestSite];

                voronoiMarker.End();

                float2 dirToSite = sitePos - nextPos;
                velocity = math.normalize(math.lerp(math.normalize(velocity), math.normalize(dirToSite), this.siteInfluence * this.deltaTime)) * velLength;

                this.velocities[index] = velocity;
                var pos = new float3(nextPos.x, this.yOffset, nextPos.y);


                //Spaceship is a quad that points upwards, so we rotate it to point correctly with these offsets

                var angle = Vector2.SignedAngle(new Vector2(1, 0), velocity);
                angle = 360.0f - angle;

                var rot = Quaternion.AngleAxis(angle + 90.0f, Vector3.up);
                rot *= Quaternion.AngleAxis(90.0f, Vector3.right);

                transform.position = pos;
                transform.rotation = rot;
            }
        }

        [BurstCompile]
        private struct UpdateSpaceshipsWithVoLTJob : IJobParallelForTransform
        {
            public Rect bounds;

            public float deltaTime;
            public float spaceshipSize;
            public float yOffset;
            public float siteInfluence;

            public int2 voltDimension;

            public ProfilerMarker voronoiMarker;

            [NoAlias]
            public NativeArray<float2> velocities;

            [NoAlias, ReadOnly]
            public NativeArray<float2> sites;

            [NoAlias, ReadOnly]
            public NativeArray<int> volt;

            public void Execute(int index, TransformAccess transform)
            {
                var currentPos = (float3)transform.position;
                var velocity = this.velocities[index];

                float2 nextPos = math.mad(velocity, this.deltaTime, currentPos.xz);

                float xMax = this.bounds.xMax - this.spaceshipSize;
                float xMin = this.bounds.xMin + this.spaceshipSize;
                float yMax = this.bounds.yMax - this.spaceshipSize;
                float yMin = this.bounds.yMin + this.spaceshipSize;

                if (Hint.Unlikely(nextPos.x > xMax))
                {
                    velocity = math.reflect(velocity, new float2(-1.0f, 0.0f));
                    nextPos.x -= (nextPos.x - xMax) * 2.0f;
                }
                else if (Hint.Unlikely(nextPos.x < xMin))
                {
                    velocity = math.reflect(velocity, new float2(1.0f, 0.0f));
                    nextPos.x += (xMin - nextPos.x) * 2.0f;
                }

                if (Hint.Unlikely(nextPos.y > yMax))
                {
                    velocity = math.reflect(velocity, new float2(0.0f, -1.0f));
                    nextPos.y -= (nextPos.y - yMax) * 2.0f;
                }
                else if (Hint.Unlikely(nextPos.y < yMin))
                {
                    velocity = math.reflect(velocity, new float2(0.0f, 1.0f));
                    nextPos.y += (yMin - nextPos.y) * 2.0f;
                }

                float velLength = math.length(velocity);

                voronoiMarker.Begin();

                int tableIndex = Voronoi2D.CalculateVoronoiLookupTableIndex(this.voltDimension, this.bounds, nextPos);
                int closestSite = this.volt[tableIndex];

                var sitePos = this.sites[closestSite];

                voronoiMarker.End();

                float2 dirToSite = sitePos - nextPos;
                velocity = math.normalize(math.lerp(math.normalize(velocity), math.normalize(dirToSite), this.siteInfluence * this.deltaTime)) * velLength;

                this.velocities[index] = velocity;
                var pos = new float3(nextPos.x, this.yOffset, nextPos.y);


                //Spaceship is a quad that points upwards, so we rotate it to point correctly with these offsets

                var angle = Vector2.SignedAngle(new Vector2(1, 0), velocity);
                angle = 360.0f - angle;

                var rot = Quaternion.AngleAxis(angle + 90.0f, Vector3.up);
                rot *= Quaternion.AngleAxis(90.0f, Vector3.right);

                transform.position = pos;
                transform.rotation = rot;
            }
        }


        private void Update()
        {

            if(this.useVoLT)
            {
                var updateWithVoLTJob = new UpdateSpaceshipsWithVoLTJob()
                {
                    bounds = this.bounds,
                    deltaTime = Time.deltaTime,
                    siteInfluence = this.siteInfluence,
                    sites = this.sites.AsArray(),
                    spaceshipSize = this.spaceshipSize,
                    velocities = this.velocities.AsArray(),
                    volt = this.voronoiLookupTable,
                    voltDimension = new int2(this.voLTWidth, this.voLTHeight),
                    yOffset = this.spaceshipYOffset,
                    voronoiMarker = spaceshipsMarker,
                };
                updateWithVoLTJob.Schedule(this.spaceshipTransforms).Complete();

            } else
            {
                var updateWithoutVoLTJob = new UpdateSpaceshipsWithoutVoLTJob()
                {
                    bounds = this.bounds,
                    deltaTime = Time.deltaTime,
                    spaceshipSize = this.spaceshipSize,
                    velocities = this.velocities.AsArray(),
                    yOffset = this.spaceshipYOffset,
                    siteInfluence = this.siteInfluence,
                    sites = this.sites.AsArray(),
                    voronoiMarker = spaceshipsMarker
                };
                updateWithoutVoLTJob.Schedule(this.spaceshipTransforms).Complete();
            }


            if (this.spaceshipSampler == null || !this.spaceshipSampler.isValid)
            {
                this.spaceshipSampler = Sampler.Get("Spaceships");
            }
        }

        private void Dispose()
        {
            this.sites.DisposeIfCreated();
            for(int i = 0; i < this.polygons.Length; i++)
            {
                this.polygons[i].Dispose();
            }
            this.polygons.DisposeIfCreated();
            this.polygonSites.DisposeIfCreated();
            this.voronoiLookupTable.DisposeIfCreated();

            if(this.spaceshipTransforms.isCreated)
            {
                this.spaceshipTransforms.Dispose();
            }
            this.velocities.DisposeIfCreated();
        }

        private void OnDestroy()
        {
            this.Dispose();
        }

    }
}
