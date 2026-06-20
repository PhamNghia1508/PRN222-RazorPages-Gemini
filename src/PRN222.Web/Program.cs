using PRN222.Web.Hubs;
using PRN222.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationWeb()
    .AddDataAccess(builder.Configuration)
    .AddBusinessServices()
    .AddUploadLimits();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MigrateAndSeedEmbeddingModels();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapHub<AdminHub>("/hubs/admin");

app.Run();
