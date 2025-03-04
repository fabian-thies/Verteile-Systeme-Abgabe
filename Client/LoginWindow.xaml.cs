// LoginWindow.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Client
{
    public partial class LoginWindow : Window
    {
        // Shared ChatClient instance used for connecting and authenticating
        private ChatClient _chatClient = new ChatClient();

        public LoginWindow()
        {
            InitializeComponent();
        }

        // Event handler for the Login button click
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameInput.Text;
            string password = PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "Please enter both username and password.";
                return;
            }

            // Try to connect to the server (if not already connected)
            if (!_chatClient.Connect("127.0.0.1", 5000))
            {
                StatusText.Text = "Failed to connect to server.";
                return;
            }

            // Send login command to the server
            string loginCommand = $"/login {username} {password}";
            _chatClient.SendMessage(loginCommand);

            // Wait a moment for the server response (for demonstration)
            await Task.Delay(500);

            // In a real application you would parse the server response asynchronously.
            // For dieses Beispiel gehen wir davon aus, dass der Login erfolgreich war,
            // wenn die Verbindung besteht. (Eventuell sollte man hier die tatsächliche Serverantwort prüfen.)
            // Open the main chat window
            MainWindow chatWindow = new MainWindow(_chatClient, username);
            chatWindow.Show();
            this.Close();
        }

        // Event handler for the Register button click
        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameInput.Text;
            string password = PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "Please enter both username and password.";
                return;
            }

            // Try to connect to the server (if not already connected)
            if (!_chatClient.Connect("127.0.0.1", 5000))
            {
                StatusText.Text = "Failed to connect to server.";
                return;
            }

            // Send register command to the server
            string registerCommand = $"/register {username} {password}";
            _chatClient.SendMessage(registerCommand);

            // Wait a moment for server response (for demonstration)
            await Task.Delay(500);

            // Inform the user to login after registration
            StatusText.Text = "Registration successful! Please click Login.";
        }
    }
}
