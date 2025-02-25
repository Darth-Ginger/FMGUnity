using System.Collections;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class Structure : MonoBehaviour
    {
        public float lightBlendTime = 0.5f;
        public float lightRange = 2.0f;

        public Light structureLight;

        public ParticleSystem[] particles;

        private bool started = false;

        private MaterialPropertyBlock mpb;

        private MeshRenderer structureRenderer;

        private void Awake()
        {
            this.structureRenderer = this.GetComponentInChildren<MeshRenderer>();

            this.mpb = new MaterialPropertyBlock();
            this.structureRenderer.SetPropertyBlock(this.mpb);
        }

        public void StartFX()
        {
            if (!this.started)
            {
                this.started = true;
                for (int i = 0; i < this.particles.Length; i++)
                {
                    this.particles[i].Play();
                }

                this.StartCoroutine(this.BlendLight());
                this.StartCoroutine(this.FadeInStructure());
            }
        }

        private IEnumerator FadeInStructure()
        {
            float timer = 0.0f;

            var col = this.structureRenderer.sharedMaterial.GetColor("_Color");
            col.a = 0.0f;

            while (timer < this.lightBlendTime)
            {
                float percent = timer / this.lightBlendTime;
                col.a = percent;

                this.mpb.SetColor("_Color", col);
                this.structureRenderer.SetPropertyBlock(this.mpb);

                yield return null;

                timer += Time.deltaTime;
            }

            col.a = 1.0f;
            this.mpb.SetColor("_Color", col);
            this.structureRenderer.SetPropertyBlock(this.mpb);
        }

        private IEnumerator BlendLight()
        {
            float timer = 0.0f;
            float range = 0.0f;
            this.structureLight.range = range;

            while (timer < this.lightBlendTime)
            {
                float percent = timer / this.lightBlendTime;
                this.structureLight.range = percent * this.lightRange;

                yield return null;

                timer += Time.deltaTime;
            }

            this.structureLight.range = this.lightRange;
        }

    }
}