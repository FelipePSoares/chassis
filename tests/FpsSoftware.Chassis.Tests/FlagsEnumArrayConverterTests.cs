using FluentAssertions;
using Newtonsoft.Json;

namespace FpsSoftware.Chassis.Tests;

[Flags]
internal enum Access
{
    None = 0,
    Read = 1,
    Write = 2,
    Delete = 4,
}

public class FlagsEnumArrayConverterTests
{
    private readonly FlagsEnumArrayConverter converter = new();

    [Fact]
    public void CanConvert_ShouldOnlyHandleFlagEnums()
    {
        converter.CanConvert(typeof(Access)).Should().BeTrue();
        converter.CanConvert(typeof(DayOfWeek)).Should().BeFalse();
    }

    [Fact]
    public void WriteJson_ShouldSerializeCombinedFlagsAsArray()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(converter);

        string json = JsonConvert.SerializeObject(Access.Read | Access.Write, settings);

        json.Should().Be("[\"Read\",\"Write\"]");
    }

    [Fact]
    public void WriteJson_ShouldNotIncludeNoneValue()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(converter);

        string json = JsonConvert.SerializeObject(Access.Read, settings);

        json.Should().Be("[\"Read\"]");
    }

    [Fact]
    public void ReadJson_ShouldParseArrayBackIntoCombinedFlags()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(converter);

        var result = JsonConvert.DeserializeObject<Access>("[\"Read\",\"Delete\"]", settings);

        result.Should().Be(Access.Read | Access.Delete);
    }
}
