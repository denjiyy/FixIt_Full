using MongoDB.Driver;
using AspNetCore.Identity.Mongo;
using AspNetCore.Identity.Mongo.Model;
using FixIt.Models.Users;
using FixIt.Models.Infrastructure;
using FixIt.Data.Infrastructure;
using FixIt.Data.Repository;
using FixIt.Data.Repository.Contracts;
using FixIt.Services;
using FixIt.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure MongoDB
var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDB connection string not found");
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDB database name not found");

// Register MongoClient as singleton
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString(mongoConnectionString);
    return new MongoClient(settings);
});

// Register MongoDbContext
builder.Services.AddSingleton<MongoDbContext>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return new MongoDbContext(client, mongoDatabaseName);
});

// Register IMongoDatabase (needed for Repository<T>)
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

// Configure ASP.NET Core Identity with MongoDB
builder.Services.AddIdentityMongoDbProvider<ApplicationUser, MongoRole>(identity =>
{
    identity.Password.RequireDigit = true;
    identity.Password.RequiredLength = 8;
    identity.Password.RequireNonAlphanumeric = false;
    identity.Password.RequireUppercase = true;
    identity.Password.RequireLowercase = true;
    identity.User.RequireUniqueEmail = true;
},
mongo =>
{
    mongo.ConnectionString = mongoConnectionString;
});

// Register repositories with correct collection names
builder.Services.AddScoped<IRepository<FixIt.Models.Locations.City>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Locations.City>(db, "cities");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Locations.Neighborhood>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Locations.Neighborhood>(db, "neighborhoods");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Issues.Tag>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Issues.Tag>(db, "tags");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Issues.Issue>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Issues.Issue>(db, "issues");
});

builder.Services.AddScoped<IRepository<FixIt.Models.Engagement.Vote>>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    return new Repository<FixIt.Models.Engagement.Vote>(db, "votes");
});

// Register services (business logic layer)
builder.Services.AddScoped<IIssueService, IssueService>();
builder.Services.AddScoped<ITagService, TagService>();

// Add CORS if needed for mobile app
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

var app = builder.Build();

// Initialize database (indexes and seed data) - optional if MongoDB is available
try
{
    using (var scope = app.Services.CreateScope())
    {
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        await SeederRunner.RunAllConfiguratorsAsync(mongoContext.Database, scope.ServiceProvider);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Failed to initialize database. Make sure MongoDB is running at {ConnectionString}", mongoConnectionString);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FixIt API v1");
        c.RoutePrefix = "swagger"; // Swagger at /swagger, home page at root
        c.DefaultModelsExpandDepth(-1); // Collapse models by default
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapAreaControllerRoute(
    name: "Identity",
    areaName: "Identity",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.Run();