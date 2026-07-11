using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.News
{
    public class ChangeStatusModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly INewsDataService _newsDataService;

        public ChangeStatusModel(ILogger<IndexModel> logger, INewsDataService newsDataService)
        {
            _logger = logger;
            _newsDataService = newsDataService;            
        }

        [BindProperty]
        public Contract.Model.News News { get; set; } = default!;
               
        public async Task<IActionResult> OnGet(Guid id)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            News = await _newsDataService.GetNewsById(id, new CancellationToken());

            if(News == null)
            {
                throw new ArgumentException("Новость не найдена");
            }

            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            await _newsDataService.ChangeStatus(News.Id, News.State, new CancellationToken());

            return RedirectToPage("Index");
        }
    }
}
