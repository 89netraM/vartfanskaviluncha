using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace VartFanSkaViLuncha.Web.Services;

public sealed class LocationsService(
    ILogger<LocationsService> logger,
    IOptions<LocationsOptions> options,
    HttpClient httpClient,
    IFusionCache cache
)
{
    private readonly Uri overpassUri = options.Value.OverpassUri;
    private readonly string overpassUserAgent = options.Value.OverpassUserAgent;
    private readonly TimeSpan cacheDuration = options.Value.CacheDuration;

    public async Task<Location[]> GetLocationsInAsync(string areaName, CancellationToken cancellationToken) =>
        await cache.GetOrSetAsync(
            areaName,
            ct => FetchLocationsInAsync(areaName, ct),
            cacheDuration,
            cancellationToken
        );

    private async Task<Location[]> FetchLocationsInAsync(string areaName, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, overpassUri)
        {
            Content = new StringContent(
                $$"""
                [out:json][timeout:25];

                relation
                ["boundary"]
                ["name"~"{{areaName}}",i]
                ->.boundaries;

                .boundaries map_to_area ->.searchArea;

                node[amenity~"^(restaurant|cafe|fast_food|pub|bar|ice_cream|food_court)$"](area.searchArea);

                out center;
                """
            ),
            Headers = { { "User-Agent", overpassUserAgent } },
        };
        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new OverpassBadResponseException(
                response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken)
            );
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogDebug("Received the following response from the Overpass API: {Response}", text);

        var overpassResponse =
            await response.Content.ReadFromJsonAsync(
                OverpassSerializerContext.Default.OverpassResponse,
                cancellationToken
            ) ?? throw new OverpassNullResponseException();

        return [.. overpassResponse.Elements.OfType<Node>().Select(Map)];
    }

    private static Location Map(Node node) => new(new(node.Longitude, node.Latitude), Map(node.Tags));

    private static Web.Tags? Map(Tags? tags) =>
        tags is null ? null : new(tags.Name, tags.OpeningHours, Map(tags.Amenity));

    private static Web.Amenity Map(Amenity amenity) =>
        amenity switch
        {
            Amenity.Restaurant => Web.Amenity.Restaurant,
            Amenity.Cafe => Web.Amenity.Cafe,
            Amenity.FastFood => Web.Amenity.FastFood,
            Amenity.Pub => Web.Amenity.Pub,
            Amenity.Bar => Web.Amenity.Bar,
            Amenity.IceCream => Web.Amenity.IceCream,
            Amenity.FoodCourt => Web.Amenity.FoodCourt,
            var unknown => throw new UnknownAmenityException(unknown),
        };
}

public sealed record OverpassResponse([property: JsonPropertyName("elements")] Element[] Elements);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Node), "node")]
public record Element();

public sealed record Node(
    [property: JsonPropertyName("lon"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] double Longitude,
    [property: JsonPropertyName("lat"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] double Latitude,
    [property: JsonPropertyName("tags")] Tags? Tags
) : Element();

public sealed record Tags(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("amenity")] Amenity Amenity,
    [property: JsonPropertyName("opening_hours")] string? OpeningHours
);

[JsonConverter(typeof(JsonStringEnumConverter<Amenity>))]
public enum Amenity
{
    [JsonStringEnumMemberName("restaurant")]
    Restaurant,

    [JsonStringEnumMemberName("cafe")]
    Cafe,

    [JsonStringEnumMemberName("fast_food")]
    FastFood,

    [JsonStringEnumMemberName("pub")]
    Pub,

    [JsonStringEnumMemberName("bar")]
    Bar,

    [JsonStringEnumMemberName("ice_cream")]
    IceCream,

    [JsonStringEnumMemberName("food_court")]
    FoodCourt,
}

file sealed class UnknownAmenityException(Amenity? unknownAmenity) : Exception($"Unknown amenity {unknownAmenity}");

[JsonSerializable(typeof(OverpassResponse))]
public sealed partial class OverpassSerializerContext : JsonSerializerContext;

file sealed class OverpassBadResponseException(HttpStatusCode statusCode, string message)
    : Exception(
        "Received an undesirable response from the Overpass API.",
        innerException: new HttpRequestException(message, inner: null, statusCode)
    );

file sealed class OverpassNullResponseException() : Exception("Received a single null literal from the Overpass API.");

public sealed class LocationsOptions
{
    public Uri OverpassUri { get; set; } = new("https://overpass-api.de/api/interpreter");

    [Required]
    public required string OverpassUserAgent { get; set; }

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);
}

[OptionsValidator]
public sealed partial class LocationsOptionsValidator : IValidateOptions<LocationsOptions>;

public static class LocationsServiceExtensions
{
    public static IServiceCollection AddLocationsService(this IServiceCollection services)
    {
        services.AddOptions<LocationsOptions>().BindConfiguration("Locations").ValidateOnStart();
        services.AddTransient<IValidateOptions<LocationsOptions>, LocationsOptionsValidator>();
        services.AddTransient<LocationsService>();
        return services;
    }
}
