using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.News
{
    public class AnswerNewsModel : PageModel
    {
        private readonly ILogger<AnswerNewsModel> _logger;
        private readonly INewsDataService _newsDataService;
        private readonly ITelegramMessageHandler _messageHandler;

        public AnswerNewsModel(ILogger<AnswerNewsModel> logger, INewsDataService newsDataService, ITelegramMessageHandler messageHandler)
        {
            _logger = logger;
            _newsDataService = newsDataService;
            _messageHandler = messageHandler;
        }

        public Contract.Model.News News { get; set; } = default!;

        public string Answer { get; set; } = default!;


        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверить права

            var news = await _newsDataService.GetNewsById(id, new CancellationToken());

            if(news == null)
            {
                return NotFound("Обращение не найдено");
            }

            News = news;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth.Principal.Identity.Name))
                return RedirectToPage("/Auth");
            var userId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверить права

            var result = await _messageHandler.SendNewsAnswer(News, Answer, new CancellationToken());

            return RedirectToPage("/News/Index");
        }
    }
}
