using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class Voronoi2DStepper : MonoBehaviour
    {
        //Scrapped - see Voronoi2DJobs.cs
        /*
        #region Public Fields

        public bool finished = false;
        public bool hasDrawnVoronoi = false;

        #endregion

        #region Private Fields

        private bool outOfBounds = false;

        private float lineThickness = 0.1f;

        private GameObject sweepline;
        private GameObject circle = null;

        private List<Mesh> siteMeshes = new List<Mesh>();
        private List<MeshRenderer> meshRenderer = new List<MeshRenderer>();

        private List<GameObject> breakpoints = new List<GameObject>();
        private List<GameObject> arcs = new List<GameObject>();

        private List<MeshFilter> halfEdgeMeshes = new List<MeshFilter>();

        private Material material;
        private MaterialPropertyBlock mpb;

        private Mesh sweeplineMesh;

        private NativeArray<float2> points;
        private NativeReference<Rect> bounds;

        private NativeList<HalfEdge> halfEdges;
        private NativeList<int> halfEdgeSites;
        private NativeList<float2> vertices;

        private NativeArray<NativePolygon2D> polygons;
        private NativeArray<int> polygonSites;

        public NativeSortedList<Voronoi2DJobs.EventPoint, Voronoi2DJobs.EventPointComparer> eventQueue;
        public NativeSortedList<Voronoi2DJobs.StatusEntry, Voronoi2DJobs.StatusEntryComparer> status;

        private Voronoi2DJobs.Voronoi2DSweep voronoiSweepJob;

        private Voronoi2DJobs.StatusEntryComparer statusComparer;

        #endregion

        private void InitVoronoi2D()
        {
            float epsilon = 10e-5f;

            this.halfEdges = new NativeList<HalfEdge>(Allocator.Persistent);
            this.halfEdgeSites = new NativeList<int>(Allocator.Persistent);
            this.vertices = new NativeList<float2>(Allocator.Persistent);

            this.voronoiSweepJob = new Voronoi2DJobs.Voronoi2DSweep()
            {
                bounds = this.bounds,
                epsilon = epsilon,
                halfEdges = this.halfEdges,
                points = this.points,
                vertices = this.vertices,
                halfEdgeSites = this.halfEdgeSites,
            };
            this.voronoiSweepJob.InitBoundaryValues();

            this.statusComparer = new Voronoi2DJobs.StatusEntryComparer()
            {
                epsilon = epsilon,
                sweepLine = this.bounds.Value.yMax,
            };

            this.sweepline.transform.position = new Vector3(this.bounds.Value.center.x, 0.0f, this.bounds.Value.yMax);

            this.eventQueue = new NativeSortedList<Voronoi2DJobs.EventPoint, Voronoi2DJobs.EventPointComparer>(new Voronoi2DJobs.EventPointComparer() { epsilon = epsilon}, Allocator.Persistent);
            this.status = new NativeSortedList<Voronoi2DJobs.StatusEntry, Voronoi2DJobs.StatusEntryComparer>(this.statusComparer, Allocator.Persistent);

            for(int i = 0; i < this.points.Length; i++)
            {
                var eventPoint = new Voronoi2DJobs.EventPoint()
                {
                    leafPtr = -1,
                    position = this.points[i],
                    type = Voronoi2DJobs.EventType.SITE_EVENT,
                    site = i,
                };

                this.eventQueue.Insert(eventPoint);
            }
        }

        public void Init(NativeArray<float2> sites, float siteRadius, float lineThickness)
        {
            this.lineThickness = lineThickness;
            this.bounds = new NativeReference<Rect>(Allocator.Persistent);

            var boundsJob = new HullAlgorithmJobs.BoundingRectangleJob()
            {
                boundingRect = this.bounds,
                points = sites,
            };

            boundsJob.Schedule().Complete();
            var r = this.bounds.Value;
            r.Expand(1.0f);
            this.bounds.Value = r;


            this.points = sites;

            var shader = Shader.Find("Unlit/Color");
            if(shader != null)
            {
                this.material = new Material(shader);
                this.material.color = Color.cyan;
                this.material.enableInstancing = true;
            }

            var boundsGo = new GameObject("Bounds");
            var boundsMr = boundsGo.AddComponent<MeshRenderer>();
            var boundsMf = boundsGo.AddComponent<MeshFilter>();

            boundsMr.sharedMaterial = this.material;
            boundsMf.mesh = MeshUtil.CreateRectangleOutline(this.bounds.Value, this.lineThickness * 0.5f);

            this.mpb = new MaterialPropertyBlock();

            var mesh = MeshUtil.CreateRing(siteRadius, lineThickness * 0.3f, 8, CardinalPlane.XZ);
            for (int i = 0; i < this.points.Length; i++)
            {
                this.siteMeshes.Add(mesh);

                var siteMeshGo = new GameObject($"Site_{i}");
                siteMeshGo.transform.position = new Vector3(this.points[i].x, 0.0f, this.points[i].y);
                var mr = siteMeshGo.AddComponent<MeshRenderer>();
                var filter = siteMeshGo.AddComponent<MeshFilter>();

                filter.sharedMesh = mesh;
                mr.sharedMaterial = this.material;

                this.meshRenderer.Add(mr);
            }

            this.sweepline = new GameObject("Sweepline");
            this.sweepline.transform.position = new Vector3(this.bounds.Value.center.x, 0.0f, this.bounds.Value.center.y);
            var sweepMr = sweepline.AddComponent<MeshRenderer>();
            var sweepMf = sweepline.AddComponent<MeshFilter>();

            float sweepWidth = this.bounds.Value.width;
            var sweepLs = new LineSegment2D(new float2(-sweepWidth / 2.0f, 0.0f), new float2(sweepWidth / 2.0f, 0.0f));
            this.sweeplineMesh = MeshUtil.CreateLine(sweepLs, lineThickness * 0.1f);


            sweepMr.sharedMaterial = this.material;
            sweepMf.sharedMesh = this.sweeplineMesh;

            this.polygons = new NativeArray<NativePolygon2D>(this.points.Length, Allocator.Persistent);
            this.polygonSites = new NativeArray<int>(this.points.Length, Allocator.Persistent);
            for(int i = 0; i < this.polygons.Length; i++)
            {
                this.polygons[i] = new NativePolygon2D(Allocator.Persistent, 4);
            }
            this.InitVoronoi2D();

            this.finished = false;
        }



        private void DrawHalfEdges()
        {
            int meshCount = this.halfEdgeMeshes.Count;
            int halfEdgeCount = this.halfEdges.Length;
            for (int i = 0; i < meshCount; i++)
            {
                var meshFilter = this.halfEdgeMeshes[i];

                var halfEdge = this.halfEdges[i];
                var vertex0 = this.vertices[halfEdge.vertexFwd];
                var vertex1 = this.vertices[halfEdge.vertexBack];

                var ls = new LineSegment2D(vertex0, vertex1);

                meshFilter.mesh = MeshUtil.CreateLine(ls, this.lineThickness * 0.1f);
            }

            for (int i = meshCount; i < halfEdgeCount; i++)
            {
                var halfEdgeGo = new GameObject($"HalfEdge_{i}");
                var mr = halfEdgeGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = this.material;

                var meshFilter = halfEdgeGo.AddComponent<MeshFilter>();

                var halfEdge = this.halfEdges[i];
                var vertex0 = this.vertices[halfEdge.vertexFwd];
                var vertex1 = this.vertices[halfEdge.vertexBack];

                var ls = new LineSegment2D(vertex0, vertex1);

                meshFilter.mesh = MeshUtil.CreateLine(ls, this.lineThickness * 0.1f);
                this.halfEdgeMeshes.Add(meshFilter);
            }
        }

        private void CreatePolygons()
        {

            var toPolygonJob = new Voronoi2DJobs.Voronoi2DToPolygonsJob()
            {
                halfEdges = this.halfEdges,
                polygons = this.polygons,
                vertices = this.vertices,
                epsilon = 10e-5f,
                bounds = this.bounds,
                halfEdgeSites = this.halfEdgeSites,
                polygonSites = this.polygonSites,
            };
            toPolygonJob.Schedule().Complete();

            for (int i = 0; i < this.polygons.Length; i++)
            {
                var polygon = this.polygons[i];
                //Voronoi Cells are convex
                var triangulation = Polygon2DTriangulation.FanTriangulation(polygon);

                var polyMesh = MeshUtil.CreatePolygonMesh(this.polygons[i], triangulation);

                var polyGo = new GameObject($"Polygon_{i}");
                var polyMr = polyGo.AddComponent<MeshRenderer>();
                var polyMf = polyGo.AddComponent<MeshFilter>();
                polyGo.transform.position = Vector3.down * 0.01f;

                this.mpb.SetColor("_Color", new Color(Random.value, Random.value, Random.value) * 0.5f);

                polyMr.material = this.material;
                polyMr.SetPropertyBlock(this.mpb);

                polyMf.sharedMesh = polyMesh;

            }
        }

        private void DrawCircle(Voronoi2DJobs.EventPoint eventPoint)
        {
            if (this.circle != null) GameObject.Destroy(this.circle);

            if(eventPoint.type == Voronoi2DJobs.EventType.CIRCLE_EVENT)
            {
                float r = math.length(eventPoint.position - eventPoint.circlePosition);
                var mesh = MeshUtil.CreateRing(r, this.lineThickness * 0.1f);

                this.circle = new GameObject("CircleEvent");
                var meshFilter = this.circle.AddComponent<MeshFilter>();
                var meshRenderer = this.circle.AddComponent<MeshRenderer>();

                meshRenderer.material = this.material;
                meshFilter.mesh = mesh;

                this.circle.transform.position = new Vector3(eventPoint.circlePosition.x, 0.0f, eventPoint.circlePosition.y);
            }
        }

        private Mesh CreateArc(float2 vertex, float sweepline, float range = 2.0f, int samples = 32)
        {
            float t = 1.0f / (2.0f * (vertex.y - sweepline));

            float a = t;
            float b = t * -2.0f * vertex.x;
            float c = t * (math.dot(vertex, vertex) - sweepline * sweepline);

            var parabola = new Parabola(a, b, c);

            float currentX = vertex.x - (range / 2.0f);

            float2[] points = new float2[samples];
            for(int i = 0; i < samples; i++)
            {
                float y = parabola.Evaluate(currentX);
                points[i] = new float2(currentX, y);

                currentX += range / (float)(samples - 1);
            }

            return MeshUtil.CreateLineStrip(points, this.lineThickness * 0.2f);
        }

        private void DrawArcs()
        {
            for (int i = 0; i < this.arcs.Count; i++)
            {
                GameObject.Destroy(this.arcs[i]);
            }
            this.arcs.Clear();

            if (this.eventQueue.Length > 0)
            {
                var nextStepEvent = this.eventQueue[0];
                for (int i = 0; i < this.points.Length; i++)
                {
                    var vertex = this.points[i];
                    float y = nextStepEvent.position.y;

                    if (y < vertex.y - 10e-5f)
                    {
                        var go = new GameObject($"Arc_{i}");
                        var mr = go.AddComponent<MeshRenderer>();
                        var mf = go.AddComponent<MeshFilter>();

                        this.mpb.SetColor("_Color", Color.gray * 0.3f);

                        mr.material = this.material;
                        mr.SetPropertyBlock(this.mpb);
                        mf.mesh = this.CreateArc(vertex, nextStepEvent.position.y);

                        this.arcs.Add(go);
                    }
                }
            }
        }

        private void DrawBreakpoints(float sweepY)
        {
            for(int i = 0; i < this.breakpoints.Count; i++)
            {
                GameObject.Destroy(this.breakpoints[i]);
            }
            this.breakpoints.Clear();

            foreach(var element in this.status)
            {
                element.Breakpoint(sweepY, out float2 i0, out float2 i1);
                float2 crossDir = math.normalize(element.BisectorDirection().Perpendicular());
                if (!math.any(math.isnan(crossDir)))
                {

                    var ls = new LineSegment2D(i1 - crossDir * 0.15f, i1 + crossDir * 0.15f);

                    var mesh = MeshUtil.CreateLine(ls, this.lineThickness * 0.1f);

                    var go = new GameObject($"Breakpoint_{element.leftSite}_{element.rightSite}");
                    var mr = go.AddComponent<MeshRenderer>();
                    var mf = go.AddComponent<MeshFilter>();

                    this.mpb.SetColor("_Color", Color.red);

                    mr.material = this.material;
                    mr.SetPropertyBlock(this.mpb);
                    mf.mesh = mesh;

                    go.transform.position = new Vector3(0.0f, 0.01f, 0.0f);

                    this.breakpoints.Add(go);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!this.hasDrawnVoronoi)
            {
                var oldColor = Handles.color;
                Handles.color = Color.cyan;

                int halfEdgeCount = this.halfEdges.Length;
                for (int i = 0; i < halfEdgeCount; i++)
                {
                    var halfEdge = this.halfEdges[i];
                    var site = this.halfEdgeSites[i];

                    var vertex0 = this.vertices[halfEdge.vertexFwd];
                    var vertex1 = this.vertices[halfEdge.vertexBack];

                    float2 dir = vertex1 - vertex0;
                    var perp = dir.Perpendicular();
                    float2 avgPos = (vertex0 + vertex1) * 0.5f;

                    float2 labelPos = avgPos - math.normalize(perp) * 0.1f;

                    Handles.Label(new Vector3(labelPos.x, 0.0f, labelPos.y), $"{site}");
                }
                Handles.color = oldColor;
            }
        }

        public void Step()
        {
            if(!this.eventQueue.IsEmpty() && !this.outOfBounds)
            {
                var nextEvent = this.voronoiSweepJob.FetchNextEvent(ref this.eventQueue, ref this.status);
                if (nextEvent.position.y < this.bounds.Value.y)
                {
                    this.outOfBounds = true;
                }
                else
                {
                    switch (nextEvent.type)
                    {
                        case Voronoi2DJobs.EventType.SITE_EVENT:
                            this.voronoiSweepJob.HandleSiteEvent(nextEvent, ref this.eventQueue, ref this.status);
                            break;
                        case Voronoi2DJobs.EventType.CIRCLE_EVENT:
                            this.voronoiSweepJob.HandleCircleEvent(nextEvent, ref this.eventQueue, ref this.status);
                            break;
                    }

                    this.DrawCircle(nextEvent);
                    this.DrawBreakpoints(nextEvent.position.y);
                    this.DrawArcs();
                    this.DrawHalfEdges();

                    if (this.eventQueue.Length > 0)
                    {
                        var nextStepEvent = this.eventQueue[0];
                        this.sweepline.transform.position = new Vector3(this.sweepline.transform.position.x, 0.0f, nextStepEvent.position.y);
                    }
                    else
                    {
                        this.sweepline.transform.position = Vector3.one * 10000;
                    }
                }

            } else if(!this.hasDrawnVoronoi)
            {

                this.DrawHalfEdges();

                for (int i = 0; i < this.breakpoints.Count; i++)
                {
                    GameObject.Destroy(this.breakpoints[i]);
                }
                this.breakpoints.Clear();

                this.CreatePolygons();

                this.hasDrawnVoronoi = true;
            } else
            {
                this.finished = true;
            }
        }

        public void OnDestroy()
        {
            if (this.bounds.IsCreated)
            {
                this.bounds.Dispose();
            }
            this.points.DisposeIfCreated();
            this.halfEdges.DisposeIfCreated();
            this.vertices.DisposeIfCreated();

            if(this.eventQueue.IsCreated)
            {
                this.eventQueue.Dispose();
            }

            if(this.status.IsCreated)
            {
                this.status.Dispose();
            }

            if(this.polygons.IsCreated)
            {
                this.polygons.Dispose();
            }

            this.halfEdgeSites.DisposeIfCreated();
            this.polygonSites.DisposeIfCreated();
        }*/

    }
        
}
