// ChatServer.cs

using System.Net;
using System.Net.Sockets;
using System.Text;
using Server.Database;

namespace Server
{
    public class ChatServer
    {
        private TcpListener _listener;
        private bool _isRunning;
        private List<ClientHandler> _connectedClients = new List<ClientHandler>();

        public void StartServer(string ip, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(ip), port);
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"Server started on {ip}:{port}");

            Task.Run(() => AcceptClients());
        }

        private async Task AcceptClients()
        {
            while (_isRunning)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                ClientHandler handler = new ClientHandler(tcpClient, this);
                lock (_connectedClients)
                {
                    _connectedClients.Add(handler);
                }

                Task.Run(() => handler.HandleClientAsync());
            }
        }

        public void BroadcastMessage(string message)
        {
            lock (_connectedClients)
            {
                foreach (var client in _connectedClients)
                {
                    client.SendMessage(message);
                }
            }
        }

        public void RemoveClient(ClientHandler client)
        {
            lock (_connectedClients)
            {
                if (_connectedClients.Contains(client))
                {
                    _connectedClients.Remove(client);
                }
            }
        }
    }

    public class ClientHandler
    {
        private TcpClient _client;
        private ChatServer _server;
        private NetworkStream _stream;
        private string _username;
        private bool _isAuthenticated = false;

        public ClientHandler(TcpClient client, ChatServer server)
        {
            _client = client;
            _server = server;
        }

        public async Task HandleClientAsync()
        {
            _stream = _client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            try
            {
                while ((bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"Received: {message} from {_client.Client.RemoteEndPoint}");

                    if (message.StartsWith("/login"))
                    {
                        string[] parts = message.Split(' ', 3);
                        if (parts.Length < 3)
                        {
                            await _stream.WriteAsync(Encoding.UTF8.GetBytes("Invalid login command.\n"));
                            continue;
                        }

                        string username = parts[1];
                        string password = parts[2];

                        if (await AuthHelper.LoginUserAsync(username, password))
                        {
                            _isAuthenticated = true;
                            _username = username;
                            await _stream.WriteAsync(Encoding.UTF8.GetBytes("Login successful!\n"));
                        }
                        else
                        {
                            await _stream.WriteAsync(Encoding.UTF8.GetBytes("Login failed. Check your credentials.\n"));
                        }

                        continue;
                    }
                    else if (message.StartsWith("/register"))
                    {
                        string[] parts = message.Split(' ', 3);
                        if (parts.Length < 3)
                        {
                            await _stream.WriteAsync(Encoding.UTF8.GetBytes("Invalid register command.\n"));
                            continue;
                        }

                        string username = parts[1];
                        string password = parts[2];

                        if (await AuthHelper.RegisterUserAsync(username, password))
                        {
                            await _stream.WriteAsync(Encoding.UTF8.GetBytes("Registration successful!\n"));
                        }
                        else
                        {
                            await _stream.WriteAsync(
                                Encoding.UTF8.GetBytes("Registration failed. Username might already exist.\n"));
                        }

                        continue;
                    }

                    // 3) If not authenticated, respond or ignore
                    if (!_isAuthenticated)
                    {
                        await _stream.WriteAsync(Encoding.UTF8.GetBytes("You must be logged in to send messages.\n"));
                        continue;
                    }

                    // 4) Normal chat message
                    string fullMessage = $"{_username}: {message}";
                    _server.BroadcastMessage(fullMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                _stream.Close();
                _client.Close();
                _server.RemoveClient(this);
            }
        }

        public void SendMessage(string message)
        {
            if (_stream != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                _stream.WriteAsync(data, 0, data.Length);
            }
        }
    }
}