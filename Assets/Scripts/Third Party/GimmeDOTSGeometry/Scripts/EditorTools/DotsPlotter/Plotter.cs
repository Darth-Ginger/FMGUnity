using System.Collections.Generic;
using UnityEngine;

namespace GimmeDOTSGeometry.Tools.DotsPlotter
{
    //Very, very simple plotter for now. Was used for testing parabola-parabola intersections
    //Not part of the main program of the package so to speak, so use with caution
    public class Plotter
    {
        internal bool drawMainAxis;

        internal Color mainAxisColor;
        internal Color backgroundColor;

        internal HashSet<Function> functions;
        internal HashSet<Mark> marks;

        internal int samples = 100;

        internal Rect window;

        public void SetAxis(bool enabled, Color axisColor)
        {
            this.drawMainAxis = enabled;
            this.mainAxisColor = axisColor;
        }

        public Rect GetWindow()
        {
            return this.window;
        }

        public void SetWindow(Rect window)
        {
            this.window = window;
        }

        public void SetBackgroundColor(Color color)
        {
            this.backgroundColor = color;
        }

        public Plotter(Rect window, int samples = 100)
        {
            this.window = window;
            this.functions = new HashSet<Function>();
            this.marks = new HashSet<Mark>();
            this.samples = samples;
        }


        public void AddFunction(Function function)
        {
            if(!this.functions.Contains(function))
            {
                this.functions.Add(function);
            }
        }

        public void RemoveFunction(Function function)
        {
            if(this.functions.Contains(function))
            {
                this.functions.Remove(function);
            }
        }


        public void AddMark(Mark mark)
        {
            if(!this.marks.Contains(mark))
            {
                this.marks.Add(mark);
            } 
        }

        public void RemoveMark(Mark mark)
        {
            if(this.marks.Contains(mark))
            {
                this.marks.Remove(mark);
            }
        }

    }
}
