using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PruebaCVisual.Data;
using PruebaCVisual.Models;

namespace PruebaCVisual.Controllers
{
    [Route("api")]
    [ApiController]
    public class UsuarioController : ControllerBase

    {
        private readonly DatabaseContext _context;
        private readonly IConfiguration _configuration;

        public UsuarioController(DatabaseContext context, IConfiguration configuration)
        { 
            _context = context;
            _configuration = configuration;
        }

        //POST: /api/registro
        [HttpPost("registro")]
        public async Task<IActionResult> RegistrarUsuario([FromBody] Usuario usuario)
        {
            //Validar que el correo no exista
            if (_context.Usuarios.Any(u => u.Correo == usuario.Correo))
            {
                return BadRequest("El correo ya está registrado");
            }

            //Hashear contrasenia
            var hashedPassword = HashPassword(usuario.Contrasenia);
            usuario.Contrasenia = hashedPassword;
            usuario.Rol = "Usuario";

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuario registrado exitosamente" });
        }

        //POST: /api/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == request.Correo);

            if (usuario == null || !VerifyPassword(request.Contrasenia, usuario.Contrasenia))
            {
                return Unauthorized("Credenciales Incorrectas");
            }

            var token = GenerateJwtToken(usuario);
            return Ok(new {Token = token});
        }

        //Hashear contrasenia
        private string HashPassword(string password)
        {
            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);

            string hashedPassword = Convert.ToBase64String(salt) + Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            return hashedPassword;
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            // Verificar que el hashedPassword no sea nulo
            if (string.IsNullOrEmpty(hashedPassword))
            {
                return false;
            }
            byte[] salt = Convert.FromBase64String(hashedPassword.Substring(0, 24)); 
            byte[] storedHash = Convert.FromBase64String(hashedPassword.Substring(24));

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            {
                byte[] computedHash = pbkdf2.GetBytes(32);

                // Comparo el hash generado con el hash almacenado en la base de datos
                return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
            }
        }


        private string GenerateJwtToken(Usuario usuario)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Correo),
                new Claim("nombre", usuario.Nombre),
                new Claim("rol", usuario.Rol),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class LoginRequest
        {
            public string Correo { get; set; }
            public string Contrasenia { get; set; }
        }
    }
}
