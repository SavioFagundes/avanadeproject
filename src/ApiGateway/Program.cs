using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        new Yarp.ReverseProxy.Configuration.RouteConfig[] {
            new() {
                RouteId = "inventory",
                ClusterId = "inventory",
                Match = new() { Path = "/inventory/{**catch-all}" },
                Transforms = new[] { new Dictionary<string,string>{{"PathPattern","/{**catch-all}"}} }
            },
            new() {
                RouteId = "sales",
                ClusterId = "sales",
                Match = new() { Path = "/sales/{**catch-all}" },
                Transforms = new[] { new Dictionary<string,string>{{"PathPattern","/{**catch-all}"}} }
            }
        },
        new Dictionary<string, Yarp.ReverseProxy.Configuration.ClusterConfig> {
            ["inventory"] = new() {
                Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig> {
                    ["d1"] = new() { Address = builder.Configuration["YARP:Clusters:inventory:Destinations:d1:Address"] ?? "http://localhost:8081/" }
                }
            },
            ["sales"] = new() {
                Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig> {
                    ["d1"] = new() { Address = builder.Configuration["YARP:Clusters:sales:Destinations:d1:Address"] ?? "http://localhost:8082/" }
                }
            }
        }
    );

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status="ok", service="gateway"}));
app.MapReverseProxy();
app.Run();
