using SST.AutoOutOfOfficeAssistant.Core;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class UserKeyTests
{
    [Test]
    [Arguments("5D8C1A3E-2F4B-4C6D-9E7F-0A1B2C3D4E5F")]
    [Arguments(" 5d8c1a3e-2f4b-4c6d-9e7f-0a1b2c3d4e5f ")]
    [Arguments("{5d8c1a3e-2f4b-4c6d-9e7f-0a1b2c3d4e5f}")]
    public async Task Normalize_ObjectId_ReturnsLowerCaseGuid(string objectId)
    {
        await Assert.That(UserKey.Normalize(objectId)).IsEqualTo("5d8c1a3e-2f4b-4c6d-9e7f-0a1b2c3d4e5f");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("anna.muster@example.com")]
    [Arguments("00000000-0000-0000-0000-000000000000")]
    public async Task Normalize_NotAnObjectId_ReturnsNull(string? value)
    {
        await Assert.That(UserKey.Normalize(value)).IsNull();
    }
}
