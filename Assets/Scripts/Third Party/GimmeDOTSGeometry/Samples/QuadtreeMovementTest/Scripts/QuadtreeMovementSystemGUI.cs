using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class QuadtreeMovementSystemGUI : SystemGUI
    {

        public QuadtreeMovementSystem quadtreeSystem;

        private float searchRadiusPercentage = 0.0f;
        private float movingPercentage = 0.0f;

        private int currentQuadtreeDepth = 0;

        private void Start()
        {
            this.currentQuadtreeDepth = this.quadtreeSystem.initialQuadtreeDepth;
            this.searchRadiusPercentage = this.quadtreeSystem.initialSearchRadiusPercentage;
            this.movingPercentage = this.quadtreeSystem.initialMovingPercentage;
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 400);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Quadtree Movement GUI");
            GUILayout.Label($"Current Points: {this.quadtreeSystem.GetNrOfPoints()}", this.textStyle);

            float totalMS = 0.0f;
            if(this.quadtreeSystem.GetRadiusQuerySampler() != null)
            {
                var sampler = this.quadtreeSystem.GetRadiusQuerySampler();
                var recorder = sampler.GetRecorder();
                if(recorder != null)
                {
                    GUILayout.Label($"Radius Query (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                    totalMS += recorder.elapsedNanoseconds / 10e5f;
                }
            }

            if(this.quadtreeSystem.GetUpdateQuadtreeSampler() != null)
            {
                var sampler = this.quadtreeSystem.GetUpdateQuadtreeSampler();
                var recorder = sampler.GetRecorder();
                if(recorder != null)
                {
                    GUILayout.Label($"Position Update (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                    totalMS += recorder.elapsedNanoseconds / 10e5f;
                }
            }

            GUILayout.Label($"Total Time (ms): {totalMS}", this.textStyle);

            GUILayout.Label($"Quadtree Depth {this.currentQuadtreeDepth}");

            this.currentQuadtreeDepth = (int)GUILayout.HorizontalSlider(this.currentQuadtreeDepth, 1, 8);
            if (this.currentQuadtreeDepth != this.quadtreeSystem.CurrentSearchDepth)
            {
                this.quadtreeSystem.CurrentSearchDepth = this.currentQuadtreeDepth;
            }


            GUILayout.Label($"Search Radius Percentage: {this.searchRadiusPercentage}");
            this.searchRadiusPercentage = GUILayout.HorizontalSlider(this.searchRadiusPercentage, 0.0f, 1.0f);
            this.quadtreeSystem.CurrentSearchPercentage = this.searchRadiusPercentage;

            if (!this.quadtreeSystem.IsDoingMultiQueries())
            {
                if (this.GUIButton("Do Multi-Query?"))
                {
                    this.quadtreeSystem.EnableMultiQuery(true);
                }
            } else
            {
                if(this.GUIButton("Do Mono-Query?"))
                {
                    this.quadtreeSystem.EnableMultiQuery(false);
                }
            }
            

            GUILayout.Label($"Percentage of Moving Objects: {this.movingPercentage}");
            this.movingPercentage = GUILayout.HorizontalSlider(this.movingPercentage, 0.0f, 1.0f);
            this.quadtreeSystem.CurrentMovingPercentage = this.movingPercentage;

            if(this.GUIButton("Add 1000 points"))
            {
                this.quadtreeSystem.AddPoints(1000);
                
            }

            if(this.quadtreeSystem.IsUsingSparseQuadtree())
            {
                if(this.GUIButton("Use Dense Quadtree"))
                {
                    this.quadtreeSystem.ExchangeQuadtreeModel();
                }
            } else
            {
                if(this.GUIButton("Use Sparse Quadtree"))
                {
                    this.quadtreeSystem.ExchangeQuadtreeModel();
                }
            }

            GUILayout.EndArea();
        }

    }
}
