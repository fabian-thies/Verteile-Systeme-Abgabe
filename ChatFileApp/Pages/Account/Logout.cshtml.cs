using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatFileApp.Pages.Account;

public class Logout : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;

    public Logout(SignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
        SignOutUserAsync().GetAwaiter().GetResult();
    }

    public void OnGet()
    {
        Response.Redirect("/");
    }

    private async Task SignOutUserAsync()
    {
        await _signInManager.SignOutAsync();
    }
}