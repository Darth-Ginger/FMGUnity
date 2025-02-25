
using System.Collections;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class FadeInMesh : MonoBehaviour
    {

        #region Public Variables

        public float startDelay;
        public float fadeTime;

        #endregion

        #region Private Variables

        private int triangleCount;

        private MeshFilter meshFilter;
        private Mesh mesh;

        #endregion

        private void Awake()
        {
            this.meshFilter = this.GetComponent<MeshFilter>();
            this.mesh = this.meshFilter.sharedMesh;

            this.triangleCount = this.mesh.triangles.Length / 3;

            this.meshFilter.mesh = null;

            this.StartCoroutine(this.FadeIn());
        }

        private Mesh CreateSubMesh(int triangleCount)
        {
            var triangles = this.mesh.triangles;
            var newTriangles = new int[triangleCount * 3];

            for (int i = 0; i < newTriangles.Length; i++)
            {
                newTriangles[i] = triangles[i];
            }

            var mesh = new Mesh()
            {
                vertices = this.mesh.vertices,
                uv = this.mesh.uv,
                uv2 = this.mesh.uv2,
                colors = this.mesh.colors,
                triangles = newTriangles
            };

            return mesh;
        }

        private IEnumerator FadeIn()
        {

            yield return new WaitForSeconds(this.startDelay);

            float timer = 0.0f;

            while (timer < this.fadeTime)
            {
                float percent = timer / this.fadeTime;
                int triangleCount = (int)(this.triangleCount * percent);
                var mesh = this.CreateSubMesh(triangleCount);
                this.meshFilter.mesh = mesh;

                yield return null;
                timer += Time.deltaTime;
            }

            this.meshFilter.mesh = this.mesh;
        }
    }
}