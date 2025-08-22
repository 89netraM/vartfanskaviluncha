using System;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VartFanSkaViLuncha.Web;
using VartFanSkaViLuncha.Web.Components;
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

builder.Services.AddRazorComponents();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(LocationSerializerContext.Default)
);

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapRazorComponents<App>().DisableAntiforgery();

app.Run();
