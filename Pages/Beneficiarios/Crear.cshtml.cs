using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace SIGEF.Pages.Beneficiarios
{
    public class CrearModel : PageModel
    {
        // --- Misma conexión usada en el resto de SIGEF ---
        // TODO: mover a appsettings.json / IConfiguration en lugar de dejarla fija aquí.
        private const string ConnectionString =
            "Host=145.223.120.19;Port=5432;Database=pendientes;Username=postgres;Password=L0g1f4rm4;";

        [BindProperty]
        public BeneficiarioInput Input { get; set; } = new();

        public string MensajeExito { get; set; }
        public string MensajeError { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState
                    .Where(kvp => kvp.Value.Errors.Count > 0)
                    .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value.Errors.Select(e => e.ErrorMessage))}");

                MensajeError = "Revisa los siguientes campos: " + string.Join(" | ", errores);
                return Page();
            }

            try
            {
                await using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                // --- Validar que el documento no exista ya ---
                const string sqlExiste = @"SELECT 1 FROM beneficiarios WHERE documento = @documento LIMIT 1;";
                await using (var cmdExiste = new NpgsqlCommand(sqlExiste, conn))
                {
                    cmdExiste.Parameters.AddWithValue("documento", Input.Documento.Trim());
                    var existe = await cmdExiste.ExecuteScalarAsync();
                    if (existe != null)
                    {
                        MensajeError = "Ya existe un beneficiario registrado con ese número de documento.";
                        return Page();
                    }
                }

                // --- Calcular edad y nombre completo ---
                int? edad = null;
                if (Input.FechaNacimiento.HasValue)
                {
                    edad = CalcularEdad(Input.FechaNacimiento.Value);
                }

                // Se reconstruye evitando espacios dobles cuando algún campo viene vacío
                string nombreCompleto = string.Join(" ", new[]
                {
                    Input.PrimerNombre,
                    Input.SegundoNombre,
                    Input.PrimerApellido,
                    Input.SegundoApellido
                });
                nombreCompleto = System.Text.RegularExpressions.Regex.Replace(nombreCompleto ?? "", @"\s+", " ").Trim();

                const string sqlInsert = @"
                    INSERT INTO beneficiarios
                        (tipodocumento, documento, primernombre, segundonombre,
                         primerapellido, segundoapellido, nombrecompleto, fechanacimiento,
                         edad, sexo, telefono1, telefono2, correo, direccion,
                         barrio, municipio, departamento, eps, regimen, estado)
                    VALUES
                        (@tipoDocumento, @documento, @primerNombre, @segundoNombre,
                         @primerApellido, @segundoApellido, @nombreCompleto, @fechaNacimiento,
                         @edad, @sexo, @telefono1, @telefono2, @correo, @direccion,
                         @barrio, @municipio, @departamento, @eps, @regimen, TRUE);";

                await using var cmdInsert = new NpgsqlCommand(sqlInsert, conn);
                cmdInsert.Parameters.AddWithValue("tipoDocumento", (object)Input.TipoDocumento ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("documento", Input.Documento.Trim());
                cmdInsert.Parameters.AddWithValue("primerNombre", (object)Input.PrimerNombre ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("segundoNombre", (object)Input.SegundoNombre ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("primerApellido", (object)Input.PrimerApellido ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("segundoApellido", (object)Input.SegundoApellido ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("nombreCompleto", (object)nombreCompleto ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("fechaNacimiento", (object)Input.FechaNacimiento ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("edad", (object)edad ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("sexo", (object)Input.Sexo ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("telefono1", (object)Input.Telefono1 ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("telefono2", (object)Input.Telefono2 ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("correo", (object)Input.Correo ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("direccion", (object)Input.Direccion ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("barrio", (object)Input.Barrio ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("municipio", (object)Input.Municipio ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("departamento", (object)Input.Departamento ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("eps", (object)Input.EPS ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("regimen", (object)Input.Regimen ?? DBNull.Value);

                await cmdInsert.ExecuteNonQueryAsync();

                MensajeExito = $"Beneficiario {nombreCompleto} creado correctamente.";
                Input = new BeneficiarioInput(); // limpiar formulario
            }
            catch (Exception ex)
            {
                MensajeError = "Ocurrió un error al guardar el beneficiario. Intenta nuevamente.";
                // TODO: reemplazar por ILogger inyectado
                Console.WriteLine(ex);
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

    public class BeneficiarioInput
    {
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
    }
}
