using System;
using System.Linq;

namespace Stats.Impl.Classification.StandardDeviation
{
  public class StandardDeviationClassifier
  {
    public static double[] Classify(double[] values)
    {
      return Classify(values, out _, out _);
    }

    public static double[] Classify(double[] values, out double mean, out double stdDeviation)
    {
      var res = new double[8];
      var orderedValues = values.OrderBy(o => o).ToArray();

      stdDeviation = GetStandardDeviation(orderedValues);
      mean = orderedValues.Average();

      res[0] = double.MinValue;
      res[1] = mean - 3 * stdDeviation;
      res[2] = mean - 2 * stdDeviation;
      res[3] = mean - stdDeviation;
      res[4] = mean + stdDeviation;
      res[5] = mean + 2 * stdDeviation;
      res[6] = mean + 3 * stdDeviation;
      res[7] = double.MaxValue;

      return res;
    }

    internal static double GetStandardDeviation(double[] values)
    {
      var mean = values.Average();
      var variance = values.Select(o => Math.Pow(o - mean, 2)).Sum() / values.Length;

      return Math.Sqrt(variance);
    }
  }
}
