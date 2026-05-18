using System.Text.Json;
using DemoWeb.Models;
using DemoWeb.Options;
using DemoWeb.Services;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<CdcDemoOptions>(
    builder.Configuration.GetSection(CdcDemoOptions.SectionName));

builder.Services.PostConfigure<CdcDemoOptions>(options => options.Validate());

builder.Services.AddHttpClient<KafkaConnectClient>();
builder.Services.AddSingleton<MySqlDemoStore>();
builder.Services.AddSingleton<KafkaTopicInspector>();
builder.Services.AddSingleton<KafkaDemoProducer>();
builder.Services.AddSingleton<CdcDemoDashboard>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Demo request failed.",
            detail = exception?.Message ?? "Unexpected error."
        });
    });
});

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api/demo");

api.MapGet("/snapshot", async (CdcDemoDashboard dashboard, CancellationToken cancellationToken) =>
{
    var snapshot = await dashboard.GetSnapshotAsync(cancellationToken);
    return Results.Ok(snapshot);
});

api.MapPost("/actions/insert", async (
    DemoActionRequest request,
    CdcDemoDashboard dashboard,
    CancellationToken cancellationToken) =>
{
    var response = await dashboard.InsertCustomerAsync(request, cancellationToken);
    return Results.Ok(response);
});

api.MapPost("/actions/update", async (
    DemoActionRequest request,
    CdcDemoDashboard dashboard,
    CancellationToken cancellationToken) =>
{
    var response = await dashboard.UpdateCustomerAsync(request, cancellationToken);
    return Results.Ok(response);
});

api.MapPost("/actions/delete", async (
    DemoActionRequest request,
    CdcDemoDashboard dashboard,
    CancellationToken cancellationToken) =>
{
    var response = await dashboard.DeleteCustomerAsync(request, cancellationToken);
    return Results.Ok(response);
});

api.MapPost("/actions/truncate", async (
    CdcDemoDashboard dashboard,
    CancellationToken cancellationToken) =>
{
    var response = await dashboard.TruncateCustomersAsync(cancellationToken);
    return Results.Ok(response);
});

api.MapPost("/actions/seed", async (
    CdcDemoDashboard dashboard,
    CancellationToken cancellationToken) =>
{
    var response = await dashboard.SeedCustomersAsync(cancellationToken);
    return Results.Ok(response);
});

api.MapPost("/actions/poison", async (
    CdcDemoDashboard dashboard,
    CancellationToken cancellationToken) =>
{
    var response = await dashboard.PublishPoisonMessageAsync(cancellationToken);
    return Results.Ok(response);
});

app.MapFallbackToFile("index.html");

await app.RunAsync();
