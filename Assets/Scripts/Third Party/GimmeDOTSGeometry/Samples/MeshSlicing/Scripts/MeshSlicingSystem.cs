using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class MeshSlicingSystem : MonoBehaviour
    {
        #region Public Fields

        public enum SelectedMesh
        {
            CUBE = 0,
            CYLINDER = 1,
            MELON = 2,
            WHALE = 3,
        }

        public Bounds bounds;

        public float sliceOffset;

        public Material boundsMaterial;
        public Material planeMaterial;
        public Material melonSliceMaterial;
        public Material whaleSliceMaterial;
        public Material sliceMaterial;

        public MeshRenderer melon;
        public MeshRenderer cube;
        public MeshRenderer cylinder;

        public SelectedMesh selectedMesh = SelectedMesh.CUBE;

        public SkinnedMeshRenderer whale;

        #endregion

        #region Private Fields

        private bool slice = false;

        private GameObject planeObject;
        private GameObject sliceA;
        private GameObject sliceB;

        private Mesh melonMesh;
        private Mesh whaleMesh;
        private Mesh cubeMesh;
        private Mesh cylinderMesh;

        private MeshFilter planeFilter;
        private MeshRenderer planeRenderer;

        private MeshFilter sliceAFilter;
        private MeshFilter sliceBFilter;

        private MeshRenderer sliceARenderer;
        private MeshRenderer sliceBRenderer;

        private Plane slicePlane;

        private Sampler meshSlicingSampler = null;

        #endregion

        private static readonly ProfilerMarker meshSlicingMarker = new ProfilerMarker("MeshSlicing");

        public bool IsSlicing() => this.slice;
        public void SetSlicing(bool enable)
        {
            this.slice = enable;
            this.planeObject.SetActive(enable);
        }

        public Sampler GetMeshSlicingSampler() => this.meshSlicingSampler;

        public void SetPlane(Vector3 normal, float dist)
        {
            this.slicePlane = new Plane(normal, dist);
        }

        public Plane GetPlane() => this.slicePlane;

        private void Start()
        {
            var boundsGo = new GameObject("Bounds");
            boundsGo.transform.parent = this.transform;
            var boundsMR = boundsGo.AddComponent<MeshRenderer>();
            var boundsMF = boundsGo.AddComponent<MeshFilter>();

            var expandedBounds = this.bounds;
            expandedBounds.Expand(0.1f);

            boundsMR.sharedMaterial = this.boundsMaterial;
            boundsMF.sharedMesh = MeshUtil.CreateBoxOutline(expandedBounds, 0.1f);

            var melonMeshFilter = this.melon.GetComponentInChildren<MeshFilter>();
            this.melonMesh = melonMeshFilter.sharedMesh;
            this.whaleMesh = new Mesh();
            
            var cubeMeshFilter = this.cube.GetComponentInChildren<MeshFilter>();
            this.cubeMesh = cubeMeshFilter.sharedMesh;

            var cylinderMeshFilter = this.cylinder.GetComponentInChildren<MeshFilter>();
            this.cylinderMesh = cylinderMeshFilter.sharedMesh;

            this.slicePlane = new Plane(Vector3.up, 0.0f);

            this.planeObject = new GameObject("Plane");
            this.planeObject.transform.parent = this.transform.parent;
            this.planeObject.transform.position = Vector3.zero;
            this.planeObject.SetActive(false);

            this.planeFilter = this.planeObject.AddComponent<MeshFilter>();
            this.planeRenderer = this.planeObject.AddComponent<MeshRenderer>();

            this.planeRenderer.material = this.planeMaterial;

            this.sliceA = new GameObject("SliceA");
            this.sliceB = new GameObject("SliceB");
            this.sliceA.transform.parent = this.transform;
            this.sliceB.transform.parent = this.transform;

            this.sliceAFilter = this.sliceA.AddComponent<MeshFilter>();
            this.sliceBFilter = this.sliceB.AddComponent<MeshFilter>();

            this.sliceARenderer = this.sliceA.AddComponent<MeshRenderer>();
            this.sliceBRenderer = this.sliceB.AddComponent<MeshRenderer>();

            this.sliceA.SetActive(false);
            this.sliceB.SetActive(false);
        }

        private void ShowMeshesDefault()
        {
            this.sliceA.SetActive(false);
            this.sliceB.SetActive(false);

            this.whale.enabled = false;
            this.melon.enabled = false;
            this.cube.enabled = false;
            this.cylinder.enabled = false;

            switch(this.selectedMesh)
            {
                case SelectedMesh.CYLINDER:
                    this.cylinder.enabled = true;
                    break;
                case SelectedMesh.CUBE:
                    this.cube.enabled = true;
                    break;
                case SelectedMesh.MELON:
                    this.melon.enabled = true;
                    break;
                case SelectedMesh.WHALE:
                    this.whale.enabled = true;
                    break;
            }
        }

        private void Update()
        {
            if (this.planeFilter.sharedMesh != null) GameObject.Destroy(this.planeFilter.sharedMesh);
            this.planeFilter.sharedMesh = IntersectionMeshUtil.PlaneCuboidIntersectionMesh(this.slicePlane, this.bounds);

            if (this.slice)
            {

                if (this.selectedMesh == SelectedMesh.WHALE)
                {
                    this.whale.BakeMesh(this.whaleMesh);
                }

                Mesh[] slicedMeshes = null;

                meshSlicingMarker.Begin();
                switch(this.selectedMesh)
                {
                    case SelectedMesh.MELON:
                        slicedMeshes = MeshSlicing.Slice(this.melonMesh, this.slicePlane);
                        break;
                    case SelectedMesh.CYLINDER:
                        slicedMeshes = MeshSlicing.Slice(this.cylinderMesh, this.slicePlane);
                        break;
                    case SelectedMesh.CUBE:
                        slicedMeshes = MeshSlicing.Slice(this.cubeMesh, this.slicePlane);
                        break;
                    case SelectedMesh.WHALE:
                        slicedMeshes = MeshSlicing.Slice(this.whaleMesh, this.slicePlane);
                        break;
                }
                meshSlicingMarker.End();

                this.whale.enabled = false;
                this.melon.enabled = false;
                this.cube.enabled = false;
                this.cylinder.enabled = false;

                if (slicedMeshes != null)
                {
                    var sliceA = slicedMeshes[0];
                    var sliceB = slicedMeshes[1];

                    Material[] materials = new Material[sliceA.subMeshCount];
                    if (this.selectedMesh == SelectedMesh.WHALE) {

                        materials[0] = this.whale.material;
                        for (int i = 1; i < materials.Length; i++)
                        {
                            materials[i] = this.whaleSliceMaterial;
                        }
                    } else if(this.selectedMesh == SelectedMesh.MELON)
                    {
                        materials[0] = this.melon.material;
                        for (int i = 1; i < materials.Length; i++)
                        {
                            materials[i] = this.melonSliceMaterial;
                        }
                    } else
                    {
                        materials[0] = this.cube.material;
                        for (int i = 1; i < materials.Length; i++)
                        {
                            materials[i] = this.sliceMaterial;
                        }
                    }

                    if(this.sliceAFilter.sharedMesh != null) Destroy(this.sliceAFilter.sharedMesh);
                    if(this.sliceBFilter.sharedMesh != null) Destroy(this.sliceBFilter.sharedMesh);

                    this.sliceAFilter.sharedMesh = sliceA;
                    this.sliceBFilter.sharedMesh = sliceB;

                    this.sliceARenderer.materials = materials;
                    this.sliceBRenderer.materials = materials;

                    var normal = this.slicePlane.normal;

                    var sliceAOffset = normal * this.sliceOffset;
                    var sliceBOffset = -normal * this.sliceOffset;

                    this.sliceA.transform.position = sliceAOffset;
                    this.sliceB.transform.position = sliceBOffset;  

                    this.sliceA.SetActive(true);
                    this.sliceB.SetActive(true);

                } else
                {
                    this.ShowMeshesDefault();
                }
                
            } else
            {
                this.ShowMeshesDefault();
            }

            if(this.meshSlicingSampler == null || !this.meshSlicingSampler.isValid)
            {
                this.meshSlicingSampler = Sampler.Get("MeshSlicing");
            }
        }

    }
}
