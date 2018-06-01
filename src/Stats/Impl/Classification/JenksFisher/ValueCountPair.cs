namespace Stats.Impl.Classification.JenksFisher
{
  public class ValueCountPair
  {
    public ValueCountPair(double value, int count)
    {
      Value = value;
      Count = count;
    }

    public double Value { get; set; }

    public int Count { get; set; }
  }
}
