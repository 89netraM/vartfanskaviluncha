using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Web>("web").WithHttpHealthCheck("/health");

builder.Build().Run();
