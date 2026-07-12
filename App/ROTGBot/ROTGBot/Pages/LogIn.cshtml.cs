using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;
using System.Security.Claims;

namespace ROTGBot.Pages
{
    public class LogInModel(IUserDataService userDataService, ITelegramMessageHandler telegramMessageHandler) : PageModel
    {
        private readonly IUserDataService _userDataService = userDataService;
        private readonly ITelegramMessageHandler _telegramMessageHandler = telegramMessageHandler;

        public string Error { get; set; } = default!;
        public string Login { get; set; } = default!;
        public string Password { get; set; } = default!;
        public bool LoginSended { get; set; } = false;
        public bool PasswordSended { get; set; } = false;
        public bool IsAuth { get; set; } = false;
        public bool IsError { get; set; } = false;

        public async Task<IActionResult> OnGetAsync()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if(!LoginSended)
            {
                LoginSended = true;
                (bool success, string result) = await _telegramMessageHandler.CreateAndSendPassword(Login, new CancellationToken());
                if (!success)
                {
                    IsError = true;
                    LoginSended = false;
                    PasswordSended = false;
                    Error = $"Ќеверный логин: {result}";
                }
            }
            else if(!PasswordSended)
            {
                var identity = await GetIdentity();
                PasswordSended = true;
                if(identity == null)
                {
                    IsError = true;
                    LoginSended = false;
                    PasswordSended = false;
                    Error = $"Ќеверный логин или пароль";
                }
                else
                {
                    return RedirectToAction("Index");
                }
            }            
           
            return Page();
        }

        private async Task<ClaimsIdentity?> GetIdentity()
        {
            var user = await _userDataService.GetUser(Login, Password, new CancellationToken());

            if (user == null)
            {
                // если пользовател€/клиента не найдено
                return null;
            }

            var claims = new List<Claim>
                {
                    new Claim(ClaimsIdentity.DefaultNameClaimType, user.Id.ToString()),
                    new Claim(ClaimsIdentity.DefaultRoleClaimType, "User")
                };
            ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, "Cookies",
                ClaimsIdentity.DefaultNameClaimType,
                ClaimsIdentity.DefaultRoleClaimType);
            return claimsIdentity;
        }
    }
     
}
