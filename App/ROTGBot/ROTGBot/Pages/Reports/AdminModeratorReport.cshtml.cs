using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Contract.Model;
using ROTGBot.Service;

namespace ROTGBot.Pages.Reports
{
    public class AdminModeratorReportModel(ILogger<AdminModeratorReportModel> logger, INewsDataService newsDataService) : PageModel
    {
        private readonly ILogger<AdminModeratorReportModel> _logger = logger;
        private readonly INewsDataService _newsDataService = newsDataService;

        [BindProperty]
        public Report Report { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            Report = await _newsDataService.GetAdminModeratorReport(new CancellationToken());

            return Page();
        }
    }
}
