using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ROTGBot.Contract.Model;
using ROTGBot.Service;

namespace ROTGBot.Pages.User
{
    public class AddUserRoleModel(IUserDataService userDataService) : PageModel
    {
        private readonly IUserDataService _userDataService = userDataService;

        public UserRole UserRole { get; set; }

        public bool IsError { get; set; } = false;
        public string Error { get; set; } = string.Empty;

        public List<SelectListItem> Roles { get; set; } = [ 
            new()
            {
                Value = Guid.Empty.ToString(),
                Disabled = true,
                Selected = true,
                Text = "Выберите роль"
            }
        ];

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");

            var currUserId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверка на роль администратора

            var roles = await _userDataService.GetRoles(new CancellationToken());

            Roles.AddRange([.. roles.Select(s => new SelectListItem()
            {
                Text = s.Description,
                Value = s.Id.ToString(),
                Selected = false,
                Disabled = false
            })]);

            var user = await _userDataService.GetUser(id, new CancellationToken());
            if (user == null)
            {                
                return NotFound("Пользователь не найден");
            }
            UserRole = new UserRole()
            {
                UserId = user.Id,
                UserName = $"{user.Name} ({user.TGLogin})",
                RoleId = Guid.Empty
            };
            return Page();
        }


        public async Task<IActionResult> OnPostAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");

            var currUserId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверка на роль администратора

            if(UserRole.RoleId == Guid.Empty)
            {
                IsError = true;
                Error = "Выберите роль";
                return Page();
            }

            var result = await _userDataService.SetRole(UserRole.UserId, UserRole.RoleId, new CancellationToken());

            return RedirectToPage("Index");
        }

    }
}
