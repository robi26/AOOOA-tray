namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// Normalises the Entra object ID (<c>oid</c> claim) that identifies a user in storage and in Graph calls.
/// </summary>
/// <remarks>
/// The object ID never changes, unlike the UPN, and Graph accepts it everywhere this app needs it (<c>/users/{id}</c>).
/// </remarks>
public static class UserKey
{
    /// <summary>Normalises the object ID, or returns null when it is not a GUID.</summary>
    /// <param name="objectId">The object ID.</param>
    /// <returns>The lower-case GUID in "D" format, or null.</returns>
    public static string? Normalize(string? objectId) =>
        Guid.TryParse(objectId?.Trim(), out var id) && id != Guid.Empty ? id.ToString("D") : null;
}
