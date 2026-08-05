using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SIGEF.Pages
{
    public class MenuModel : PageModel
    {
        public string NombreUsuario { get; set; }
        public string RolUsuario { get; set; }
        public string DocumentoUsuario { get; set; }

        public void OnGet()
        {
            // Si no hay sesión activa, regresa al login
            var documento = HttpContext.Session.GetString("SigefDocumento");
            if (string.IsNullOrEmpty(documento))
            {
                Response.Redirect("/Index");
                return;
            }

            DocumentoUsuario = documento;
            NombreUsuario = HttpContext.Session.GetString("SigefNombre") ?? "Usuario";
            RolUsuario = HttpContext.Session.GetString("SigefRol") ?? "Sin rol";
        }

        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Index");
        }
    }
}
