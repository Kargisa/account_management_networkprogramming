using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AccountManagementLibrary;

namespace AccountManagementClient
{
    internal class TcpHelper
    {
        const int tcpPort = 4356;
        private static IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, tcpPort);

        internal static async Task<List<User>> LoadUsersAsync()
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(ep);
            using (NetworkStream stream = client.GetStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write("getusers");
                }

                return await JsonSerializer.DeserializeAsync<List<User>>(stream);
            }
        }

        internal static async Task<ObservableCollection<Group>> LoadGroupsAsync()
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(ep);
            using (NetworkStream stream = client.GetStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write("getgroups");
                }

                return await JsonSerializer.DeserializeAsync<ObservableCollection<Group>>(stream);
            }
        }

        internal static async Task<int> ProcessAddGroupAsync(string groupname)
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(ep);
            using (NetworkStream stream = client.GetStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write("addgroup");
                    writer.Write(groupname);
                }

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string result = reader.ReadToEnd();
                    try
                    {
                        return JsonSerializer.Deserialize<int>(result);
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException(result);
                    }
                }
            }
        }

        internal static async Task<bool> ProcessAddToGroupAsync(Group group, User user)
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(ep);
            using (NetworkStream stream = client.GetStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write("addtogroup");
                    writer.Write(group.Gid);
                    writer.Write(user.Uid);
                }

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string result = reader.ReadToEnd();
                    try
                    {
                        return JsonSerializer.Deserialize<bool>(result);
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException(result);
                    }
                }
            }
        }

        internal static async Task<bool> ProcessRemoveGroupAsync(Group group)
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(ep);
            using (NetworkStream stream = client.GetStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write("removegroup");
                    writer.Write(group.Gid);
                }

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string result = await reader.ReadToEndAsync();
                    try
                    {
                        return JsonSerializer.Deserialize<bool>(result);
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException(result);
                    }
                }
            }
        }

        internal static async Task<bool> ProcessRemoveFromGroupAsync(Group group, User user)
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(ep);
            using (NetworkStream stream = client.GetStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write("removefromgroup");
                    writer.Write(group.Gid);
                    writer.Write(user.Uid);
                }

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string result = reader.ReadToEnd();
                    try
                    {
                        return JsonSerializer.Deserialize<bool>(result);
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException(result);
                    }
                }
            }
        }
    }
}
