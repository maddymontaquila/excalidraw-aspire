#:package CommunityToolkit.Aspire.Hosting.NodeJS.Extensions@9.8.0
#:package Aspire.Hosting.DevTunnels@13.0.0-preview.1.25503.1
#:sdk Aspire.AppHost.Sdk@13.0.0-preview.1.25503.1

using Aspire.Hosting.DevTunnels;

var builder = DistributedApplication.CreateBuilder(args);


var collabServer = builder.AddYarnApp("collab-server", "./excalidraw-room", "start:dev")
    .WithHttpEndpoint(env: "PORT");

var app = builder.AddYarnApp("excalidraw", "./excalidraw")
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(env: "VITE_APP_PORT")
    .WithIconName("DrawShape");
//.WithEnvironment("VITE_APP_WS_SERVER_URL", collabServer.GetEndpoint("http"));

var devTunnel = builder.AddDevTunnel("excalidraw-tunnel-2")
    .WithAnonymousAccess()
    .WithReference(collabServer)
    .WithReference(app);

var tcs = new TaskCompletionSource();

builder.Eventing.Subscribe<ResourceReadyEvent>(devTunnel.Resource, async (e, ct) =>
{
    tcs.SetResult();
});

app.WithEnvironment(async c =>
{
    await tcs.Task;
});

#region excalidraw-dockerfile
builder.AddDockerfile("excalidraw-docker", "./excalidraw")
    .WithHttpEndpoint(port: 3000, targetPort: 80)
    .WithEnvironment("NODE_ENV", "development")
    .WithBuildArg("NODE_ENV", "development")
    .WithBindMount("./excalidraw", "/opt/node_app/app")
    .WithBindMount("./excalidraw/package.json", "/opt/node_app/package.json")
    .WithBindMount("./excalidraw/yarn.lock", "/opt/node_app/yarn.lock")
    .WithIconName("DrawShape", IconVariant.Regular)
    .WithExplicitStart();
#endregion

builder.Build().Run();
