using System.Net.Sockets;
using System.Text;

namespace Client
{
    public class ChatClient
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;

        /// <summary>
        /// Connects to the chat server.
        /// </summary>
        public bool Connect(string ip, int port)
        {
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.Connect(ip, port);
                _stream = _tcpClient.GetStream();
                // You might want to start a reading loop here (e.g. Task.Run(ReceiveMessages)).
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sends a message to the server.
        /// </summary>
        public void SendMessage(string message)
        {
            if (_stream != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                _stream.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Continuously receives messages from the server.
        /// </summary>
        public async Task ReceiveMessages(Action<string> onMessageReceived)
        {
            byte[] buffer = new byte[1024];
            int bytesRead;

            while (true)
            {
                try
                {
                    bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) 
                    {
                        // Server disconnected
                        break;
                    }
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    onMessageReceived?.Invoke(message);
                }
                catch (Exception)
                {
                    // Handle exceptions or disconnections
                    break;
                }
            }
        }

        /// <summary>
        /// Disconnects the client from the server.
        /// </summary>
        public void Disconnect()
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
    }
}
