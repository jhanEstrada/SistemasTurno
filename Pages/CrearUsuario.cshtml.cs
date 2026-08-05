using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace SIGEF.Pages
{
    public class CrearUsuarioModel : PageModel
    {
        // --- Configuración de conexión a la base de datos "pendientes" ---
        private const string ConnectionString =
            "Host=145.223.120.19;Port=5432;Database=pendientes;Username=postgres;Password=L0g1f4rm4;";

        // --- Configuración del correo remitente (Gmail con contraseña de aplicación) ---
        private const string SmtpRemitente = "auxlogistica2@logifarma.co";
        private const string SmtpPassword = "huvq fgco suaa rgvr";
        private const string CorreoNotificacion = "estradajhan3@gmail.com";

        [BindProperty]
        public string NombreCompleto { get; set; }

        [BindProperty]
        public string TipoDocumento { get; set; }

        [BindProperty]
        public string NumeroDocumento { get; set; }

        [BindProperty]
        public string Direccion { get; set; }

        [BindProperty]
        public string Municipio { get; set; }

        [BindProperty]
        public string Correo { get; set; }

        [BindProperty]
        public string Rol { get; set; }

        // Mensajes para mostrar en pantalla (los usa la vista en el div de alerta)
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Revisa los datos, hay campos incompletos.";
                return Page();
            }

            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();

            // 1. Verificar si el documento ya existe
            const string checkSql = "SELECT 1 FROM \"UsuariosSigef\" WHERE documento = @documento LIMIT 1;";
            await using (var checkCmd = new NpgsqlCommand(checkSql, conn))
            {
                checkCmd.Parameters.AddWithValue("documento", NumeroDocumento);
                var existe = await checkCmd.ExecuteScalarAsync();

                if (existe != null)
                {
                    ErrorMessage = "No se pudo crear el usuario porque el documento ya existe.";
                    return Page();
                }
            }

            // 2. Generar contraseña aleatoria de 6 dígitos
            string claveGenerada = GenerarClaveNumerica(6);

            // 3. Insertar el nuevo usuario
            const string insertSql = @"
                INSERT INTO ""UsuariosSigef""
                    (tipodoc, documento, clave, nombre, direccion, municipio, correo, rol)
                VALUES
                    (@tipodoc, @documento, @clave, @nombre, @direccion, @municipio, @correo, @rol);";

            try
            {
                await using var insertCmd = new NpgsqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("tipodoc", TipoDocumento);
                insertCmd.Parameters.AddWithValue("documento", NumeroDocumento);
                insertCmd.Parameters.AddWithValue("clave", claveGenerada);
                insertCmd.Parameters.AddWithValue("nombre", NombreCompleto);
                insertCmd.Parameters.AddWithValue("direccion", (object)Direccion ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("municipio", (object)Municipio ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("correo", (object)Correo ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("rol", Rol);

                await insertCmd.ExecuteNonQueryAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                // Protección extra por si dos solicitudes llegan al mismo tiempo (condición de carrera)
                ErrorMessage = "No se pudo crear el usuario porque el documento ya existe.";
                return Page();
            }

            // 4. Enviar correo de notificación con los datos del usuario y la contraseña asignada
            try
            {
                await EnviarCorreoNotificacionAsync(claveGenerada);
            }
            catch (Exception)
            {
                // El usuario ya quedó creado en la base de datos aunque el correo falle;
                // se informa igual pero se aclara que la notificación no se pudo enviar.
                SuccessMessage = "Usuario creado exitosamente, pero no se pudo enviar el correo de notificación.";
                return Page();
            }

            // 5. Confirmación en pantalla
            SuccessMessage = "Usuario creado exitosamente. Se ha enviado la notificación con los datos de acceso.";
            return Page();
        }

        private static string GenerarClaveNumerica(int digitos)
        {
            Span<byte> buffer = stackalloc byte[4];
            RandomNumberGenerator.Fill(buffer);
            uint valor = BitConverter.ToUInt32(buffer);

            int min = (int)Math.Pow(10, digitos - 1);
            int max = (int)Math.Pow(10, digitos) - 1;

            int numero = min + (int)(valor % (uint)(max - min + 1));
            return numero.ToString();
        }

        private async Task EnviarCorreoNotificacionAsync(string clave)
        {
            var mensaje = new MailMessage
            {
                From = new MailAddress(SmtpRemitente, "SIGEF - Sistema Integral de Gestión Farmacéutica"),
                Subject = "Nuevo usuario creado en SIGEF",
                IsBodyHtml = true,
                Body = $@"
                    <div style='font-family: Segoe UI, sans-serif; color:#0c2338;'>
                        <h2 style='color:#0f4c81;'>Nuevo usuario registrado en SIGEF</h2>
                        <p>Se ha creado un nuevo usuario con los siguientes datos:</p>
                        <table style='border-collapse: collapse; width:100%; max-width:480px;'>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Nombre completo:</td><td style='padding:6px 10px;'>{NombreCompleto}</td></tr>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Tipo de documento:</td><td style='padding:6px 10px;'>{TipoDocumento}</td></tr>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Número de documento:</td><td style='padding:6px 10px;'>{NumeroDocumento}</td></tr>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Dirección:</td><td style='padding:6px 10px;'>{Direccion}</td></tr>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Municipio:</td><td style='padding:6px 10px;'>{Municipio}</td></tr>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Correo:</td><td style='padding:6px 10px;'>{Correo}</td></tr>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Rol asignado:</td><td style='padding:6px 10px;'>{Rol}</td></tr>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Usuario de acceso:</td><td style='padding:6px 10px;'>{NumeroDocumento}</td></tr>
                            <tr><td style='padding:6px 10px; font-weight:bold;'>Contraseña asignada:</td><td style='padding:6px 10px; font-weight:bold; color:#0f4c81;'>{clave}</td></tr>
                        </table>
                        <p style='margin-top:16px; font-size:12px; color:#6b7f92;'>Este es un mensaje automático generado por SIGEF.</p>
                    </div>"
            };

            mensaje.To.Add(CorreoNotificacion);

            using var smtpClient = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(SmtpRemitente, SmtpPassword)
            };

            await smtpClient.SendMailAsync(mensaje);
        }
    }
}
