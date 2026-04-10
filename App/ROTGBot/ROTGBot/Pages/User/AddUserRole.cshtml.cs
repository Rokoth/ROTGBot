using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ROTGBot.Contract.Model;

namespace ROTGBot.Pages.User
{
    public class AddUserRoleModel : PageModel
    {
        public UserRole UserRole { get; set; }

        public List<SelectListItem> Roles { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!auth.Succeeded || string.IsNullOrEmpty(auth?.Principal?.Identity?.Name))
                return RedirectToPage("/Auth");

            var currUserId = Guid.Parse(auth.Principal.Identity.Name);

            //todo: проверка на роль администратора

            var user = await _userDataService.GetUser(id, new CancellationToken());
            if (user == null)
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


        public async Task<IActionResult> OnPostAsync()
        {

        }

    }
}
