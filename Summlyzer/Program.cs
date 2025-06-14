var builder = WebApplication.CreateBuilder(args);

// 👇 Add controller services
builder.Services.AddControllers();

// (Optional) Add Swagger/OpenAPI if you're using it
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 👇 Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve Angular static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseRouting();

// 👇 Enable controller endpoint routing
app.MapControllers();

// Redirect all other requests to Angular's index.html (for SPA support)
app.MapFallbackToFile("index.html");

app.Run();
