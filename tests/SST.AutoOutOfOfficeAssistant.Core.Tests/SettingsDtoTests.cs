using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Core.Contracts;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class SettingsDtoTests
{
    [Test]
    public async Task Validate_Defaults_AreValid()
    {
        await Assert.That(new SettingsDto().Validate()).IsEmpty();
    }

    [Test]
    public async Task Validate_EmptyInternalTemplate_IsInvalid()
    {
        var errors = new SettingsDto { InternalTemplate = " " }.Validate();

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Validate_EmptyExternalTemplateWithoutExternalAudience_IsValid()
    {
        var errors = new SettingsDto { ExternalTemplate = string.Empty, ExternalAudience = ExternalAudience.None }.Validate();

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task Validate_TooLongTemplate_IsInvalid()
    {
        var errors = new SettingsDto { InternalTemplate = new string('x', SettingsDto.MaxTemplateLength + 1) }.Validate();

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments("xx-XX")]
    [Arguments("")]
    public async Task Validate_UnsupportedCulture_IsInvalid(string culture)
    {
        var errors = new SettingsDto { Culture = culture }.Validate();

        await Assert.That(errors).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Validate_UndefinedAudience_IsInvalid()
    {
        var errors = new SettingsDto { ExternalAudience = (ExternalAudience)42 }.Validate();

        await Assert.That(errors).IsNotEmpty();
    }

    [Test]
    public async Task ToSettings_NormalisesCultureCasing()
    {
        var settings = new SettingsDto { Culture = "EN-gb" }.ToSettings("5d8c1a3e-2f4b-4c6d-9e7f-0a1b2c3d4e5f");

        await Assert.That(settings.Culture).IsEqualTo("en-GB");
    }
}
