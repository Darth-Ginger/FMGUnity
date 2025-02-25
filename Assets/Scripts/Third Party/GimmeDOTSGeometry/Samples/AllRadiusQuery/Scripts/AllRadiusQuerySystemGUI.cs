using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class AllRadiusQuerySystemGUI : SystemGUI
    {
        public AllRadiusQuerySystem querySystem;

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 400);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("All Radius Query System GUI");
            GUILayout.Label($"Current Points: {this.querySystem.GetNrOfPoints()}", this.textStyle);

            if(this.querySystem.GetAllRadiusQuerySampler() != null)
            {
                var sampler = this.querySystem.GetAllRadiusQuerySampler();
                var recorder = sampler.GetRecorder();
                if(recorder != null)
                {
                    GUILayout.Label($"All Radius Query (ms) {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            float allRadius = this.querySystem.allRadius;
            this.querySystem.allRadius = GUILayout.HorizontalSlider(this.querySystem.allRadius, 0.01f, 0.5f);
            if(allRadius != this.querySystem.allRadius)
            {
                this.querySystem.SetAllRadius(this.querySystem.allRadius);
            }

            if(this.querySystem.IsUsingParallelQuery())
            {
                if(this.GUIButton("Single-threaded Query")) 
                {
                    this.querySystem.EnableParallelQuery(false);
                }

                GUILayout.Label($"Batches {this.querySystem.GetBatches()}", this.textStyle);
                int batches = (int)GUILayout.HorizontalSlider((float)this.querySystem.GetBatches(), 16, 1024);
                this.querySystem.SetBatches(batches);

                if (this.querySystem.IsUsingPresortedQueue())
                {
                    if (this.GUIButton("Stop Using Presorted Queue"))
                    {
                        this.querySystem.EnablePresortedQueue(false);
                    }
                }
                else
                {
                    if (this.GUIButton("Use Presorted Queue"))
                    {
                        this.querySystem.EnablePresortedQueue(true);
                    }
                }

            } else
            {
                if(this.GUIButton("Parallel Query"))
                {
                    this.querySystem.EnableParallelQuery(true);
                }
            }



            if(this.GUIButton("Add 10 Points"))
            {
                this.querySystem.AddPoints(10);
            }

            if (this.GUIButton("Add 100 Points"))
            {
                this.querySystem.AddPoints(100);
            }

            if (this.GUIButton("Add 1000 Points"))
            {
                this.querySystem.AddPoints(1000);
            }

            GUILayout.EndArea();
        }
    }
}
