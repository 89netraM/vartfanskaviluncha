using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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

                relation(52822) -> .sweden;
                .sweden map_to_area -> .swedenArea;

                relation["boundary"]
                    ["name"~"{{areaName}}",i]
                        -> .boundaries;

                .boundaries map_to_area ->.searchArea;

                node[amenity~"^(restaurant|cafe|fast_food|pub|bar|ice_cream|food_court)$"]
                    [name]
                    (area.searchArea)
                    (area.swedenArea);

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

        return [.. overpassResponse.Elements.OfType<Node>().Where(IsOpenAtLunch).Select(Map)];
    }

    private static Location Map(Node node) =>
        new(node.Tags.Name, new(node.Longitude, node.Latitude), MapAmenity(node.Tags), MapUrl(node.Tags));

    private static Web.Amenity MapAmenity(Tags tags) =>
        tags.Amenity switch
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

    private static Uri? MapUrl(Tags tags)
    {
        if (tags.ExtensionData is null)
        {
            return null;
        }

        if (TryGetUri(tags.ExtensionData, "website:menu", out var websiteMenu))
        {
            return websiteMenu;
        }

        if (TryGetUri(tags.ExtensionData, "website", out var website))
        {
            return website;
        }

        if (TryGetUri(tags.ExtensionData, "contact:website", out var contactWebsite))
        {
            return contactWebsite;
        }

        if (TryGetUri(tags.ExtensionData, "url", out var url))
        {
            return url;
        }

        return null;

        static bool TryGetUri(
            IReadOnlyDictionary<string, JsonElement> tags,
            string property,
            [NotNullWhen(true)] out Uri? uri
        )
        {
            uri = null;
            return tags.TryGetValue(property, out var element)
                && element.GetString() is string str
                && Uri.TryCreate(str, UriKind.Absolute, out uri);
        }
    }

    private static bool IsOpenAtLunch(Node node) =>
        node.Tags.OpeningHours is null || OpeningHoursParser.OpeningHoursParser.IsOpenAtLunch(node.Tags.OpeningHours);
}

public sealed record OverpassResponse([property: JsonPropertyName("elements")] Element[] Elements);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Node), "node")]
public record Element();

public sealed record Node(
    [property: JsonPropertyName("lon"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] double Longitude,
    [property: JsonPropertyName("lat"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] double Latitude,
    [property: JsonPropertyName("tags")] Tags Tags
) : Element();

public sealed class Tags
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("amenity")]
    public required Amenity Amenity { get; init; }

    [JsonPropertyName("opening_hours")]
    public string? OpeningHours { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

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
