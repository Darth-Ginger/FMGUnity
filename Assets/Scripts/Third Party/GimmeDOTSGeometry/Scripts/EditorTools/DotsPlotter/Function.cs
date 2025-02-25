using System;
using UnityEngine;

namespace GimmeDOTSGeometry.Tools.DotsPlotter
{
    public class Function 
    {
        public Color color;

        public Func<float, float> expression;

        public Function(Func<float, float> expression)
        {
            this.expression = expression;
        }

        public float Evaluate(float x)
        {
            return this.expression(x);
        }
    }
}
