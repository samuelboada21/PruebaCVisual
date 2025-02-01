using System.ComponentModel.DataAnnotations;

namespace PruebaCVisual.Models
{
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Nombre { get; set; }

        [Required]
        [MaxLength(50)]
        public int Apellido { get; set; }

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Correo { get; set; }

        [Required]
        [MaxLength(255)]
        public string Contrasenia { get; set; }

        [Required]
        [MaxLength(20)]
        public string Rol {  get; set; }

    }
}
