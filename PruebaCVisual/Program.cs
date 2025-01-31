using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Construir la aplicacion
var app = builder.Build();

//Traigo la cadena de conexion desde el archivo appsetting
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//Iniciar conexion a la db
try
{
    using (var connection = new SqlConnection(connectionString))
    { 
        await connection.OpenAsync();
        Console.WriteLine("Conexion a la base de datos exitosa");
    }
}
catch (Exception ex)
{ 
    Console.WriteLine($"Error al conectar con la base de datos: {ex.Message}");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
