using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class IndexModel : PageModel
    {

        private readonly ILogger<IndexModel> _logger;
        private readonly IUserDataService _userDataService;

        public IndexModel(ILogger<IndexModel> logger, IUserDataService userDataService)
        {

        }

        [BindProperty]
        public List<Contract.Model.User> Users { get; set; } = [];

        public async Task<IActionResult> OnGetAsync()
        {

        }
    }
}
