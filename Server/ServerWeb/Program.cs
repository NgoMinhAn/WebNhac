using ServerWeb.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// 1. Connect to MongoDB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("Missing connection string 'DefaultConnection'");

var mongoClient = new MongoClient(connectionString);
builder.Services.AddSingleton<IMongoClient>(mongoClient);

builder.Services.AddSingleton(sp => 
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase("node_auth_test");
});

builder.Services.AddScoped<AppDbContext>(sp => 
{
    var database = sp.GetRequiredService<IMongoDatabase>();
    return new AppDbContext(database);
});

// 2. Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.Name = "MusicApp_Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

// 3. Add services to the container
builder.Services.AddControllersWithViews();

// 4. Add Session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// 5. Initialize MongoDB collections with indexes
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    
    // Ensure collections exist and create indexes
    try
    {
        // Users collection
        var usersCollection = db.GetCollection<ServerWeb.Models.User>("users");
        var userIndexOptions = new CreateIndexOptions { Unique = true };
        await usersCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ServerWeb.Models.User>(
                Builders<ServerWeb.Models.User>.IndexKeys.Ascending(u => u.Email),
                userIndexOptions
            )
        );

        // Songs collection
        var songsCollection = db.GetCollection<ServerWeb.Models.Song>("songs");
        
        // Playlists collection  
        var playlistsCollection = db.GetCollection<ServerWeb.Models.Playlist>("playlists");

        // PlaylistSongs collection
        var playlistSongsCollection = db.GetCollection<ServerWeb.Models.PlaylistSong>("playlistSongs");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning($"MongoDB initialization warning: {ex.Message}");
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Configure middleware order (IMPORTANT)
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();