using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Stats.Impl.Classification.JenksFisher
{
  public class JenksFisherClassifier
  {
    private readonly List<ValueCountPair> _cumulValues;
    private readonly int _numValues;
    private readonly int _numBreaks;
    private readonly int _bufferSize;
    private double[] _previousSsm;
    private double[] _currentSsm;
    private readonly int[] _classBreaks;
    private int _classBreaksIndex;
    private int _completedRows;

    /// <summary>
    /// Constructor that initializes main variables used in fisher calculation of natural breaks.
    /// </summary>
    /// <param name="vcpc">ordered list of pairs of values to occurrence counts.</param>
    /// <param name="k">number of breaks to find.</param>
    private JenksFisherClassifier(ValueCountPair[] vcpc, int k)
    {
      _cumulValues = new List<ValueCountPair>();
      _numValues = vcpc.Length;
      _numBreaks = k;
      _bufferSize = _numValues - (k - 1);
      _previousSsm = new double[_bufferSize];
      _currentSsm = new double[_bufferSize];
      _classBreaks = new int[_bufferSize * (_numBreaks - 1)];
      _classBreaksIndex = 0;
      _completedRows = 0;

      var cwv = 0.0;
      var cw = 0;

      for (var i = 0; i != _numValues; ++i)
      {
        var currPair = vcpc[i];
        Debug.Assert(i == 0 || currPair.Value >= vcpc[i - 1].Value); // PRECONDITION: the value sequence must be strictly increasing

        var w = currPair.Count;
        Debug.Assert(w > 0); // PRECONDITION: all weights must be positive

        cw += w;
        Debug.Assert(cw >= w); // No overflow? No loss of precision?

        cwv += w * currPair.Value;
        _cumulValues.Add(new ValueCountPair(cwv, cw));

        if (i < _bufferSize)
          _previousSsm[i] = cwv * cwv / cw; // prepare sum of squared means for first class. Last (k-1) values are omitted
      }
    }

    /// <summary>
    /// Does the internal processing to actually create the breaks.
    /// </summary>
    /// <param name="breakCount">number of breaks.</param>
    /// <param name="values">asc ordered input of values and their occurence counts.</param>
    /// <returns>collection of breaks.</returns>
    public static double[] Classify(ValueCountPair[] values, int breakCount)
    {
      var breaksArray = new double[breakCount];
      var m = values.Length;

      Debug.Assert(breakCount <= m); // PRECONDITION

      if (breakCount == 0)
        return breaksArray;

      var jf = new JenksFisherClassifier(values, breakCount);

      if (breakCount > 1)
      {
        // runs the actual calculation
        jf.CalcAll();

        var lastClassBreakIndex = jf.FindMaxBreakIndex(jf._bufferSize - 1, 0, jf._bufferSize);

        while (--breakCount != 0)
        {
          // assign the break values to the result
          breaksArray[breakCount] = values[lastClassBreakIndex + breakCount].Value;

          Debug.Assert(lastClassBreakIndex < jf._bufferSize);

          if (breakCount > 1)
          {
            jf._classBreaksIndex -= jf._bufferSize;
            lastClassBreakIndex = jf._classBreaks[jf._classBreaksIndex + lastClassBreakIndex];
          }
        }

        Debug.Assert(jf._classBreaks[jf._classBreaksIndex] == jf._classBreaks[0]);
      }

      Debug.Assert(breakCount == 0);

      breaksArray[0] = values[0].Value; // break for the first class is the minimum of the dataset.

      return breaksArray;
    }

    /// <summary>
    /// Main entry point for creation of Jenks-Fisher natural breaks.
    /// </summary>
    /// <param name="values">array of the values, do not need to be sorted.</param>
    /// <param name="breakCount">number of breaks to create.</param>
    /// <returns>collection with breaks.</returns>
    public static double[] Classify(double[] values, int breakCount)
    {
      var sortedUniqueValueCounts = GetValueCountPairs(values);
      double[] breaksArray;

      if (sortedUniqueValueCounts.Length > breakCount)
        breaksArray = Classify(sortedUniqueValueCounts, breakCount);
      else
      {
        var i = 0;

        breaksArray = new double[sortedUniqueValueCounts.Length];

        foreach (var vcp in sortedUniqueValueCounts)
        {
          breaksArray[i] = vcp.Value;

          i++;
        }
      }

      var result = new List<double>(breaksArray.Length);

      result.AddRange(breaksArray);

      return result.ToArray();
    }

    /// <summary>
    /// Gets sum of weighs for elements with index b..e.
    /// </summary>
    /// <param name="b">index of begin element.</param>
    /// <param name="e">index of end element.</param>
    /// <returns>sum of weights.</returns>
    private int GetSumOfWeights(int b, int e)
    {
      Debug.Assert(b != 0);    // First element always belongs to class 0, thus queries should never include it.
      Debug.Assert(b <= e);
      Debug.Assert(e < _numValues);

      var res = _cumulValues[e].Count;

      res -= _cumulValues[b - 1].Count;

      return res;
    }

    /// <summary>
    /// Gets sum of weighed values for elements with index b..e.
    /// </summary>
    /// <param name="b">index of begin element.</param>
    /// <param name="e">index of end element.</param>
    /// <returns>the cumul. sum of the values*weight.</returns>
    private double GetSumOfWeightedValues(int b, int e)
    {
      Debug.Assert(b != 0);
      Debug.Assert(b <= e);
      Debug.Assert(e < _numValues);

      var res = _cumulValues[e].Value;

      res -= _cumulValues[b - 1].Value;

      return res;
    }

    /// <summary>
    /// Gets the Squared Mean for elements within index b..e, multiplied by weight. Note that
    /// n*mean^2 = sum^2/n when mean := sum/n.
    /// </summary>
    /// <param name="b">index of begin element.</param>
    /// <param name="e">index of end element.</param>
    /// <returns>the sum of squared mean.</returns>
    private double GetSsm(int b, int e)
    {
      var res = GetSumOfWeightedValues(b, e);

      return res * res / GetSumOfWeights(b, e);
    }

    /// <summary>
    /// <para>
    /// Finds CB[i+completedRows] given that the result is at least
    /// bp+(completedRows-1) and less than ep+(completedRows-1).
    /// </para>
    /// <para>
    /// Complexity: O(ep-bp) &lt;= O(m).
    /// </para>
    /// </summary>
    /// <param name="i">startIndex.</param>
    /// <param name="bp">endindex.</param>
    /// <param name="ep"></param>
    /// <returns>The index.</returns>
    private int FindMaxBreakIndex(int i, int bp, int ep)
    {
      Debug.Assert(bp < ep);
      Debug.Assert(bp <= i);
      Debug.Assert(ep <= i + 1);
      Debug.Assert(i < _bufferSize);
      Debug.Assert(ep <= _bufferSize);

      var minSsm = _previousSsm[bp] + GetSsm(bp + _completedRows, i + _completedRows);
      var foundP = bp;

      while (++bp < ep)
      {
        var currSsm = _previousSsm[bp] + GetSsm(bp + _completedRows, i + _completedRows);

        if (currSsm > minSsm)
        {
          minSsm = currSsm;

          foundP = bp;
        }
      }

      _currentSsm[i] = minSsm;

      return foundP;
    }

    /// <summary>
    /// <para>
    /// Find CB[i+completedRows] for all i&gt;=bi and i&lt;ei given that the
    /// results are at least bp+(completedRows-1) and less than ep+(completedRows-1).
    /// </para>
    /// <para>
    /// Complexity: O(log(ei-bi)*Max((ei-bi),(ep-bp)))&lt;= O(m*log(m)).
    /// </para>
    /// </summary>
    private void CalcRange(int bi, int ei, int bp, int ep)
    {
      Debug.Assert(bi <= ei);
      Debug.Assert(ep <= ei);
      Debug.Assert(bp <= bi);

      if (bi == ei)
        return;

      Debug.Assert(bp < ep);

      var mi = (int)Math.Floor((bi + ei) / 2.0);
      var mp = FindMaxBreakIndex(mi, bp, Math.Min(ep, mi + 1));

      Debug.Assert(bp <= mp);
      Debug.Assert(mp < ep);
      Debug.Assert(mp <= mi);

      // solve first half of the sub-problems with lower 'half' of possible outcomes
      CalcRange(bi, mi, bp, Math.Min(mi, mp + 1));

      _classBreaks[_classBreaksIndex + mi] = mp; // store result for the middle element.

      // solve second half of the sub-problems with upper 'half' of possible outcomes
      CalcRange(mi + 1, ei, mp, ep);
    }

    /// <summary>
    /// Swaps the content of the two lists with each other.
    /// </summary>
    private void SwapArrays()
    {
      var temp = new double[_previousSsm.Length];

      Array.Copy(_previousSsm, temp, _previousSsm.Length);

      _previousSsm = new double[_currentSsm.Length];

      Array.Copy(_currentSsm, _previousSsm, _currentSsm.Length);

      _currentSsm = new double[temp.Length];

      Array.Copy(temp, _currentSsm, temp.Length);
    }

    /// <summary>
    /// <para>Starting point of calculation of breaks.</para>
    /// <para>Complexity: O(m*log(m)*k).</para>
    /// </summary>
    private void CalcAll()
    {
      if (_numBreaks >= 2)
      {
        _classBreaksIndex = 0;
        for (_completedRows = 1; _completedRows < _numBreaks - 1; ++_completedRows)
        {
          CalcRange(0, _bufferSize, 0, _bufferSize); // complexity: O(m*log(m))

          SwapArrays();
          _classBreaksIndex += _bufferSize;
        }
      }
    }

    /// <summary>
    /// Calculates the occurence count of given values and returns them.
    /// </summary>
    /// <param name="values"></param>
    /// <returns>Occurences of values.</returns>
    private static ValueCountPair[] GetValueCountPairs(IReadOnlyCollection<double> values)
    {
      var result = new List<ValueCountPair>();
      var vcpMap = new Dictionary<double, ValueCountPair>();

      foreach (var value in values)
      {
        if (!vcpMap.ContainsKey(value))
        {
          var vcp = new ValueCountPair(value, 1);

          vcpMap.Add(value, vcp);
          result.Add(vcp);
        }
        else
          vcpMap[value].Count++;
      }

      return result.OrderBy(o => o.Value).ToArray();
    }
  }
}
