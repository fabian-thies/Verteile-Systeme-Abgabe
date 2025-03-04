// WhiteboardPlugin/Pages/WhiteboardPage.cshtml.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WhiteboardPlugin.Pages
{
    [AllowAnonymous]
    public class WhiteboardPageModel : PageModel
    {
        public void OnGet()
        {
            // Any server-side data or logic
        }
    }
}