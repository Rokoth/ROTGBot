using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ROTGBot.Pages.User
{
    public class IndexModel : PageModel
    {
        public IndexModel()
        {

        }

        [BindProperty]
        public List<Contract.Model.User> Users { get; set; } = [];

        public async Task<IActionResult> OnGetAsync()
        {

        }
    }
}
