using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.News
{
    public class AnswerNewsModel : PageModel
    {
        private readonly ILogger<AnswerNewsModel> _logger;
        private readonly INewsDataService _newsDataService;

        public AnswerNewsModel(ILogger<AnswerNewsModel> logger, INewsDataService newsDataService)
        {

        }

        public Contract.Model.News News { get; set; } = default!;

        public string Answer { get; set; } = default!;


        public async Task<IActionResult> OnGetAsync()
        {

        }

        public async Task<IActionResult> OnPostAsync()
        {
        }
    }
}
