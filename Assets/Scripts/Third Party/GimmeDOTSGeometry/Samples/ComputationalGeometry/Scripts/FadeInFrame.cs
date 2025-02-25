using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GimmeDOTSGeometry.Samples
{
    public class FadeInFrame : MonoBehaviour
    {

        public float fadeDelay;
        public float fadeTime;

        private Image image;

        void Start()
        {
            this.image = this.GetComponent<Image>();
            var color = this.image.color;
            color.a = 0.0f;
            this.image.color = color;

            this.StartCoroutine(this.FadeIn());
        }

        private IEnumerator FadeIn()
        {

            var color = this.image.color;
            color.a = 0.0f;
            this.image.color = color;

            yield return new WaitForSeconds(this.fadeDelay);


            float timer = 0.0f;
            while (timer < this.fadeTime)
            {

                float percent = timer / this.fadeTime;
                color.a = percent;

                this.image.color = color;


                yield return null;

                timer += Time.deltaTime;
            }

            color.a = 1.0f;
            this.image.color = color;
        }
    }
}