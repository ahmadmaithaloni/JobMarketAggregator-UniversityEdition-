using Microsoft.EntityFrameworkCore;
using Scalar;
using Scalar.AspNetCore;
using ScraperAPI.Services.LocationMapper_Service;
using ScraperAPI.Services.ScraperService;
using ScraperAPI.Services.Scraping_Service;
using WebApplication1.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // stop the "Object Cycle" crash that happen when the scraped jobs stored in the db
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<ScrapingEngineDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))); // regester the DbContext
builder.Services.AddScoped<ILocationMapperService, LocationMapper>(); // register the location mapper service
builder.Services.AddScoped<IScrapingService, DynamicScrapingEngine>(); // regester the dynamic scraping engine as scraping service
builder.Services.AddScoped<IScraperService, BaytScraper>(); // first scraper regestration under scraping engine
builder.Services.AddScoped<IScraperService, ReedScraper>(); // second scraper regestration under scraping engine
//builder.Services.Configure<SerpApiSettings>(builder.Configuration.GetSection("SerpApi")); // regester the serp api key from appsettings as a service (for security)

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapScalarApiReference();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
