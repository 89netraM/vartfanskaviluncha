using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace VartFanSkaViLuncha.Web;

public sealed record Location(Coordinates Coordinates, IImmutableDictionary<string, string> Tags);

public sealed record Coordinates(double Longitude, double Latitude);

[JsonSerializable(typeof(Location))]
[JsonSerializable(typeof(Location[]))]
public sealed partial class LocationSerializerContext : JsonSerializerContext;
