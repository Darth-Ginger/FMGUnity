
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class OctreeGUI : SystemGUI
    {

        public OctreeSystem system;
        public OctreeFlyer flyer;


        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 260);
            GUI.Box(areaRect, "");
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Octree GUI");

            GUILayout.Label($"Current Diamonds: {this.system.Diamonds.Count}", this.textStyle);

            if (this.flyer.GetOctreeSampler() != null)
            {
                var sampler = this.flyer.GetOctreeSampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null)
                {
                    GUILayout.Label($"Radius Search (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            GUILayout.Label("Search Radius");
            this.system.searchRadius = GUILayout.HorizontalSlider(this.system.searchRadius, 15.0f, 40.0f);

            if (this.GUIButton("Add 10 Diamonds"))
            {
                this.system.AddDiamonds(10);
            }

            if (this.GUIButton("Add 100 Diamonds"))
            {
                this.system.AddDiamonds(100);
            }

            if(this.GUIButton("Move 10 Diamonds"))
            {
                this.system.MoveDiamonds(10);
            }

            if(this.GUIButton("Move 100 Diamonds"))
            {
                this.system.MoveDiamonds(100);
            }

            GUILayout.EndArea();
        }
    }
}