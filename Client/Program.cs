using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sandbox.Interface;

if (args.Any(arg => string.Equals(arg, "--runners", StringComparison.OrdinalIgnoreCase)))
{
    RunRunnerMenu();
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins("https://app.example.com", "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Demo only: cấu hình thật thì bổ sung Authority / Audience / TokenValidationParameters
        options.RequireHttpsMetadata = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Global exception handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = app.Environment.IsDevelopment() ? exception?.Message : null,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

// Chỉ bật swagger ở môi trường dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ví dụ custom middleware để log request/response time
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;

    Console.WriteLine($"[Request] {context.Request.Method} {context.Request.Path}");

    await next();

    var elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds;
    Console.WriteLine($"[Response] {context.Response.StatusCode} - {elapsedMs:N0} ms");
});

// Routing
app.UseRouting();

// CORS nên nằm trước Authentication/Authorization trong nhiều API scenario
app.UseCors("FrontendPolicy");

// Security
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapControllers();

// Có thể thêm health endpoint đơn giản
app.MapGet("/health", () => Results.Ok(new
{
    status = "OK",
    utcNow = DateTime.UtcNow
}))
.AllowAnonymous();

app.Run();

static void RunRunnerMenu()
{
    List<Type> runnerTypes =
    [
        ..typeof(IRunner).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                typeof(IRunner).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.Name)
    ];

    if (runnerTypes.Count == 0)
    {
        Console.WriteLine("No runner found.");
        return;
    }

    for (int index = 0; index < runnerTypes.Count; index++)
    {
        Console.WriteLine($"Option {index + 1}: {runnerTypes[index].Name}");
    }

    Console.Write("Enter option: ");
    string? input = Console.ReadLine();
    if (!int.TryParse(input, out int optionSelected) || optionSelected < 1 || optionSelected > runnerTypes.Count)
    {
        Console.WriteLine("Invalid option.");
        return;
    }

    Type selectedRunnerType = runnerTypes[optionSelected - 1];
    if (Activator.CreateInstance(selectedRunnerType) is not IRunner runner)
    {
        Console.WriteLine($"Cannot create runner: {selectedRunnerType.Name}");
        return;
    }

    Console.WriteLine(runner.GetType().Name);
    runner.RunExample();
}
