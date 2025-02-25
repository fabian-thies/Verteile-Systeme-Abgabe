using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ChatApp.Components.Pages;

public class AuthenticationService
{
    private readonly HttpClient _httpClient;

    public AuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> Register(Register.RegisterModel registerModel)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerModel);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> Login(Login.LoginModel loginModel)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginModel);
        return response.IsSuccessStatusCode;
    }
}
