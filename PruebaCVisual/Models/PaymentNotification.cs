using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PruebaCVisual.Models
{
    public class PaymentNotification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime FechaHora { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string TransaccionID { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = string.Empty ;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 1000000, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Monto { get; set; }

        [Required]
        [StringLength(50)]
        public string Banco { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string MetodoPago {  get; set; } = string.Empty;

    }
}
