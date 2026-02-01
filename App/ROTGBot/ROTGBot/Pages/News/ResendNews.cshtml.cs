using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.News
{
    public class ResendNewsModel : PageModel
    {
        private readonly ILogger<ResendNewsModel> _logger;
        private readonly INewsDataService _newsDataService;

        public ResendNewsModel(ILogger<ResendNewsModel> logger, INewsDataService newsDataService)
        {
            _newsDataService = newsDataService;
            _logger = logger;
        }

        public ROTGBot.Contract.Model.News News { get; set; } = default!;


        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверить права на просмотр и отправку

            var result = await _newsDataService.GetNewsById(id, new CancellationToken());

            if(result == null)
            {
                return NotFound();
            }

            News = result;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);


            //todo: отправка


        }
    }
}
