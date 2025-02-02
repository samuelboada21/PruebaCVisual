using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PruebaCVisual.Data;
using PruebaCVisual.Models;
using Stripe;
using System.IdentityModel.Tokens.Jwt;


namespace PruebaCVisual.Controllers
{
    [Route("api")]
    [ApiController]
    [Authorize]
    public class PaymentNotificationsController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _logPath = "Logs/";
        private readonly string _webhookSecret;


        public PaymentNotificationsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _webhookSecret = configuration["Stripe:WebHookSecret"];

            if (!Directory.Exists(_logPath))
            {
                Directory.CreateDirectory(_logPath);
            }
        }

        //Creamos la sesion desde un cliente, recuperamos la URL de stripe
        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutSessionRequest request)
        {
            // Obtener el usuario logueado por claim
            int usuarioId;
            var claimSub = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst(JwtRegisteredClaimNames.Sub);
            if (claimSub == null || !int.TryParse(claimSub.Value, out usuarioId))
            {
                return Unauthorized("No se pudo determinar el usuario autenticado.");
            }

            // Validar que successUrl y cancelUrl lleguen desde el cliente
            if (string.IsNullOrEmpty(request.SuccessUrl) || string.IsNullOrEmpty(request.CancelUrl))
            {
                return BadRequest("Las URLs de éxito y cancelación son obligatorias.");
            }

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                LineItems = request.Items.Select(item => new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = item.Currency,
                        UnitAmount = item.UnitAmount,
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Name,
                        }
                    },
                    Quantity = item.Quantity,
                }).ToList(),
                Mode = "payment",
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                Metadata = new Dictionary<string, string>
        {
            { "usuarioId", usuarioId.ToString() }
        }
            };

            var service = new Stripe.Checkout.SessionService();
            var session = await service.CreateAsync(options);

            return Ok(new
            {
                SessionId = session.Id,
                Url = session.Url
            });
        }

        //POST: /api/webhook/payments
        [HttpPost("webhook/payments")]
        [AllowAnonymous]
        public async Task<IActionResult> ReceivePaymentNotification()
        {
            var signatureHeader = Request.Headers["Stripe-Signature"];
            var body = await new StreamReader(Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(body, signatureHeader, _webhookSecret);

                //Solo recibimos el evendo de chackout completado
                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                    if (session == null)
                    {
                        return BadRequest("Objeto de sesión inválido.");
                    }

                    string paymentIntentId = session.PaymentIntentId;
                    if (string.IsNullOrEmpty(paymentIntentId))
                    {
                        return BadRequest("Faltan datos en la sesión completada.");
                    }

                    //UsuarioId desde la Metadata
                    string usuarioIdStr = session.Metadata?.ContainsKey("usuarioId") == true ? session.Metadata["usuarioId"] : null;
                    if (string.IsNullOrEmpty(usuarioIdStr) || !int.TryParse(usuarioIdStr, out int usuarioId))
                    {
                        return BadRequest("Faltan datos en la sesión completada.");
                    }

                    var paymentIntentService = new PaymentIntentService();
                    var paymentIntent = await paymentIntentService.GetAsync(paymentIntentId);

                    if (paymentIntent == null)
                    {
                        return BadRequest("No se pudo obtener información del pago.");
                    }

                    // Obtener el método de pago usado
                    string metodoPago = paymentIntent.PaymentMethodTypes.FirstOrDefault() ?? "N/A";
                    //Guardar en la DB
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        using (var command = new SqlCommand("sp_InsertPaymentNotification", connection))
                        {
                            command.CommandType = System.Data.CommandType.StoredProcedure;
                            command.Parameters.AddWithValue("@FechaHora", DateTime.UtcNow);
                            command.Parameters.AddWithValue("@TransaccionID", paymentIntent.Id);
                            command.Parameters.AddWithValue("@Estado", paymentIntent.Status);
                            command.Parameters.AddWithValue("@Monto", paymentIntent.AmountReceived / 100m);
                            command.Parameters.AddWithValue("@Banco", "Stripe");
                            command.Parameters.AddWithValue("@MetodoPago", metodoPago);
                            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    LogTransaction($"Pago {paymentIntent.Id} registrado correctamente con método: {metodoPago}", "Exito");
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
                // Obtener usuario logueado desde los claims
                var claimSub = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst(JwtRegisteredClaimNames.Sub);
                var claimRole = User.FindFirst("rol");

                if (claimSub == null || !int.TryParse(claimSub.Value, out int usuarioId))
                {
                    return Unauthorized("No se pudo determinar el usuario autenticado.");
                }

                bool esAdmin = claimRole != null && claimRole.Value.Equals("Administrador", StringComparison.OrdinalIgnoreCase);
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(esAdmin ? "GetAllPaymentNotifications" : "GetPaymentNotificationsByUserId", connection))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;

                        if (!esAdmin)
                        {
                            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        }

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
                                    MetodoPago = reader.GetString(6),
                                    UsuarioId = reader.GetInt32(7)
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
                                    MetodoPago = reader.GetString(6),
                                    UsuarioId = reader.GetInt32(7)
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
