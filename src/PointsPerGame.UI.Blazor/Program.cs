using Microsoft.AspNetCore.HttpOverrides;
using PointsPerGame.Core.Services;
using PointsPerGame.Core.Web;
using PointsPerGame.UI.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<TablesService>();
builder.Services.AddScoped<ILeagueTableService, LeagueTableService>();
builder.Services.AddScoped<IResultsDataSource, GuardianScraper>();
builder.Services.AddScoped<TableAvailabilityChecker>();
builder.Services.AddSingleton<TableAvailabilityCache>();
builder.Services.AddHttpClient();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
