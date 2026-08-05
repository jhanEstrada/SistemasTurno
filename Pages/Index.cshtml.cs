using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace SIGEF.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        // --- Configuración de conexión a la base de datos "pendientes" ---
        private const string ConnectionString =
            "Host=145.223.120.19;Port=5432;Database=pendientes;Username=postgres;Password=L0g1f4rm4;";

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        [BindProperty]
        public string Username { get; set; } // Número de documento

        [BindProperty]
        public string Password { get; set; } // Clave de 6 dígitos

        public string ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Usuario o contraseña incorrectos.";
                return Page();
            }

            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();

            const string sql = @"
                SELECT clave, estado, nombre, rol
                FROM ""UsuariosSigef""
                WHERE documento = @documento
                LIMIT 1;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("documento", Username.Trim());

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                // No existe ningún usuario con ese documento
                ErrorMessage = "Usuario o contraseña incorrectos.";
                return Page();
            }

            string claveBd = reader.GetString(reader.GetOrdinal("clave"));
            bool estado = reader.GetBoolean(reader.GetOrdinal("estado"));
            string nombre = reader.GetString(reader.GetOrdinal("nombre"));
            string rol = reader.GetString(reader.GetOrdinal("rol"));

            // La contraseña no coincide
            if (!string.Equals(claveBd, Password.Trim(), StringComparison.Ordinal))
            {
                ErrorMessage = "Usuario o contraseña incorrectos.";
                return Page();
            }

            // El usuario existe y la contraseña es correcta, pero está inactivo
            if (!estado)
            {
                ErrorMessage = "Tu usuario se encuentra inactivo. Contacta al administrador de SIGEF.";
                return Page();
            }

            // Login exitoso: guardamos datos básicos en sesión
            HttpContext.Session.SetString("SigefDocumento", Username.Trim());
            HttpContext.Session.SetString("SigefNombre", nombre);
            HttpContext.Session.SetString("SigefRol", rol);

            _logger.LogInformation("Usuario {Documento} inició sesión en SIGEF con rol {Rol}", Username, rol);

            // TODO: ajustar la ruta de destino según tu página principal / Menu
            return RedirectToPage("/Menu");
        }
    }
}
