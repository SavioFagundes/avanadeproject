using System.Text;
using InventoryService.Auth;
using InventoryService.Data;
using InventoryService.Models;
using InventoryService.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };
});
builder.Services.AddAuthorization();

builder.Services.AddHostedService<RabbitSubscriber>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status="ok", service="inventory"})).AllowAnonymous();
app.MapAuth();

app.UseAuthentication();
app.UseAuthorization();

var products = app.MapGroup("/api/products").RequireAuthorization();

products.MapGet("/", async (AppDbContext db) => await db.Products.AsNoTracking().ToListAsync());
products.MapGet("/{id:int}", async (int id, AppDbContext db) => await db.Products.FindAsync(id) is { } p ? Results.Ok(p) : Results.NotFound());
products.MapPost("/", async (Product p, AppDbContext db) => { db.Products.Add(p); await db.SaveChangesAsync(); return Results.Created($"/api/products/{p.Id}", p); });
products.MapPut("/{id:int}", async (int id, Product updated, AppDbContext db) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();
    p.Name = updated.Name;
    p.Description = updated.Description;
    p.Price = updated.Price;
    p.Quantity = updated.Quantity;
    await db.SaveChangesAsync();
    return Results.Ok(p);
});
products.MapGet("/{id:int}/availability", async (int id, int qty, AppDbContext db) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();
    return Results.Ok(new { available = p.Quantity >= qty, current = p.Quantity });
});

app.Run();
