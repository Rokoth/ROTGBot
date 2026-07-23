using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages
{
    public class LogOutModel(IUserDataService userDataService) : PageModel
    {
        private readonly IUserDataService _userDataService = userDataService;
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
            var user = HttpContext.User?.Identity;

            if(user?.IsAuthenticated != true)
            {
                return RedirectToPage("./Index");
            }

            if(!Guid.TryParse(user?.Name, out Guid userId))
            {
                return RedirectToPage("./Index");
            }            

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (IsError)
            {
                return Page();
            }

            await _userDataService.ClearPassword(userId, new CancellationToken());

            return RedirectToPage("./Index");
        }
    }
}
