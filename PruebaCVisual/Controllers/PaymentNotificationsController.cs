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

        //constructor
        public PaymentNotificationsController(DatabaseContext context)
        { 
            _context = context;
            _connectionString = _context.Database.GetConnectionString();
        }

        //POST: /api/webhook/payments
        [HttpPost("webhook/payments")]
        public async Task<IActionResult> CreatePaymentNotification([FromBody] PaymentNotification paymentNotification)
        {
            if (paymentNotification == null)
            {
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
                return CreatedAtAction(nameof(CreatePaymentNotification), new { id = paymentNotification.Id }, paymentNotification);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al procesar la solicitud: {ex.Message}");
            }
        }
    }
}
