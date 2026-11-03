namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// Merges "Out of office" calendar events into absence blocks.
/// </summary>
public static class AbsenceBlockCalculator
{
    /// <summary>
    /// Merges events into blocks. Two neighbouring events belong to the same block when they overlap or touch,
    /// or when the gap between them contains no working time (for example a night or a weekend).
    /// </summary>
    /// <param name="oofEvents">The "Out of office" events, in any order.</param>
    /// <param name="workingHours">The working hours used to decide whether a gap breaks a block.</param>
    /// <returns>The blocks, ordered by start.</returns>
    public static IReadOnlyList<AbsenceBlock> MergeBlocks(IEnumerable<TimeRange> oofEvents, WorkingHours workingHours)
    {
        ArgumentNullException.ThrowIfNull(oofEvents);
        ArgumentNullException.ThrowIfNull(workingHours);

        var merged = new List<TimeRange>();
        foreach (var current in oofEvents.Where(e => !e.IsEmpty).OrderBy(e => e.Start))
        {
            if (merged.Count > 0)
            {
                var last = merged[^1];
                if (current.Start <= last.End || !workingHours.ContainsWorkingTime(new TimeRange(last.End, current.Start)))
                {
                    merged[^1] = last with { End = current.End > last.End ? current.End : last.End };
                    continue;
                }
            }

            merged.Add(current);
        }

        return merged
            .Select(r => new AbsenceBlock(r.Start, r.End, workingHours.NextWorkingStart(r.End)))
            .ToList();
    }

    /// <summary>
    /// Returns the block the automatic reply should cover: the first block that is not over yet
    /// (its <see cref="AbsenceBlock.ReturnAt"/> is after <paramref name="now"/>). This is either the current absence
    /// or the next upcoming one. Returns null when there is none.
    /// </summary>
    /// <param name="oofEvents">The "Out of office" events.</param>
    /// <param name="workingHours">The working hours.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The relevant block, or null.</returns>
    public static AbsenceBlock? Calculate(IEnumerable<TimeRange> oofEvents, WorkingHours workingHours, DateTimeOffset now) =>
        MergeBlocks(oofEvents, workingHours).FirstOrDefault(b => b.ReturnAt > now);
}
