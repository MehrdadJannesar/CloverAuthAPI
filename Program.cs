using CloverAuthAPI.Controllers;
using CloverAuthAPI.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<CloverSettings>(builder.Configuration.GetSection("CloverSettings"));

builder.Services.AddHttpClient<CloverAuthController>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();
