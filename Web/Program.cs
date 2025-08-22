using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VartFanSkaViLuncha.Web;
using VartFanSkaViLuncha.Web.Services;

var builder = WebApplication.CreateSlimBuilder(args);

// if (builder.Environment.IsDevelopment())
// {
builder.Configuration.AddUserSecrets(Assembly.GetCallingAssembly());

// }

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("cache");
builder.Services.AddFusionCache();

builder.Services.AddLocationsService();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(LocationSerializerContext.Default)
);

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet(
    "/{areaName}",
    async (
        [FromServices] LocationsService locations,
        [FromRoute] string areaName,
        CancellationToken cancellationToken
    ) => await locations.GetLocationsInAsync(areaName, cancellationToken)
);

app.Run();
