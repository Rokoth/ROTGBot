using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ROTGBot.Pages
{
    public class LogOutModel : PageModel
    {
        public string Error { get; set; } = default!;
        public string Login { get; set; } = default!;
        public bool IsLogged { get; set; } = false;

        public bool IsError { get; set; } = false;

        public async Task<IActionResult> OnGetAsync()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (IsError)
            {
                return Page();
            }
            return RedirectToPage("./Index");
        }
    }
}
