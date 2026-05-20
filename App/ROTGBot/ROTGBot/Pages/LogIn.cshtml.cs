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
                    Error = $"Неверный логин: {result}";
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
                    Error = $"Неверный логин или пароль";
                }
            }
            else if(IsAuth)
            {
                return RedirectToAction("Index");
            }
            else
            {
                IsError = true;
                LoginSended = false;
                PasswordSended = false;
                Error = "Неверный логин или пароль";
            }
            return Page();
        }

        private Task<ClaimsIdentity> GetIdentity() 
        {
            if (client != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimsIdentity.DefaultNameClaimType, client.Id.ToString()),
                    new Claim(ClaimsIdentity.DefaultRoleClaimType, roleType)
                };
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, authType,
                    ClaimsIdentity.DefaultNameClaimType,
                    ClaimsIdentity.DefaultRoleClaimType);
                return claimsIdentity;
            }
            // если пользователя/клиента не найдено
            return null;
        }
    }
     
}
