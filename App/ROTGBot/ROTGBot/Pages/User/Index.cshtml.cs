using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Contract.Filters;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class IndexModel(ILogger<IndexModel> logger, IUserDataService userDataService) : PageModel
    {

        private readonly ILogger<IndexModel> _logger = logger;
        private readonly IUserDataService _userDataService = userDataService;

        [BindProperty]
        public List<Contract.Model.User> Users { get; set; } = [];

        [BindProperty]
        public Filter<Contract.Model.User> Filter { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");

            var currUserId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверка на роль администратора

            Users = await _userDataService.GetUsers(Filter, new CancellationToken());

            return Page();
        }
    }
}
