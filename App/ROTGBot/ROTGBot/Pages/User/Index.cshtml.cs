using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Contract.Filters;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class IndexModel(ILogger<IndexModel> logger, IUserDataService userDataService) : PageModel
    {

        private readonly ILogger<IndexModel> _logger = logger;
        private readonly IUserDataService _userDataService = userDataService;

        [BindProperty]
        public List<Contract.Model.User> Users { get; set; } = [];

        [BindProperty]
        public Filter<Contract.Model.User> Filter { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            Users = await _userDataService.GetUsers(Filter, new CancellationToken());

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Users = await _userDataService.GetUsers(Filter, new CancellationToken());

            return Page();
        }
    }
}
