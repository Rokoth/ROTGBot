using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class DetailsModel : PageModel
    {
        private readonly IUserDataService _userDataService;
        private readonly ILogger<UnblockUserModel> _logger;

        public DetailsModel(IUserDataService userDataService, ILogger<UnblockUserModel> logger)
        {
            _userDataService = userDataService;
            _logger = logger;
        }

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
            if (user == null)
                return NotFound();

            UserModel = user;

            return Page();
        }
    }
}
