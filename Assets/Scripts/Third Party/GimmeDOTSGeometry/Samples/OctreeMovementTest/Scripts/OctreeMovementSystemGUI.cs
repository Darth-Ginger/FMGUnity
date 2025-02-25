using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class OctreeMovementSystemGUI : SystemGUI
    {

        public OctreeMovementSystem octreeSystem;

        private float searchRadiusPercentage = 0.0f;
        private float movingPercentage = 0.0f;


        private int currentOctreeDepth = 0;

        private void Start()
        {
            this.currentOctreeDepth = this.octreeSystem.initialOctreeDepth;
            this.searchRadiusPercentage = this.octreeSystem.initialSearchRadiusPercentage;
            this.movingPercentage = this.octreeSystem.initialMovingPercentage;
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 550);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Octree Movement GUI");
            GUILayout.Label($"Current Points: {this.octreeSystem.GetNrOfPoints()}", this.textStyle);

            float totalMS = 0.0f;
            if(this.octreeSystem.GetRadiusQuerySampler() != null)
            {
                var sampler = this.octreeSystem.GetRadiusQuerySampler();
                var recorder = sampler.GetRecorder();
                if(recorder != null)
                {
                    GUILayout.Label($"Radius Query (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                    totalMS += recorder.elapsedNanoseconds / 10e5f;
                }
            }

            if(this.octreeSystem.GetUpdateOctreeSampler() != null)
            {
                var sampler = this.octreeSystem.GetUpdateOctreeSampler();
                var recorder = sampler.GetRecorder();
                if(recorder != null)
                {
                    GUILayout.Label($"Position Update (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                    totalMS += recorder.elapsedNanoseconds / 10e5f;
                }
            }

            GUILayout.Label($"Total Time (ms): {totalMS}", this.textStyle);

            GUILayout.Label($"Octree Depth {this.currentOctreeDepth}");

            this.currentOctreeDepth = (int)GUILayout.HorizontalSlider(this.currentOctreeDepth, 1, 7);
            if (this.currentOctreeDepth != this.octreeSystem.CurrentSearchDepth)
            {
                this.octreeSystem.CurrentSearchDepth = this.currentOctreeDepth;
            }

            GUILayout.Label($"Search Radius Percentage: {this.searchRadiusPercentage}");
            this.searchRadiusPercentage = GUILayout.HorizontalSlider(this.searchRadiusPercentage, 0.0f, 1.0f);
            this.octreeSystem.CurrentSearchPercentage = this.searchRadiusPercentage;

            if(!this.octreeSystem.IsDoingMultiQueries())
            {
                if(this.GUIButton("Do Multi-Query?"))
                {
                    this.octreeSystem.EnableMultiQuery(true);
                }

            } else
            {
                if(this.GUIButton("Do Mono-Query?")) {
                    this.octreeSystem.EnableMultiQuery(false);
                }
            }

            GUILayout.Label($"Percentage of Moving Objects: {this.movingPercentage}");
            this.movingPercentage = GUILayout.HorizontalSlider(this.movingPercentage, 0.0f, 1.0f);
            this.octreeSystem.CurrentMovingPercentage = this.movingPercentage;

            if(this.GUIButton("Add 1000 points"))
            {
                this.octreeSystem.AddPoints(1000);
            }

            if(this.octreeSystem.IsUsingSparseOctree())
            {
                if(this.GUIButton("Use Dense Octree"))
                {
                    this.octreeSystem.ExchangeOctreeModel();
                }
            } else
            {
                if(this.GUIButton("Use Sparse Octree"))
                {
                    this.octreeSystem.ExchangeOctreeModel();
                }
            }

            GUILayout.EndArea();
        }

    }
}
