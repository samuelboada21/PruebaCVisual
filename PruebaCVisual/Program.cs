using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PruebaCVisual.Data;
using PruebaCVisual.Models;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Configuración de JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });


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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
