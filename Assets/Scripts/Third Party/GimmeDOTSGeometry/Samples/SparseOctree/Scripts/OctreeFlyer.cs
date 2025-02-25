using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class OctreeFlyer : MonoBehaviour
    {

        #region Public Variables

        public float speed;
        public float rotationSpeed;
        public float verticalSpeed;

        public float nosediveAngle;
        public float rollAngle;

        public float nosediveTransitionTime = 0.3f;
        public float rollTransitionTime = 0.3f;

        public GameObject searchSphere;

        public LineRenderer lineRenderer;

        public OctreeSystem system;

        #endregion

        #region Private Variables

        private bool nosediveInProgress = false;
        private bool rollInProgress = false;

        private NativeList<uint> searchResults;

        private static readonly ProfilerMarker octreeMarker = new ProfilerMarker("OctreeSearch");

        private Sampler octreeSampler = null;

        #endregion

        public Sampler GetOctreeSampler() => this.octreeSampler;


        private void Start()
        {
            this.searchResults = new NativeList<uint>(Allocator.Persistent);
        }

        private void OnDestroy()
        {
            if (this.searchResults.IsCreated)
            {
                this.searchResults.Dispose();
            }
        }

        private IEnumerator RollStart(bool left)
        {
            if (!this.rollInProgress)
            {
                this.rollInProgress = true;

                var localEuler = this.transform.localEulerAngles;
                float startAngle = localEuler.z;
                float targetAngle = left ? this.rollAngle : 360.0f - this.rollAngle;

                float totalChange = Mathf.DeltaAngle(startAngle, targetAngle);

                float timer = 0.0f;
                while (timer < this.rollTransitionTime)
                {
                    float percent = timer / this.rollTransitionTime;
                    float currentAngle = startAngle + totalChange * percent;

                    localEuler = this.transform.localEulerAngles;
                    localEuler.z = currentAngle;
                    this.transform.localEulerAngles = localEuler;

                    yield return null;

                    timer += Time.deltaTime;
                }

                this.rollInProgress = false;
            }
        }


        private IEnumerator RollEnd()
        {
            if (!this.rollInProgress)
            {
                this.rollInProgress = true;

                var localEuler = this.transform.localEulerAngles;
                float startAngle = localEuler.z;
                float targetAngle = 0.0f;

                float totalChange = Mathf.DeltaAngle(startAngle, targetAngle);

                float timer = 0.0f;
                while (timer < this.rollTransitionTime)
                {
                    float percent = timer / this.rollTransitionTime;
                    float currentAngle = startAngle + totalChange * percent;

                    localEuler = this.transform.localEulerAngles;
                    localEuler.z = currentAngle;
                    this.transform.localEulerAngles = localEuler;

                    yield return null;

                    timer += Time.deltaTime;
                }

                this.rollInProgress = false;
            }
        }

        private IEnumerator NoseDiveStart(bool upwards)
        {
            if (!this.nosediveInProgress)
            {
                this.nosediveInProgress = true;

                var localEuler = this.transform.localEulerAngles;
                float startAngle = localEuler.x;
                float targetAngle = upwards ? this.nosediveAngle : 360.0f - this.nosediveAngle;

                float totalChange = Mathf.DeltaAngle(startAngle, targetAngle);

                float timer = 0.0f;
                while (timer < this.nosediveTransitionTime)
                {
                    float percent = timer / this.nosediveTransitionTime;
                    float currentAngle = startAngle + totalChange * percent;

                    localEuler = this.transform.localEulerAngles;
                    localEuler.x = currentAngle;
                    this.transform.localEulerAngles = localEuler;

                    yield return null;

                    timer += Time.deltaTime;

                }

                this.nosediveInProgress = false;
            }
        }

        private IEnumerator NoseDiveEnd()
        {
            if (!this.nosediveInProgress)
            {
                this.nosediveInProgress = true;

                var localEuler = this.transform.localEulerAngles;
                float startAngle = localEuler.x;
                float targetAngle = 0.0f;

                float totalChange = Mathf.DeltaAngle(startAngle, targetAngle);

                float timer = 0.0f;
                while (timer < this.nosediveTransitionTime)
                {
                    float percent = timer / this.nosediveTransitionTime;
                    float currentAngle = startAngle + totalChange * percent;

                    localEuler = this.transform.localEulerAngles;
                    localEuler.x = currentAngle;
                    this.transform.localEulerAngles = localEuler;

                    yield return null;

                    timer += Time.deltaTime;

                }

                this.nosediveInProgress = false;
            }
        }

        void Update()
        {

            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            var euler = this.transform.localEulerAngles;
            euler.y += horizontal * this.rotationSpeed * Time.deltaTime;
            this.transform.localEulerAngles = euler;

            if (vertical != 0.0f)
            {
                this.StartCoroutine(this.NoseDiveStart(vertical > 0.0f));
            }
            else
            {
                this.StartCoroutine(this.NoseDiveEnd());
            }

            if (horizontal != 0.0f)
            {
                this.StartCoroutine(this.RollStart(horizontal < 0.0f));
            }
            else
            {
                this.StartCoroutine(this.RollEnd());
            }

            //The old -- should the up-down controls be inverted -- question ^^
            this.transform.position -= Vector3.up * vertical * this.verticalSpeed * Time.deltaTime;
            this.transform.position += this.transform.forward * this.speed * Time.deltaTime;

            var wrapBounds = system.wrapAroundBounds;
            var wrapMin = wrapBounds.min;
            var wrapMax = wrapBounds.max;


            Vector3 wrappedPosition = this.transform.position;

            if (wrappedPosition.x < wrapMin.x)
            {
                wrappedPosition.x = wrapMax.x;
            }
            if (wrappedPosition.x > wrapMax.x)
            {
                wrappedPosition.x = wrapMin.x;
            }

            if (wrappedPosition.y < wrapMin.y)
            {
                wrappedPosition.y = wrapMax.y;
            }
            if (wrappedPosition.y > wrapMax.y)
            {
                wrappedPosition.y = wrapMin.y;
            }

            if (wrappedPosition.z < wrapMin.z)
            {
                wrappedPosition.z = wrapMax.z;
            }
            if (wrappedPosition.z > wrapMax.z)
            {
                wrappedPosition.z = wrapMin.z;
            }

            this.transform.position = wrappedPosition;

            var octree = this.system.Octree;
            var data = octree.GetDataBuckets();
            var diamonds = this.system.Diamonds;

            this.searchResults.Clear();
            octreeMarker.Begin();
            var job = octree.GetCellsInRadius(this.transform.position, this.system.searchRadius, ref this.searchResults);
            job.Complete();
            octreeMarker.End();

            List<Vector3> linePositions = new List<Vector3>();

            for (int i = 0; i < this.searchResults.Length; i++)
            {
                uint cell = this.searchResults[i];
                if (data.TryGetFirstValue(cell, out int diamond, out var it))
                {
                    if (Vector3.Distance(this.transform.position, diamonds[diamond].transform.position) < this.system.searchRadius)
                    {
                        linePositions.Add(this.transform.position);
                        linePositions.Add(diamonds[diamond].transform.position);
                    }

                    while (data.TryGetNextValue(out diamond, ref it))
                    {
                        if (Vector3.Distance(this.transform.position, diamonds[diamond].transform.position) < this.system.searchRadius)
                        {
                            linePositions.Add(this.transform.position);
                            linePositions.Add(diamonds[diamond].transform.position);
                        }

                    }
                }
            }

            this.lineRenderer.positionCount = linePositions.Count;
            this.lineRenderer.SetPositions(linePositions.ToArray());

            this.searchSphere.transform.localScale = new Vector3(this.system.searchRadius, this.system.searchRadius, this.system.searchRadius) * 2.0f;

            if (this.octreeSampler == null || !this.octreeSampler.isValid)
            {
                this.octreeSampler = Sampler.Get("OctreeSearch");
            }
        }
    }
}