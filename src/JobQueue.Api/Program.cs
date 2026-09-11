using JobQueue.Application;
using JobQueue.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
// OpenAPI document + Scalar interactive reference UI. Gate these behind
// app.Environment.IsDevelopment() (or authentication) before deploying publicly.
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("JobQueue API"));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
