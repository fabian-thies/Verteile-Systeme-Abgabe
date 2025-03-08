using System.Windows;
using System.Windows.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Client.Views;

public partial class RegisterWindow : Window
{
    private readonly HubConnection connection;
    private readonly ILogger<RegisterWindow> _logger;

    public RegisterWindow()
    {
        InitializeComponent();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<RegisterWindow>();
        _logger.LogInformation("Initializing RegisterWindow and creating SignalR connection.");

        connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/chatHub")
            .WithAutomaticReconnect()
            .Build();

        ConnectToServer();
    }

    private async void ConnectToServer()
    {
        _logger.LogInformation("Attempting to connect to the server in RegisterWindow...");
        try
        {
            await connection.StartAsync();
            _logger.LogInformation("Connected to the server successfully.");
            ErrorTextBlock.Text = "";
            ErrorPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to server.");
            ErrorTextBlock.Text = "Failed to connect to server: " + ex.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Register button clicked.");
        ErrorTextBlock.Text = "";
        ErrorPanel.Visibility = Visibility.Collapsed;

        var username = UsernameTextBox.Text;
        var password = PasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;

        if (password != confirmPassword)
        {
            _logger.LogWarning("Password and confirmation do not match for user: {Username}", username);
            ErrorTextBlock.Text = "Passwords do not match.";
            ErrorPanel.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            _logger.LogInformation("Attempting to register user: {Username}", username);
            var isRegistered = await connection.InvokeAsync<bool>("Register", username, password);
            if (isRegistered)
            {
                _logger.LogInformation("User {Username} registered successfully.", username);
                ErrorTextBlock.Text = "Registration successful!";
                ErrorPanel.Visibility = Visibility.Visible;
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                Close();
            }
            else
            {
                _logger.LogWarning("Registration failed for user: {Username}. Username might already exist.", username);
                ErrorTextBlock.Text = "Registration failed. Username might already exist.";
                ErrorPanel.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during registration for user: {Username}", username);
            ErrorTextBlock.Text = "An error occurred: " + ex.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("BackToLogin button clicked. Navigating to LoginWindow.");
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _logger.LogInformation("Dragging RegisterWindow via title bar.");
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Close button clicked in RegisterWindow. Closing window.");
        Close();
    }
}
