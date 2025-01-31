using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PruebaCVisual.Data;
using PruebaCVisual.Models;
using Stripe;

namespace PruebaCVisual.Controllers
{
    [Route("api")]
    [ApiController]
    public class PaymentNotificationsController : ControllerBase
    {
        private readonly string _stripeSecret;
        private readonly string _connectionString;
        private readonly string _logPath = "logs/";
        private readonly IConfiguration _configuration;

        public PaymentNotificationsController(DatabaseContext context, IOptions<StripeSettings> stripeSettings, IConfiguration configuration)
        {
            _stripeSecret = stripeSettings.Value.SecretKey;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _configuration = configuration;

            if (!Directory.Exists(_logPath))
            {
                Directory.CreateDirectory(_logPath);
            }
        }

        //POST: /api/webhook/payments
        [HttpPost("webhook/payments")]
        public async Task<IActionResult> ReceivePaymentNotification()
        {
            var signatureHeader = Request.Headers["Stripe-Signature"];
            // Se obtiene el secret del webhook desde la configuración
            var secret = _configuration.GetValue<string>("Stripe:WebHookSecret"); 
            var body = await new StreamReader(Request.Body).ReadToEndAsync();

            try
            {
                // Verifiao la firma
                var stripeEvent = EventUtility.ConstructEvent(
                    body,
                    signatureHeader,
                    secret
                );

                if (stripeEvent.Type == "payment_intent.succeeded")
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;

                    using (var connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        using (var command = new SqlCommand("sp_InsertPaymentNotification", connection))
                        {
                            command.CommandType = System.Data.CommandType.StoredProcedure;
                            command.Parameters.AddWithValue("@FechaHora", DateTime.UtcNow);
                            command.Parameters.AddWithValue("@TransaccionID", paymentIntent.Id);
                            command.Parameters.AddWithValue("@Estado", paymentIntent.Status);
                            command.Parameters.AddWithValue("@Monto", paymentIntent.AmountReceived / 100m);  // Stripe envía los montos en centavos
                            command.Parameters.AddWithValue("@Banco", "Stripe");
                            command.Parameters.AddWithValue("@MetodoPago", paymentIntent.PaymentMethodTypes[0]);

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    LogTransaction($"Pago {paymentIntent.Id} registrado correctamente", "Exito");
                    return Ok();
                }

                return BadRequest();
            }
            catch (Exception ex)
            {
                LogTransaction($" No se pudo registrar el pago: {ex.Message}", "Error");
                return StatusCode(400, $"Webhook Error: {ex.Message}");
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
