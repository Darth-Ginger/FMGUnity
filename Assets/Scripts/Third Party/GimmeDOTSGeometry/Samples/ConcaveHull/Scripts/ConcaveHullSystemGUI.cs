using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class ConcaveHullSystemGUI : SystemGUI
    {

        public ConcaveHullSystem system;


        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 300);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Concave Hull System GUI");
            GUILayout.Label($"Current Points: {this.system.GetNrOfPoints()}", this.textStyle);

            if(this.system.GetConcaveHullSampler() != null)
            {
                var sampler = this.system.GetConcaveHullSampler();
                var recorder = sampler.GetRecorder();

                if(recorder != null)
                {
                    var modeStr = this.system.mode == ConcaveHullSystem.Mode.CONVEX ? "Convex" : "Concave";
                    GUILayout.Label($"{modeStr} Hull (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            switch(this.system.mode)
            {
                case ConcaveHullSystem.Mode.CONVEX:

                    if(this.GUIButton("Concave Hull"))
                    {
                        this.system.SetMode(ConcaveHullSystem.Mode.CONCAVE);
                    }

                    break;
                case ConcaveHullSystem.Mode.CONCAVE:

                    GUILayout.Label($"Concavity ({this.system.concavity})");
                    this.system.concavity = GUILayout.HorizontalSlider(this.system.concavity, 0.5f, 5.0f);

                    if(this.GUIButton("Convex Hull"))
                    {
                        this.system.SetMode(ConcaveHullSystem.Mode.CONVEX);
                    }

                    break;
            }

            switch (this.system.dataSet)
            {
                case ConcaveHullSystem.DataSet.DOLPHIN:

                    if(this.GUIButton("Tree"))
                    {
                        this.system.SetDataSet(ConcaveHullSystem.DataSet.TREE);
                    }

                    break;
                case ConcaveHullSystem.DataSet.TREE:

                    if(this.GUIButton("Rotation"))
                    {
                        this.system.SetDataSet(ConcaveHullSystem.DataSet.ROTATING);
                    }

                    break;
                case ConcaveHullSystem.DataSet.ROTATING:

                    if(this.GUIButton("Dolphin"))
                    {
                        this.system.SetDataSet(ConcaveHullSystem.DataSet.DOLPHIN);
                    }

                    if(this.GUIButton("Add 10 Points"))
                    {
                        this.system.AddPoints(10);
                    }

                    if(this.GUIButton("Add 100 Points"))
                    {
                        this.system.AddPoints(100);
                    }

                    if(this.GUIButton("Add 1000 Points"))
                    {
                        this.system.AddPoints(1000);
                    }

                    break;

            }

            GUILayout.EndArea();
        }

    }
}
