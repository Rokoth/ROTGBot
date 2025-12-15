using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ROTGBot.Pages
{
    public class LogInModel : PageModel
    {
        public string Error { get; set; } = default!;
        public string Login { get; set; } = default!;
        public string Password { get; set; } = default!;
        public bool LoginSended { get; set; } = default!;

        public void OnGet()
        {
        }
    }
}
