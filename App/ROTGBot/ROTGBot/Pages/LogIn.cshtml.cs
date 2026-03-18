using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages
{
    public class LogInModel(IUserDataService userDataService) : PageModel
    {
        private readonly IUserDataService _userDataService = userDataService;

        public string Error { get; set; } = default!;
        public string Login { get; set; } = default!;
        public string Password { get; set; } = default!;
        public bool LoginSended { get; set; } = false;
        public bool PasswordSended { get; set; } = false;

        public async Task<IActionResult> OnGetAsync()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if(!LoginSended)
            {
                LoginSended = true;
            }
            else if(!PasswordSended)
            {
                PasswordSended = true;
            }
            else
            {

            }
            return Page();
        }
    }
}
