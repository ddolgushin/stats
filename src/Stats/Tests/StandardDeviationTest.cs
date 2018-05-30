using System;
using Xunit;
using Stats.Impl.Classification.StandardDeviation;

namespace Stats.Tests
{
	public class StandardDeviationTest
	{
		[Fact]
		public void StandardDeviationCalculationResultIsCorrectTest()
		{
			var values = new [] { 14.0, 18, 12, 15, 11, 19, 13, 22 };
			var res = StandardDeviationClassifier.GetStandardDeviation(values);

			Assert.Equal(3.57, Math.Round(res, 2));
		}

		[Fact]
		public void BreaksArrayIsCorrectTest()
		{
			var values = new [] { 79.0, 78, 77, 75, 75, 74, 74, 74, 74, 70 };
			var breaks = StandardDeviationClassifier.Classify(values);
			var expectedValues = new [] { double.MinValue, 67.7750432527246224, 70.1833621684830816, 72.5916810842415408, 77.4083189157584592, 79.8166378315169184, 82.2249567472753776, double.MaxValue };

			Assert.Equal(breaks, expectedValues);
		}
	}
}
