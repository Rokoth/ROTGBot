using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.Reports
{
    public class UserReportModel(ILogger<UserReportModel> logger, INewsDataService newsDataService) : PageModel
    {
        private readonly ILogger<UserReportModel> _logger = logger;
        private readonly INewsDataService _newsDataService = newsDataService;

        public async Task<IActionResult> OnGetAsync()
        {

        }
    }
}
