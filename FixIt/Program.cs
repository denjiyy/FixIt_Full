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

// Register generic repository
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Register services (business logic layer)
builder.Services.AddScoped<IIssueService, IssueService>();
builder.Services.AddScoped<ITagService, TagService>();

var app = builder.Build();

// Initialize database (indexes and seed data)
using (var scope = app.Services.CreateScope())
{
    var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    await SeederRunner.RunAllConfiguratorsAsync(mongoContext.Database, scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();