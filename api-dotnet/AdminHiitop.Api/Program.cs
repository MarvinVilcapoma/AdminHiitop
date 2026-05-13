using AdminHiitop.Api.Application;
using AdminHiitop.Api.Extensions;
using AdminHiitop.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

await app.UseApiPipelineAsync();
