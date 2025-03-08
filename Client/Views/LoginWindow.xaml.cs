using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Client.Views;

public partial class LoginWindow : Window
{
    private readonly HubConnection connection;
    private readonly ILogger<LoginWindow> _logger;
    private int retrySecondsRemaining;
    private DispatcherTimer retryTimer;

    public LoginWindow()
    {
        InitializeComponent();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<LoginWindow>();

        _logger.LogInformation("Initializing LoginWindow and creating SignalR connection.");

        connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/chatHub")
            .Build();

        _logger.LogInformation("SignalR HubConnection created with URL: {Url}", "http://localhost:5000/chatHub");

        ConnectToServer();
    }

    private async void ConnectToServer()
    {
        _logger.LogInformation("Attempting to connect to the server...");
        try
        {
            await connection.StartAsync();
            _logger.LogInformation("Successfully connected to the server.");

            // On success: clear error messages and hide error panel
            ErrorTextBlock.Text = "";
            RetryCountdownTextBlock.Text = "";
            ErrorPanel.Visibility = Visibility.Collapsed;
            StopRetryTimer();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to the server.");

            // Set full error message
            ErrorTextBlock.Text = "Failed to connect to server: " + ex.Message;
            // Show error panel so it occupies space
            ErrorPanel.Visibility = Visibility.Visible;
            StartRetryTimer();
        }
    }

    private void StartRetryTimer()
    {
        _logger.LogInformation("Starting retry timer.");
        retrySecondsRemaining = 10;
        RetryCountdownTextBlock.Text = "Erneuter Verbindungsversuch in " + retrySecondsRemaining + " Sekunden...";
        if (retryTimer == null)
        {
            retryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            retryTimer.Tick += RetryTimer_Tick;
        }

        retryTimer.Start();
    }

    private void StopRetryTimer()
    {
        if (retryTimer != null && retryTimer.IsEnabled)
        {
            _logger.LogInformation("Stopping retry timer.");
            retryTimer.Stop();
        }
    }

    private async void RetryTimer_Tick(object sender, EventArgs e)
    {
        retrySecondsRemaining--;
        if (retrySecondsRemaining > 0)
        {
            RetryCountdownTextBlock.Text = "Erneuter Verbindungsversuch in " + retrySecondsRemaining + " Sekunden...";
        }
        else
        {
            RetryCountdownTextBlock.Text = "Erneuter Verbindungsversuch...";
            _logger.LogInformation("Retry timer elapsed. Attempting to reconnect.");
            StopRetryTimer();
            await Task.Delay(500); // small delay before retry
            ConnectToServer();
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Login button clicked.");
        ErrorTextBlock.Text = string.Empty;
        ErrorPanel.Visibility = Visibility.Collapsed;

        var username = UsernameTextBox.Text;
        var password = PasswordBox.Password;

        try
        {
            _logger.LogInformation("Attempting login for user: {Username}", username);
            var isAuthenticated = await connection.InvokeAsync<bool>("Login", username, password);
            if (isAuthenticated)
            {
                _logger.LogInformation("User {Username} authenticated successfully.", username);
                var mainWindow = new MainWindow(connection);
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
            else
            {
                _logger.LogWarning("Authentication failed for user: {Username}", username);
                ErrorTextBlock.Text = "Invalid username or password.";
                ErrorPanel.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during login for user: {Username}", username);
            ErrorTextBlock.Text = "An error occurred: " + ex.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Register button clicked. Opening RegisterWindow.");
        var registerWindow = new RegisterWindow();
        registerWindow.Show();
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _logger.LogInformation("Dragging the window.");
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Close button clicked. Closing LoginWindow.");
        Close();
    }
}