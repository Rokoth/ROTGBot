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



        public string Error { get; set; }
        public LoginModel LoginModel { get; set; } = new LoginModel()
        {
            Login="1",
            Password=""
        };
        public bool LoginSended { get; set; }
        public bool PasswordSended { get; set; }
        public bool IsAuth { get; set; }
        public bool IsError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {            
            IsError = false;
            IsAuth = false;
            LoginSended = false;
            PasswordSended= false;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if(!LoginSended)
            {
                LoginSended = true;
                (bool success, string result) = await _telegramMessageHandler.CreateAndSendPassword(LoginModel.Login, new CancellationToken());
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
            var user = await _userDataService.GetUser(LoginModel.Login, LoginModel.Password, new CancellationToken());

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

    public class LoginModel
    {
        public  string   Login { get; set; }
        public string Password { get; set; }
    }
     
}
