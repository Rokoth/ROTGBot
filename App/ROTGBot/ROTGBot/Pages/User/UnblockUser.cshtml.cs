using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class UnblockUserModel(IUserDataService userDataService, ILogger<UnblockUserModel> logger) : PageModel
    {
        private readonly IUserDataService _userDataService = userDataService;
        private readonly ILogger<UnblockUserModel> _logger = logger;

        public Contract.Model.User UserModel { get; set; } = default!;
        public string Error { get; set; } = default!;
        public bool IsError { get; set; } = false;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userDataService.GetUser(id, new CancellationToken());
            if(user == null)
            {
                IsError = true;
                return Page();
            }
            UserModel = new Contract.Model.User()
            {
                Name = user.Name,
                TGLogin = user.TGLogin
            };
            return Page();
        }
    }
}
