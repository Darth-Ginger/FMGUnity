using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class AllRectangleQuerySystemGUI : SystemGUI
    {
        public AllRectangleQuerySystem querySystem;


        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 400);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("All Rectangle Query System GUI");
            GUILayout.Label($"Current Points: {this.querySystem.GetNrOfPoints()}", this.textStyle);

            if (this.querySystem.GetAllRectangleQuerySampler() != null)
            {
                var sampler = this.querySystem.GetAllRectangleQuerySampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null)
                {
                    GUILayout.Label($"All Rectangle Query (ms) {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            var allRect = this.querySystem.allRectangle;
            float width = allRect.width;
            float height = allRect.height;

            width = GUILayout.HorizontalSlider(width, 0.02f, 1.0f);
            height = GUILayout.HorizontalSlider(height, 0.02f, 1.0f);

            if (width != allRect.width || height != allRect.height)
            {
                allRect.width = width;
                allRect.height = height;
                allRect.xMin = -width * 0.5f;
                allRect.yMin = -height * 0.5f;

                this.querySystem.SetAllRectangle(allRect);
            }

            if(this.querySystem.IsUsingParallelQuery())
            {
                if (this.GUIButton("Single-threaded Query"))
                {
                    this.querySystem.EnableParallelQuery(false);
                }

                GUILayout.Label($"Batches {this.querySystem.GetBatches()}", this.textStyle);
                int batches = (int)GUILayout.HorizontalSlider((float)this.querySystem.GetBatches(), 16, 8192);
                this.querySystem.SetBatches(batches);

                if(this.querySystem.IsUsingPresortedQueue())
                {
                    if(this.GUIButton("Stop Using Presorted Queue"))
                    {
                        this.querySystem.EnablePresortedQueue(false);
                    }
                } else
                {
                    if(this.GUIButton("Use Presorted Queue"))
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

            if (this.GUIButton("Add 10 Points"))
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
