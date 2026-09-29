using System.Text.Json;
using System.Text.Json.Serialization;
using LnShelf.Shared;

namespace LnShelf.Api.Tests.Contracts;

/// <summary>
/// Contract enums travel by name only. An integer used to deserialize into a value no
/// member names and fail much later, as a 500, inside whichever switch met it first.
/// </summary>
public sealed class StrictStringEnumConverterTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("""{"status":7}""")]
    [InlineData("""{"status":2}""")]
    [InlineData("""{"status":"7"}""")]
    [InlineData("""{"status":"Finished"}""")]
    public void Deserialize_AnythingButAMemberName_IsRefused(string json)
    {
        var act = () => JsonSerializer.Deserialize<SetVolumeReadRequest>(json, WebOptions);

        // JsonException is what model binding turns into a 400.
        act.ShouldThrow<JsonException>();
    }

    [Fact]
    public void Deserialize_AMemberName_Binds() =>
        JsonSerializer.Deserialize<SetVolumeReadRequest>("""{"status":"Read"}""", WebOptions)
            .ShouldNotBeNull().Status.ShouldBe(ReadStatus.Read);

    [Fact]
    public void Serialize_WritesTheMemberName() =>
        JsonSerializer.Serialize(new SetVolumeReadRequest { Status = ReadStatus.Reading }, WebOptions)
            .ShouldBe("""{"status":"Reading"}""");

    /// <summary>
    /// Every contract enum, not only the one the defect was found on: an attribute left on
    /// the framework converter is the same hole reopened.
    /// </summary>
    [Fact]
    public void EveryContractEnum_UsesTheStrictConverter()
    {
        var contractEnums = typeof(StrictStringEnumConverter<>).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.IsPublic)
            .ToList();

        contractEnums.ShouldNotBeEmpty();
        contractEnums.ShouldAllBe(type =>
            type.GetCustomAttributes(typeof(JsonConverterAttribute), false)
                .Cast<JsonConverterAttribute>()
                .Any(attribute => attribute.ConverterType != null
                                  && attribute.ConverterType.IsGenericType
                                  && attribute.ConverterType.GetGenericTypeDefinition() == typeof(StrictStringEnumConverter<>)));
    }
}
