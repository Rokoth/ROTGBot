using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ROTGBot.Contract.Model;
using ROTGBot.Service;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ROTGBot.Pages.Reports
{
    public class ModeratorReportModel(ILogger<UserReportModel> logger, INewsDataService newsDataService) : PageModel
    {
        private readonly ILogger<UserReportModel> _logger = logger;
        private readonly INewsDataService _newsDataService = newsDataService;

        [BindProperty]
        public ModeratorReport Report { get; set; } = default!;

        public ModeratorReportFilter Filter { get; set; } = default!;

        public List<SelectListItem> AllStates { get; set; } = [
            new()
            {
                Value = "new",
                Disabled = false,
                Selected = true,
                Text = "Новый"
            },
            new()
            {
                Value = "accepted",
                Disabled = false,
                Selected = true,
                Text = "Принят"
            },
            new()
            {
                Value = "approved",
                Disabled = false,
                Selected = true,
                Text = "Подтвержден"
            },
            new()
            {
                Value = "declined",
                Disabled = false,
                Selected = true,
                Text = "Отказан"
            }
        ];

        public async Task<IActionResult> OnGetAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            Report = await _newsDataService.GetModeratorReport(userId, Filter, new CancellationToken());

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            Report = await _newsDataService.GetModeratorReport(userId, Filter, new CancellationToken());

            return Page();
        }
    }
}
