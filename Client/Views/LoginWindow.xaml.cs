using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Views
{
    public partial class LoginWindow : Window
    {
        private readonly HubConnection connection;
        private DispatcherTimer retryTimer;
        private int retrySecondsRemaining = 60;

        public LoginWindow()
        {
            InitializeComponent();
            connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/chatHub")
                .Build();

            ConnectToServer();
        }

        private async void ConnectToServer()
        {
            try
            {
                await connection.StartAsync();
                // On success: clear error and hide error panel
                ErrorTextBlock.Text = "";
                RetryCountdownTextBlock.Text = "";
                ErrorPanel.Visibility = Visibility.Collapsed;
                StopRetryTimer();
            }
            catch (Exception ex)
            {
                // Set full error message
                ErrorTextBlock.Text = "Failed to connect to server: " + ex.Message;
                // Show error panel so it takes Platz
                ErrorPanel.Visibility = Visibility.Visible;
                StartRetryTimer();
            }
        }

        private void StartRetryTimer()
        {
            retrySecondsRemaining = 60;
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
                StopRetryTimer();
                await System.Threading.Tasks.Task.Delay(500); // small delay before retry
                ConnectToServer();
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Text = string.Empty;
            ErrorPanel.Visibility = Visibility.Collapsed;

            var username = UsernameTextBox.Text;
            var password = PasswordBox.Password;

            try
            {
                var isAuthenticated = await connection.InvokeAsync<bool>("Login", username, password);
                if (isAuthenticated)
                {
                    var mainWindow = new MainWindow(connection);
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    ErrorTextBlock.Text = "Invalid username or password.";
                    ErrorPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ErrorTextBlock.Text = "An error occurred: " + ex.Message;
                ErrorPanel.Visibility = Visibility.Visible;
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = new RegisterWindow();
            registerWindow.Show();
            Close();
        }

        // Allows dragging via the title bar.
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // Closes the window when the close button is clicked.
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
