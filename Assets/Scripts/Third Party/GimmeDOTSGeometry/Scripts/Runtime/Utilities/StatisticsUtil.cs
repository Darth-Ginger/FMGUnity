using Unity.Collections;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class StatisticsUtil
    {

        /// <summary>
        /// Calculates a regression line (based on least-squares). The slope and intercept factors
        /// are directly interpreted as a Line2D, ready to be used in geometry algorithms
        /// </summary>
        /// <param name="average"></param>
        /// <param name="values"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static Line2D EstimateRegressionLine2D(float2 average, NativeArray<float2> values, float epsilon = 10e-5f)
        {
            float2 sum = float2.zero;
            float numerator = 0.0f;
            float denominator = 0.0f;

            for(int i = 0; i < values.Length; i++)
            {
                float2 diff = values[i] - average;
                numerator = math.mad(diff.x, diff.y, numerator);
                denominator = math.mad(diff.x, diff.x, denominator);

                sum += values[i];
            }

            //To avoid division by zero
            if (math.abs(denominator) < epsilon)
            {
                float2 point = average;
                float2 direction = new float2(0.0f, 1.0f);

                return new Line2D(point, direction);

            } else {

                float slope = numerator / denominator;
                float intercept = (sum.y - slope * sum.x) / (float)values.Length;

                float2 point = new float2(0.0f, intercept);
                float2 direction = math.normalize(new float2(1.0f, slope));

                return new Line2D(point, direction);
            }
        }



    }
}
