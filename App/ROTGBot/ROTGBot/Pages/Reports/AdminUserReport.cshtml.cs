using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Contract.Model;
using ROTGBot.Service;

namespace ROTGBot.Pages.Reports
{
    public class AdminUserReportModel(ILogger<UserReportModel> logger, INewsDataService newsDataService) : PageModel
    {
        private readonly ILogger<UserReportModel> _logger = logger;
        private readonly INewsDataService _newsDataService = newsDataService;

        [BindProperty]
        public AdminUserReport Report { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            Report = await _newsDataService.GetAdminUserReport(new CancellationToken());

            return Page();
        }
    }
}
