using GimmeDOTSGeometry.Tools.DotsPlotter;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace GimmeDOTSGeometry
{
    public class ParabolaIntersectionTest
    {

        private void Setup()
        {
            var cam = RuntimeTestUtility.CreateCamera();
            cam.transform.position = Vector3.up * 7.0f;

            RuntimeTestUtility.CreateDirectionalLight();
        }

        [UnityTest]
        public IEnumerator SimpleIntersection()
        {
            this.Setup();

            //I'll never forget you, TI-83... although you were very BASIC hehe
            /*:*/ var plotter = new Plotter(new Rect(-5, -5, 10, 10));
            /*:*/
            /*:*/ var parA = new Parabola(1.0f, 0.0f, 0.0f);
            /*:*/ var parB = new Parabola(1.0f, 1.0f, 1.0f);
            /*:*/
            //Hopefully I do not run out of program memory hehe

            plotter.AddFunction(new Function(parA.Evaluate)
            {
                color = Color.green * 0.75f,
            });

            plotter.AddFunction(new Function(parB.Evaluate)
            {
                color = Color.blue * 0.75f
            });

            plotter.SetBackgroundColor(Color.gray * 0.5f);
            plotter.SetAxis(true, Color.white);

            var plotDrawerGO = new GameObject("Plot");
            var drawer = plotDrawerGO.AddComponent<PlotDrawer>();
            drawer.plotter = plotter;
            drawer.ySize = 300.0f;

            if(GraphFunctionIntersections.ParabolaIntersections(parA, parB, out float2 i0, out float2 i1))
            {
                plotter.AddMark(new Mark()
                {
                    color = Color.red,
                    position = i0,
                    size = 5.0f
                });

                plotter.AddMark(new Mark()
                {
                    color = Color.red,
                    position = i1,
                    size = 5.0f
                });
            }

            while(!drawer.finished)
            {
                yield return null;
            }

        }

        [UnityTest]
        public IEnumerator NoIntersection()
        {
            this.Setup();


            var plotter = new Plotter(new Rect(-5, -5, 10, 10));

            var parA = new Parabola(1.0f, 0.0f, 0.5f);
            var parB = new Parabola(-1.0f, 0.0f, -0.5f);

            plotter.AddFunction(new Function(parA.Evaluate)
            {
                color = Color.green * 0.75f,
            });

            plotter.AddFunction(new Function(parB.Evaluate)
            {
                color = Color.blue * 0.75f
            });

            plotter.SetBackgroundColor(Color.gray * 0.5f);
            plotter.SetAxis(true, Color.white);

            var plotDrawerGO = new GameObject("Plot");
            var drawer = plotDrawerGO.AddComponent<PlotDrawer>();
            drawer.plotter = plotter;
            drawer.ySize = 300.0f;

            if (GraphFunctionIntersections.ParabolaIntersections(parA, parB, out float2 i0, out float2 i1))
            {
                plotter.AddMark(new Mark()
                {
                    color = Color.red,
                    position = i0,
                    size = 5.0f
                });

                plotter.AddMark(new Mark()
                {
                    color = Color.red,
                    position = i1,
                    size = 5.0f
                });
            }

            while (!drawer.finished)
            {
                yield return null;
            }

        }


        [UnityTest]
        public IEnumerator TwoIntersections()
        {
            this.Setup();

            var plotter = new Plotter(new Rect(-5, -5, 10, 10));

            var parA = new Parabola(1.0f, 0.0f, 0.5f);
            var parB = new Parabola(-1.0f, 0.2f, 1.0f);

            plotter.AddFunction(new Function(parA.Evaluate)
            {
                color = Color.green * 0.75f,
            });

            plotter.AddFunction(new Function(parB.Evaluate)
            {
                color = Color.blue * 0.75f
            });

            plotter.SetBackgroundColor(Color.gray * 0.5f);
            plotter.SetAxis(true, Color.white);

            var plotDrawerGO = new GameObject("Plot");
            var drawer = plotDrawerGO.AddComponent<PlotDrawer>();
            drawer.plotter = plotter;
            drawer.ySize = 300.0f;

            if (GraphFunctionIntersections.ParabolaIntersections(parA, parB, out float2 i0, out float2 i1))
            {
                plotter.AddMark(new Mark()
                {
                    color = Color.red,
                    position = i0,
                    size = 5.0f
                });

                plotter.AddMark(new Mark()
                {
                    color = Color.red,
                    position = i1,
                    size = 5.0f
                });
            }

            while (!drawer.finished)
            {
                yield return null;
            }

        }
    }
}
