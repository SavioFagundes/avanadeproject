using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using SalesService.Auth;
using SalesService.Data;
using SalesService.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtSection = builder.Configuration.GetSection("JWT");
var secret = jwtSection["Secret"] ?? "super_secret_key_change_me";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret))
    };
});
builder.Services.AddAuthorization();

builder.Services.AddHttpClient("inventory", (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>()["INVENTORY_BASE_URL"] ?? "http://localhost:8081";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status="ok", service="sales"})).AllowAnonymous();
app.MapAuth();

app.UseAuthentication();
app.UseAuthorization();

var orders = app.MapGroup("/api/orders").RequireAuthorization();

orders.MapGet("/", async (AppDbContext db) =>
{
    var list = await db.Orders.Include(o => o.Items).AsNoTracking().OrderByDescending(o => o.Id).ToListAsync();
    return Results.Ok(list);
});

orders.MapGet("/{id:int}", async (int id, AppDbContext db) =>
{
    var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

orders.MapPost("/", async (CreateOrderDto dto, AppDbContext db, IHttpClientFactory httpFactory, IConfiguration cfg) =>
{
    // validate stock
    var http = httpFactory.CreateClient("inventory");

    foreach (var it in dto.Items)
    {
        var resp = await http.GetAsync($"/api/products/{it.ProductId}/availability?qty={it.Quantity}");
        if (!resp.IsSuccessStatusCode) return Results.BadRequest(new { error = $"Product {it.ProductId} not found" });
        var payload = await resp.Content.ReadFromJsonAsync<AvailabilityResponse>();
        if (payload is null || payload.available == false)
        {
            return Results.BadRequest(new { error = $"Insufficient stock for product {it.ProductId}", current = payload?.current ?? 0 });
        }
    }

    var order = new Order { Items = dto.Items.Select(i => new OrderItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList() };
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    PublishOrderConfirmed(cfg, order);
    return Results.Created($"/api/orders/{order.Id}", order);
});

app.Run();

static void PublishOrderConfirmed(IConfiguration cfg, Order order)
{
    var factory = new ConnectionFactory { HostName = cfg["RabbitMQ:HostName"] ?? "rabbitmq" };
    var queue = cfg["RabbitMQ:Queue"] ?? "order.confirmed";
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();
    channel.QueueDeclare(queue: queue, durable: false, exclusive: false, autoDelete: false, arguments: null);

    var message = new { Items = order.Items.Select(i => new { i.ProductId, i.Quantity }).ToList() };
    var json = System.Text.Json.JsonSerializer.Serialize(message);
    var body = System.Text.Encoding.UTF8.GetBytes(json);
    channel.BasicPublish(exchange: "", routingKey: queue, basicProperties: null, body: body);
}

record AvailabilityResponse(bool available, int current);
public record CreateOrderDto(List<CreateOrderItemDto> Items);
public record CreateOrderItemDto(int ProductId, int Quantity);
