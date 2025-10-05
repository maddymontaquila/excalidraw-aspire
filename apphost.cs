#:package CommunityToolkit.Aspire.Hosting.NodeJS.Extensions@9.8.0
#:package Aspire.Hosting.DevTunnels@9.5.1-*
#:package Aspire.Hosting.Yarp@9.5.1-*
#:package Aspire.Hosting.Azure.AppContainers@9.5.1
#:sdk Aspire.AppHost.Sdk@9.5.1

using Aspire.Hosting.DevTunnels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

var aca = builder.AddAzureContainerAppEnvironment("aca");

var collabServer = builder.AddYarnApp("collab-server", "./excalidraw-room", "start:dev")
    .WithYarnPackageInstallation()
    .WithHttpEndpoint(env: "PORT")
    .PublishAsDockerFile(c =>
    {
        c.WithEndpoint("http", c => c.UriScheme = "https");
    })
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MaxReplicas = 1;
    });

if (builder.ExecutionContext.IsRunMode)
{
    var app = builder.AddYarnApp("excalidraw-dev", "./excalidraw")
        .WithYarnPackageInstallation()
        .WithEnvironment("BROWSER", "none")
        .WithHttpEndpoint(env: "VITE_APP_PORT")
        .WithIconName("DrawShape");

    var devTunnel = builder.AddDevTunnel("excalidraw-tunnel")
        .WithAnonymousAccess()
        .WithReference(collabServer)
        .WithReference(app);

    var tcs = new TaskCompletionSource<EndpointReference>(TaskCreationOptions.RunContinuationsAsynchronously);

    #region Workaround Fowler's lapse of judgement

    var portResourceName = $"{devTunnel.Resource.Name}-{collabServer.Resource.Name}-http";
    builder.CreateResourceBuilder<DevTunnelPortResource>(portResourceName)
           .OnResourceEndpointsAllocated((resource, evt, ct) =>
           {
               tcs.TrySetResult(resource.GetEndpoint("tunnel"));
               return Task.CompletedTask;
           });

    #endregion

    app.WithEnvironment(async c =>
    {
        var tunnelEndpoint = await tcs.Task;
        c.EnvironmentVariables["VITE_APP_WS_SERVER_URL"] = tunnelEndpoint;
    });
}

builder.AddYarp("excalidraw")
       .WithExternalHttpEndpoints()
       .WithDockerfile("./excalidraw", "Dockerfile.aspire")
       .WithStaticFiles()
       .WithConfiguration(c =>
       {
           c.AddRoute("/socket.io/{**catch-all}", collabServer);
       })
       .WithExplicitStart();

builder.Build().Run();
