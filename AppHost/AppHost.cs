using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");

builder.AddProject<Projects.Web>("web").WithHttpHealthCheck("/health").WithReference(redis);

builder.Build().Run();
