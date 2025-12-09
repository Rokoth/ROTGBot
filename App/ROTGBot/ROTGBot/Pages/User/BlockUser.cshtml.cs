using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ROTGBot.Pages.User
{
    public class BlockUserModel : PageModel
    {
        public Contract.Model.User UserModel { get; set; }

        public void OnGet()
        {
        }
    }
}
