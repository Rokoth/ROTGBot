using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class UnblockUserModel(IUserDataService userDataService, ILogger<UnblockUserModel> logger) : PageModel
    {
        private readonly IUserDataService _userDataService = userDataService;
        private readonly ILogger<UnblockUserModel> _logger = logger;

        public Contract.Model.User UserModel { get; set; } = default!;
        public string Error { get; set; } = default!;
        public bool IsError { get; set; } = false;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");

            var currUserId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверка на роль администратора

            var user = await _userDataService.GetUser(id, new CancellationToken());
            if(user == null)
            {
                return NotFound("Пользователь не найден");
            }
            UserModel = new Contract.Model.User()
            {
                Name = user.Name,
                TGLogin = user.TGLogin,
                Id = user.Id
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");

            var currUserId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверка на роль администратора

            bool result = await _userDataService.UnblockUser(UserModel.Id, new CancellationToken());

            if (result)
            {
                return RedirectToPage("Details");
            }
            IsError = true;
            Error = "Не удалось заблокировать пользователя";
            return Page();
        }
    }
}
