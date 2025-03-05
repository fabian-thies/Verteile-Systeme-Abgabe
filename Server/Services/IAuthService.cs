namespace Server.Services;

public interface IAuthService
{
    Task<bool> Login(string username, string password);
    Task<bool> Register(string username, string password);
}