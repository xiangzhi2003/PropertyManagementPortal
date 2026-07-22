using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using Amazon.S3;
using Amazon.Runtime;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Load .env containing AWS key
DotNetEnv.Env.Load();

// Build AWS credentials
var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";


// Registers IAmazonS3
if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
{
    var credentials = new BasicAWSCredentials(accessKey, secretKey);
    
    // 3. Register IAmazonS3 using these specific credentials and region
    builder.Services.AddSingleton<IAmazonS3>(sp => 
        new AmazonS3Client(credentials, Amazon.RegionEndpoint.GetBySystemName(region)));
}
else
{
    // Fallback default registration for environments like production (IAM Roles)
    builder.Services.AddAWSService<IAmazonS3>();
}

// Add the service to controllers
builder.Services.AddScoped<IS3Service, S3Service>(); 
builder.Services.AddControllersWithViews();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

builder.Services.AddAuthentication()
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    });

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

var app = builder.Build();

try
{
    await SeedData.InitializeAsync(app);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "SeedData error: {Message}", ex.Message);
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.Run();
