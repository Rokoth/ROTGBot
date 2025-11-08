using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Db.Context;
using ROTGBot.Service;

namespace ROTGBot.Pages.News
{
    public class IndexModel(ILogger<IndexModel> logger, INewsDataService newsDataService) : PageModel
    {
        private readonly ILogger<IndexModel> _logger = logger;
        private readonly INewsDataService _newsDataService = newsDataService;

        [BindProperty]
        public List<Contract.Model.News> News { get; set; } = default!;

        [BindProperty]
        public List<Contract.Filters.NewsFilter> News { get; set; } = default!;

        public async Task<IActionResult> OnGet()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            News = _newsDataService.GetNewsByFilter();

            return Page();
        }
    }
}
