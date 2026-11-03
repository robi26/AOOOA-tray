namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// A half-open time interval <c>[Start, End)</c>.
/// </summary>
/// <param name="Start">Inclusive start.</param>
/// <param name="End">Exclusive end.</param>
public readonly record struct TimeRange(DateTimeOffset Start, DateTimeOffset End)
{
    /// <summary>Gets a value indicating whether the range has no positive length.</summary>
    public bool IsEmpty => this.End <= this.Start;

    /// <summary>Returns true when this range and <paramref name="other"/> share at least one instant.</summary>
    /// <param name="other">The other range.</param>
    /// <returns>True when overlapping.</returns>
    public bool Overlaps(TimeRange other) => this.Start < other.End && other.Start < this.End;
}
