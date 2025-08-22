using System.Text.Json.Serialization;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;

namespace VartFanSkaViLuncha.Web;

public sealed record Location(Coordinates Coordinates, Tags? Tags);

public sealed record Coordinates(double Longitude, double Latitude);

public sealed record Tags(string? Name, string? OpeningHours, Amenity? Amenity);

[JsonConverter(typeof(JsonStringEnumConverter<Amenity>))]
public enum Amenity
{
    Restaurant,

    Cafe,

    FastFood,

    Pub,

    Bar,

    IceCream,

    FoodCourt,
}

[JsonSerializable(typeof(Location))]
[JsonSerializable(typeof(Location[]))]
[JsonSerializable(typeof(FusionCacheDistributedEntry<Location[]>))]
public sealed partial class LocationSerializerContext : JsonSerializerContext;
