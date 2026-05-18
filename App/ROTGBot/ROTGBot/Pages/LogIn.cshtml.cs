using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

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
                PasswordSended = true;
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
    }
}
