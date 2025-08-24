using System;
using System.Text.Json.Serialization;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;

namespace VartFanSkaViLuncha.Web;

public sealed record Location(string Name, Coordinates Coordinates, Amenity Amenity, Uri? Url);

public sealed record Coordinates(double Longitude, double Latitude);

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
