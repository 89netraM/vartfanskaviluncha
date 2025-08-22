using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VartFanSkaViLuncha.Web;
using VartFanSkaViLuncha.Web.Services;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddUserSecrets(Assembly.GetCallingAssembly());

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("cache");
builder
    .Services.AddFusionCache()
    .WithSerializer(
        new FusionCacheSystemTextJsonSerializer(
            new JsonSerializerOptions() { TypeInfoResolverChain = { LocationSerializerContext.Default } }
        )
    )
    .WithRegisteredDistributedCache();

builder.Services.AddLocationsService();

builder.Services.AddSingleton(Random.Shared);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(LocationSerializerContext.Default)
);

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet(
    "/{areaName}",
    async (
        [FromServices] LocationsService locations,
        [FromServices] Random random,
        [FromRoute] string areaName,
        CancellationToken cancellationToken
    ) =>
        random.GetItems(await locations.GetLocationsInAsync(areaName, cancellationToken), 1) is [var location]
            ? Results.Ok(location)
            : Results.NotFound()
);

app.MapGet(
    "/{areaName}/all",
    async (
        [FromServices] LocationsService locations,
        [FromServices] Random random,
        [FromRoute] string areaName,
        CancellationToken cancellationToken
    ) => await locations.GetLocationsInAsync(areaName, cancellationToken)
);

app.Run();
