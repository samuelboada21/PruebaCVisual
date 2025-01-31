using Microsoft.EntityFrameworkCore;
using PruebaCVisual.Data;
using PruebaCVisual.Models;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

//Inyecto Stripe
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

// Configuro la clave secreta de Stripe
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Agregar conexión a la base de datos SQL Server
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Agregar servicios del controlador
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Construir la aplicacion
var app = builder.Build();

//Traigo la cadena de conexion desde el archivo appsetting
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
