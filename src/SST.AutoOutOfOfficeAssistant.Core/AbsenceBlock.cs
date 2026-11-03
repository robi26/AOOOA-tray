namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// One continuous absence, made of one or more merged "Out of office" calendar events.
/// </summary>
/// <param name="Start">Start of the first event.</param>
/// <param name="End">End of the last event (exclusive).</param>
/// <param name="ReturnAt">First working-time instant at or after <paramref name="End"/>. The automatic reply runs until then.</param>
public sealed record AbsenceBlock(DateTimeOffset Start, DateTimeOffset End, DateTimeOffset ReturnAt);
