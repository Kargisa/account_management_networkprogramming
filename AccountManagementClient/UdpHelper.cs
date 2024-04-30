using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace AccountManagementClient
{
    internal class UdpHelper
    {
        const int udpPort = 4355;
        private static readonly IPEndPoint udpEndpoint = new IPEndPoint(IPAddress.Loopback, udpPort);

        internal static async Task<string> LoginAsync(string username, string hash)
        {
            UdpClient udpClient = new UdpClient();
            byte[] datagram = Encoding.UTF8.GetBytes($"login#{username}#{hash}");
            await udpClient.SendAsync(datagram, udpEndpoint);

            var response = await udpClient.ReceiveAsync();
            return Encoding.UTF8.GetString(response.Buffer);
        }

        internal static async Task<string> LogoutAsync(string user, string token)
        {
            UdpClient udpClient = new UdpClient();
            byte[] datagram = Encoding.UTF8.GetBytes($"logout#{user}#{token}");
            await udpClient.SendAsync(datagram, udpEndpoint);

            var response = await udpClient.ReceiveAsync();
            return Encoding.UTF8.GetString(response.Buffer);
        }
    }
}
