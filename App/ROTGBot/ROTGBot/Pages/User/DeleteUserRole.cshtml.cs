using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ROTGBot.Contract.Model;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class DeleteUserRoleModel(ILogger<DeleteUserRoleModel> logger, IUserDataService userDataService) : PageModel
    {
        private readonly ILogger<DeleteUserRoleModel> _logger = logger;
        private readonly IUserDataService _userDataService = userDataService;

        public UserRole UserRole { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync([FromQuery]Guid userId, Guid roleId)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");

            var currUserId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверка на роль администратора

            List<UserRole> userRoles = await _userDataService.GetUserRoles(userId, new CancellationToken());
            var userRole = userRoles.FirstOrDefault(s => s.RoleId == roleId);
            if(userRole == null)
            {
                return NotFound("ƒанна€ роль пользователю не назначена");
            }
            UserRole = userRole;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");

            var currUserId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверка на роль администратора

            await _userDataService.DeleteUserRole(UserRole, new CancellationToken());
            return Redirect("Details");
        }
    }
}
