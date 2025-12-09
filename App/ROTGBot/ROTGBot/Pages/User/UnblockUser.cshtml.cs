using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class UnblockUserModel : PageModel
    {
        private readonly IUserDataService _userDataService;
        private readonly ILogger<UnblockUserModel> _logger;

        public UnblockUserModel(IUserDataService userDataService, ILogger<UnblockUserModel> logger)
        {
            _userDataService = userDataService;
            _logger = logger;
        }

        public Contract.Model.User UserModel { get; set; } = default!;
        public string Error { get; set; } = default!;
        public bool IsError { get; set; } = false;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userDataService.GetUser(id, new CancellationToken());
        }
    }
}
