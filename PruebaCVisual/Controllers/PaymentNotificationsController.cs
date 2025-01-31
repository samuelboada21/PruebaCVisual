using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PruebaCVisual.Data;
using PruebaCVisual.Models;

namespace PruebaCVisual.Controllers
{
    [Route("api")]
    [ApiController]
    public class PaymentNotificationsController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly string _connectionString;
        private readonly string _logPath = "Logs/";

        //constructor
        public PaymentNotificationsController(DatabaseContext context)
        { 
            _context = context;
            _connectionString = _context.Database.GetConnectionString();

            //Verifico si existe la carpeta Logs, si no, entonces la creo
            if (!Directory.Exists(_logPath))
            {
                Directory.CreateDirectory(_logPath);
            }
        }

        //POST: /api/webhook/payments
        [HttpPost("webhook/payments")]
        public async Task<IActionResult> CreatePaymentNotification([FromBody] PaymentNotification paymentNotification)
        {
            if (paymentNotification == null)
            {
                LogTransaction("Datos de pago no proporcionados", "Error");
                return BadRequest("Datos de pago no proporcionados");
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                { 
                    await connection.OpenAsync();

                    //Llamo al procedimiento almacenado para insertar
                    using (var command = new SqlCommand("sp_InsertPaymentNotification", connection))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;

                        //Parámetros para el procedimiento almacenado
                        command.Parameters.AddWithValue("@FechaHora", paymentNotification.FechaHora);
                        command.Parameters.AddWithValue("@TransaccionID", paymentNotification.TransaccionID);
                        command.Parameters.AddWithValue("@Estado", paymentNotification.Estado);
                        command.Parameters.AddWithValue("@Monto", paymentNotification.Monto);
                        command.Parameters.AddWithValue("@Banco", paymentNotification.Banco);
                        command.Parameters.AddWithValue("@MetodoPago", paymentNotification.MetodoPago);

                        await command.ExecuteNonQueryAsync();
                    }
                }
                LogTransaction($"Pago  {paymentNotification.TransaccionID} registrado correctamente", "Exito");
                return CreatedAtAction(nameof(CreatePaymentNotification), new { id = paymentNotification.Id }, paymentNotification);
            }
            catch (Exception ex)
            {
                LogTransaction($"No se pudo procesar la solicitud --> {ex.Message}", "Error");
                return StatusCode(500, $"Error al procesar la solicitud: {ex.Message}");
            }
        }

        //GET: /api/webhook/payments
        [HttpGet("webhook/payments")]
        public async Task<IActionResult> GetAllPayments()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                { 
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("GetAllPaymentNotifications", connection))
                    { 
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var payments = new List<PaymentNotification>();
                            while (await reader.ReadAsync()) 
                            {
                                payments.Add(new PaymentNotification
                                {
                                    Id = reader.GetInt32(0),
                                    FechaHora = reader.GetDateTime(1),
                                    TransaccionID = reader.GetString(2),
                                    Estado = reader.GetString(3),
                                    Monto = reader.GetDecimal(4),
                                    Banco = reader.GetString(5),
                                    MetodoPago = reader.GetString(6)
                                });
                            }
                            return Ok(payments);
                        }
                    }
                }
            }
            catch (Exception ex) 
            {
                return StatusCode(500, $"Error al obtener los pagos: {ex.Message}");
            }
        }

        //GET: /api/webhook/payments/{id}
        [HttpGet("webhook/payments/{id}")]
        public async Task<IActionResult> GetPaymentById(int id)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString)) 
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("GetPaymentNotificationById", connection))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Id", id);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var payment = new PaymentNotification
                                {
                                    Id = reader.GetInt32(0),
                                    FechaHora = reader.GetDateTime(1),
                                    TransaccionID = reader.GetString(2),
                                    Estado = reader.GetString(3),
                                    Monto = reader.GetDecimal(4),
                                    Banco = reader.GetString(5),
                                    MetodoPago = reader.GetString(6)
                                };
                                return Ok(payment);
                            }
                            return NotFound($"No se encontró el pago con ID {id}");
                        }
                    }
                }
            }
            catch (Exception ex) 
            {
                return StatusCode(500, $"Error al obtener el pago: {ex.Message}");
            }
        }

        private void LogTransaction(string message, string status)
        {
            var logFile = $"{_logPath}transacciones_{DateTime.UtcNow:yyyyMMdd}.log";
            var logMessage = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Estado: {status} - {message}\n";

            System.IO.File.AppendAllText(logFile, logMessage);
        }
    }
}
