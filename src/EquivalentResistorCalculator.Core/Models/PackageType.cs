using System.Text.Json.Serialization;

namespace EquivalentResistorCalculator.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<PackageType>))]
public enum PackageType
{
    ThroughHole,
    SMD,
}
