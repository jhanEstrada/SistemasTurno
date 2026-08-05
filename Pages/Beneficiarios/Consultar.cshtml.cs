using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace SIGEF.Pages.Beneficiarios
{
    public class ConsultarModel : PageModel
    {
        // TODO: mover a appsettings.json / IConfiguration en lugar de dejarla fija aquí.
        private const string ConnectionString =
            "Host=145.223.120.19;Port=5432;Database=pendientes;Username=postgres;Password=L0g1f4rm4;";

        [BindProperty]
        public BusquedaInput Busqueda { get; set; } = new();

        [BindProperty]
        public BeneficiarioEditInput Input { get; set; } = new();

        public bool Encontrado { get; set; }
        public string MensajeError { get; set; }
        public string MensajeExito { get; set; }

        public void OnGet()
        {
            // Página en blanco: solo el cuadro de búsqueda.
        }

        // ---------- Handler: Buscar beneficiario ----------
        public async Task<IActionResult> OnPostBuscarAsync()
        {
            // Solo validamos los campos de búsqueda en este handler
            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(Busqueda.TipoDocumento) || string.IsNullOrWhiteSpace(Busqueda.Documento))
            {
                MensajeError = "Debes indicar el tipo y número de documento para buscar.";
                return Page();
            }

            try
            {
                await using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT idbeneficiario, tipodocumento, documento, primernombre, segundonombre,
                           primerapellido, segundoapellido, fechanacimiento, sexo, telefono1,
                           telefono2, correo, direccion, barrio, municipio, departamento, eps,
                           regimen, estado
                    FROM beneficiarios
                    WHERE tipodocumento = @tipo AND documento = @documento
                    LIMIT 1;";

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("tipo", Busqueda.TipoDocumento.Trim());
                cmd.Parameters.AddWithValue("documento", Busqueda.Documento.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    Encontrado = false;
                    MensajeError = "No se encontró ningún beneficiario con ese tipo y número de documento.";
                    return Page();
                }

                Input = new BeneficiarioEditInput
                {
                    IdBeneficiario = reader.GetInt32(reader.GetOrdinal("idbeneficiario")),
                    TipoDocumento = reader.GetString(reader.GetOrdinal("tipodocumento")),
                    Documento = reader.GetString(reader.GetOrdinal("documento")),
                    PrimerNombre = reader.IsDBNull(reader.GetOrdinal("primernombre")) ? null : reader.GetString(reader.GetOrdinal("primernombre")),
                    SegundoNombre = reader.IsDBNull(reader.GetOrdinal("segundonombre")) ? null : reader.GetString(reader.GetOrdinal("segundonombre")),
                    PrimerApellido = reader.IsDBNull(reader.GetOrdinal("primerapellido")) ? null : reader.GetString(reader.GetOrdinal("primerapellido")),
                    SegundoApellido = reader.IsDBNull(reader.GetOrdinal("segundoapellido")) ? null : reader.GetString(reader.GetOrdinal("segundoapellido")),
                    FechaNacimiento = reader.IsDBNull(reader.GetOrdinal("fechanacimiento")) ? null : reader.GetDateTime(reader.GetOrdinal("fechanacimiento")),
                    Sexo = reader.IsDBNull(reader.GetOrdinal("sexo")) ? null : reader.GetString(reader.GetOrdinal("sexo")),
                    Telefono1 = reader.IsDBNull(reader.GetOrdinal("telefono1")) ? null : reader.GetString(reader.GetOrdinal("telefono1")),
                    Telefono2 = reader.IsDBNull(reader.GetOrdinal("telefono2")) ? null : reader.GetString(reader.GetOrdinal("telefono2")),
                    Correo = reader.IsDBNull(reader.GetOrdinal("correo")) ? null : reader.GetString(reader.GetOrdinal("correo")),
                    Direccion = reader.IsDBNull(reader.GetOrdinal("direccion")) ? null : reader.GetString(reader.GetOrdinal("direccion")),
                    Barrio = reader.IsDBNull(reader.GetOrdinal("barrio")) ? null : reader.GetString(reader.GetOrdinal("barrio")),
                    Municipio = reader.IsDBNull(reader.GetOrdinal("municipio")) ? null : reader.GetString(reader.GetOrdinal("municipio")),
                    Departamento = reader.IsDBNull(reader.GetOrdinal("departamento")) ? null : reader.GetString(reader.GetOrdinal("departamento")),
                    EPS = reader.IsDBNull(reader.GetOrdinal("eps")) ? null : reader.GetString(reader.GetOrdinal("eps")),
                    Regimen = reader.IsDBNull(reader.GetOrdinal("regimen")) ? null : reader.GetString(reader.GetOrdinal("regimen")),
                    Estado = reader.GetBoolean(reader.GetOrdinal("estado"))
                };

                Encontrado = true;
                // Conservamos los criterios de búsqueda para que el cuadro no quede vacío
                Busqueda.TipoDocumento = Input.TipoDocumento;
                Busqueda.Documento = Input.Documento;
            }
            catch (Exception ex)
            {
                MensajeError = "Ocurrió un error al buscar el beneficiario. Intenta nuevamente.";
                Console.WriteLine(ex); // TODO: reemplazar por ILogger inyectado
            }

            return Page();
        }

        // ---------- Handler: Guardar cambios ----------
        public async Task<IActionResult> OnPostGuardarAsync()
        {
            // En este handler solo nos interesa validar Input, no Busqueda
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Busqueda.")).ToList())
            {
                ModelState.Remove(key);
            }

            if (Input.IdBeneficiario <= 0)
            {
                MensajeError = "No se identificó el beneficiario a modificar. Vuelve a buscarlo.";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                var errores = ModelState
                    .Where(kvp => kvp.Value.Errors.Count > 0)
                    .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value.Errors.Select(e => e.ErrorMessage))}");

                MensajeError = "Revisa los siguientes campos: " + string.Join(" | ", errores);
                Encontrado = true;
                return Page();
            }

            try
            {
                await using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                // --- Evitar duplicar el documento con OTRO beneficiario ---
                const string sqlDuplicado = @"
                    SELECT 1 FROM beneficiarios
                    WHERE documento = @documento AND idbeneficiario <> @id
                    LIMIT 1;";

                await using (var cmdDup = new NpgsqlCommand(sqlDuplicado, conn))
                {
                    cmdDup.Parameters.AddWithValue("documento", Input.Documento.Trim());
                    cmdDup.Parameters.AddWithValue("id", Input.IdBeneficiario);
                    var duplicado = await cmdDup.ExecuteScalarAsync();
                    if (duplicado != null)
                    {
                        MensajeError = "Ya existe otro beneficiario registrado con ese número de documento.";
                        Encontrado = true;
                        return Page();
                    }
                }

                int? edad = Input.FechaNacimiento.HasValue ? CalcularEdad(Input.FechaNacimiento.Value) : null;

                string nombreCompleto = string.Join(" ", new[]
                {
                    Input.PrimerNombre,
                    Input.SegundoNombre,
                    Input.PrimerApellido,
                    Input.SegundoApellido
                });
                nombreCompleto = System.Text.RegularExpressions.Regex.Replace(nombreCompleto ?? "", @"\s+", " ").Trim();

                const string sqlUpdate = @"
                    UPDATE beneficiarios SET
                        tipodocumento = @tipoDocumento,
                        documento = @documento,
                        primernombre = @primerNombre,
                        segundonombre = @segundoNombre,
                        primerapellido = @primerApellido,
                        segundoapellido = @segundoApellido,
                        nombrecompleto = @nombreCompleto,
                        fechanacimiento = @fechaNacimiento,
                        edad = @edad,
                        sexo = @sexo,
                        telefono1 = @telefono1,
                        telefono2 = @telefono2,
                        correo = @correo,
                        direccion = @direccion,
                        barrio = @barrio,
                        municipio = @municipio,
                        departamento = @departamento,
                        eps = @eps,
                        regimen = @regimen,
                        estado = @estado,
                        fechaactualizacion = now()
                    WHERE idbeneficiario = @id;";

                await using var cmdUpdate = new NpgsqlCommand(sqlUpdate, conn);
                cmdUpdate.Parameters.AddWithValue("tipoDocumento", (object)Input.TipoDocumento ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("documento", Input.Documento.Trim());
                cmdUpdate.Parameters.AddWithValue("primerNombre", (object)Input.PrimerNombre ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("segundoNombre", (object)Input.SegundoNombre ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("primerApellido", (object)Input.PrimerApellido ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("segundoApellido", (object)Input.SegundoApellido ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("nombreCompleto", (object)nombreCompleto ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("fechaNacimiento", (object)Input.FechaNacimiento ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("edad", (object)edad ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("sexo", (object)Input.Sexo ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("telefono1", (object)Input.Telefono1 ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("telefono2", (object)Input.Telefono2 ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("correo", (object)Input.Correo ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("direccion", (object)Input.Direccion ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("barrio", (object)Input.Barrio ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("municipio", (object)Input.Municipio ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("departamento", (object)Input.Departamento ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("eps", (object)Input.EPS ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("regimen", (object)Input.Regimen ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("estado", Input.Estado);
                cmdUpdate.Parameters.AddWithValue("id", Input.IdBeneficiario);

                await cmdUpdate.ExecuteNonQueryAsync();

                MensajeExito = $"Beneficiario {nombreCompleto} actualizado correctamente.";
                Encontrado = true;
                Busqueda.TipoDocumento = Input.TipoDocumento;
                Busqueda.Documento = Input.Documento;
            }
            catch (Exception ex)
            {
                MensajeError = "Ocurrió un error al actualizar el beneficiario. Intenta nuevamente.";
                Encontrado = true;
                Console.WriteLine(ex); // TODO: reemplazar por ILogger inyectado
            }

            return Page();
        }

        private static int CalcularEdad(DateTime fechaNacimiento)
        {
            var hoy = DateTime.Today;
            int edad = hoy.Year - fechaNacimiento.Year;
            if (fechaNacimiento.Date > hoy.AddYears(-edad)) edad--;
            return edad;
        }
    }

    public class BusquedaInput
    {
        [Display(Name = "Tipo de documento")]
        public string? TipoDocumento { get; set; }

        [Display(Name = "Número de documento")]
        public string? Documento { get; set; }
    }

    public class BeneficiarioEditInput
    {
        public int IdBeneficiario { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de documento.")]
        [Display(Name = "Tipo de documento")]
        public string TipoDocumento { get; set; }

        [Required(ErrorMessage = "El número de documento es obligatorio.")]
        [StringLength(20)]
        [Display(Name = "Número de documento")]
        public string Documento { get; set; }

        [Required(ErrorMessage = "El primer nombre es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Primer nombre")]
        public string PrimerNombre { get; set; }

        [StringLength(50)]
        [Display(Name = "Segundo nombre")]
        public string? SegundoNombre { get; set; }

        [Required(ErrorMessage = "El primer apellido es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Primer apellido")]
        public string PrimerApellido { get; set; }

        [StringLength(50)]
        [Display(Name = "Segundo apellido")]
        public string? SegundoApellido { get; set; }

        [Display(Name = "Fecha de nacimiento")]
        [DataType(DataType.Date)]
        public DateTime? FechaNacimiento { get; set; }

        [Display(Name = "Sexo")]
        public string? Sexo { get; set; }

        [StringLength(20)]
        [Display(Name = "Teléfono principal")]
        public string? Telefono1 { get; set; }

        [StringLength(20)]
        [Display(Name = "Teléfono secundario")]
        public string? Telefono2 { get; set; }

        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        [StringLength(150)]
        [Display(Name = "Correo electrónico")]
        public string? Correo { get; set; }

        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }

        [StringLength(100)]
        [Display(Name = "Barrio")]
        public string? Barrio { get; set; }

        [StringLength(100)]
        [Display(Name = "Municipio")]
        public string? Municipio { get; set; }

        [StringLength(100)]
        [Display(Name = "Departamento")]
        public string? Departamento { get; set; }

        [StringLength(100)]
        [Display(Name = "EPS")]
        public string? EPS { get; set; }

        [Display(Name = "Régimen")]
        public string? Regimen { get; set; }

        [Display(Name = "Activo")]
        public bool Estado { get; set; } = true;
    }
}