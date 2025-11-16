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

        }

        public ROTGBot.Contract.Model.News News { get; set; } = default!;


        public async Task<IActionResult> OnGetAsync()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
        }
    }
}
