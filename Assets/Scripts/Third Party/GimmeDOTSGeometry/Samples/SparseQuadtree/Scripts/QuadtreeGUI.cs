
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class QuadtreeGUI : SystemGUI
    {
        public QuadtreeSystem system;
        public QuadtreeWalker walker;


        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 260);
            GUI.Box(areaRect, "");
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Quadtree GUI");

            GUILayout.Label($"Current Lightning Rods: {this.system.Rods.Count}", this.textStyle);

            if (this.walker.GetQuadtreeSampler() != null)
            {
                var sampler = this.walker.GetQuadtreeSampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null)
                {
                    GUILayout.Label($"Radius Search (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }


            GUILayout.Label("Search Radius");
            this.system.searchRadius = GUILayout.HorizontalSlider(this.system.searchRadius, 3.0f, 25.0f);

            if (this.GUIButton("Add 10 Rods"))
            {
                this.system.AddLightningRods(10);
            }

            if (this.GUIButton("Add 100 Rods"))
            {
                this.system.AddLightningRods(100);
            }

            if(this.GUIButton("Move 10 Rods"))
            {
                this.system.MoveRandomLightningRods(10);
            }

            if(this.GUIButton("Move 100 Rods"))
            {
                this.system.MoveRandomLightningRods(100);
            }

            GUILayout.EndArea();
        }

    }
}