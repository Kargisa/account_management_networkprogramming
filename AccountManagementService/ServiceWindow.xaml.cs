using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using AccountManagementLibrary;

namespace AccountManagementService
{
    public partial class MainWindow : Window
    {
        const int udpPort = 4355;
        const int tcpPort = 4356;

        private readonly ObservableCollection<UserWithToken> loggedInUsers;

        private readonly ChartModels chartModels;

        private object userLock = new object();
        private object groupLock = new object();
        private object groupMemberLock = new object();

        public MainWindow()
        {
            InitializeComponent();

            loggedInUsers = new ObservableCollection<UserWithToken>();
            lbLoggedInUsers.ItemsSource = loggedInUsers;

            // Show the current data
            chartModels = new ChartModels(pie, DataManager.GroupData, bar, DataManager.UserData);

            // TODO: start the services
            _ = Task.Run(async () => await StartUDPService());
            _ = Task.Run(async () => await StartTCPService());
        }

        private async Task StartUDPService()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, udpPort);
            UdpClient client = new UdpClient(ep);

            while (true)
            {
                var result = await client.ReceiveAsync();
                _ = Task.Factory.StartNew(async (stat) =>
                {
                    var taskResult = (UdpReceiveResult)stat!;
                    byte[] buffer = taskResult.Buffer;
                    string stringData = Encoding.UTF8.GetString(buffer);
                    string[] split = stringData.Split('#');
                    if (split[0] == "login")
                    {
                        var user = DataManager.UserData.FirstOrDefault(u => u.Value.Login == split[1] && u.Value.PasswordHash == split[2]).Value;
                        if (user == null)
                        {
                            _ = await client.SendAsync(Encoding.UTF8.GetBytes("failed"), taskResult.RemoteEndPoint);
                            return;
                        }

                        string token = Guid.NewGuid().ToString();
                        Dispatcher.Invoke(() =>
                        {
                            lock (userLock)
                            {
                                loggedInUsers.Add(new UserWithToken(user, token));
                            }
                        });
                        _ = await client.SendAsync(Encoding.UTF8.GetBytes(token), taskResult.RemoteEndPoint);
                    }
                    else if (split[0] == "logout")
                    {
                        UserWithToken user = null;
                        Dispatcher.Invoke(() =>
                        {
                            lock (userLock)
                            {
                                user = loggedInUsers.FirstOrDefault(u => u.User.Login == split[1] && u.Token == split[2]);
                            }
                        });
                        if (user == null)
                        {
                            _ = await client.SendAsync(Encoding.UTF8.GetBytes("failed"), taskResult.RemoteEndPoint);
                            return;
                        }
                        Dispatcher.Invoke(() =>
                        {
                            lock (userLock)
                            {
                                loggedInUsers.Remove(user);
                            }
                        });
                        _ = await client.SendAsync(Encoding.UTF8.GetBytes("ok"), taskResult.RemoteEndPoint);
                    }

                }, result);
            }
        }

        private async Task StartTCPService()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, tcpPort);
            TcpListener tcpListener = new TcpListener(ep);
            tcpListener.Start();

            while (true)
            {
                var client = await tcpListener.AcceptTcpClientAsync();
                _ = Task.Factory.StartNew(async (stat) =>
                {
                    var taskClient = (TcpClient)stat!;
                    using (NetworkStream stream = client.GetStream())
                    {
                        string operation;
                        using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

                        operation = reader.ReadString();

                        switch (operation)
                        {
                            case "getusers":
                                List<User> users;
                                lock (groupLock)
                                {
                                    users = DataManager.UserData.OrderBy(u => u.Value.Lastname).ThenBy(u => u.Value.Firstname).Select(u => u.Value).ToList();
                                }
                                await JsonSerializer.SerializeAsync(stream, users);
                                break;
                            case "getgroups":
                                ObservableCollection<Group> collection;
                                lock (groupLock)
                                {
                                    collection = new ObservableCollection<Group>(DataManager.GroupData.OrderBy(u => u.Name));
                                }
                                await JsonSerializer.SerializeAsync(stream, collection);
                                break;
                            case "addgroup":
                                string newGroupName = reader.ReadString();
                                try
                                {
                                    bool exists;
                                    lock (groupLock)
                                    {
                                        exists = DataManager.GroupData.Any(g => g.Name == newGroupName);
                                    }
                                    if (exists)
                                        throw new InvalidOperationException("Group with name: " + newGroupName + " already exists!");
                                }
                                catch (InvalidOperationException e)
                                {
                                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                    {
                                        writer.AutoFlush = true;
                                        await writer.WriteAsync(e.Message);
                                    }
                                    return;
                                }

                                Group newGroup;
                                lock (groupLock)
                                {
                                    newGroup = new Group(DataManager.GroupData.Count, newGroupName);
                                    DataManager.GroupData.Add(newGroup);
                                }
                                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                {
                                    writer.AutoFlush = true;
                                    string respose = JsonSerializer.Serialize(newGroup.Gid);
                                    await writer.WriteAsync(respose);
                                }
                                break;
                            case "removegroup":
                                int gid = reader.ReadInt32();
                                Group deleteGroup;
                                lock (groupLock)
                                {
                                    deleteGroup = DataManager.GroupData.FirstOrDefault(g => g.Gid == gid);
                                }
                                try
                                {
                                    if (deleteGroup != null)
                                        throw new InvalidOperationException("Can not delete group with id " + gid + " because it does not exist!");
                                }
                                catch (InvalidOperationException e)
                                {
                                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                    {
                                        writer.AutoFlush = true;
                                        await writer.WriteAsync(e.Message);
                                    }
                                    return;
                                }

                                lock (groupLock)
                                {
                                    DataManager.GroupData.Remove(deleteGroup);
                                }
                                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                {
                                    writer.AutoFlush = true;
                                    string respose = JsonSerializer.Serialize(true);
                                    await writer.WriteAsync(respose);
                                }

                                break;
                            case "addtogroup":
                                int gidMember = reader.ReadInt32();
                                int uidMember = reader.ReadInt32();

                                Group group;
                                lock (groupLock)
                                {
                                    group = DataManager.GroupData.FirstOrDefault(g => g.Gid == gidMember);
                                }

                                try
                                {
                                    if (group == null)
                                        throw new InvalidOperationException("No group with gid: " + gidMember + " found");
                                }
                                catch (Exception e)
                                {
                                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                    {
                                        writer.AutoFlush = true;
                                        await writer.WriteAsync(e.Message);
                                    }
                                    return;
                                }

                                User user;
                                lock (groupLock)
                                {
                                    user = DataManager.UserData.FirstOrDefault(u => u.Value.Uid == uidMember).Value;
                                }

                                try
                                {
                                    if (user == null)
                                        throw new InvalidOperationException("No user with uid: " + uidMember + " found");
                                }
                                catch (Exception e)
                                {
                                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                    {
                                        writer.AutoFlush = true;
                                        await writer.WriteAsync(e.Message);
                                    }
                                    return;
                                }

                                lock (groupMemberLock)
                                {
                                    group.Users.Add(user);
                                }

                                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                {
                                    writer.AutoFlush = true;
                                    string respose = JsonSerializer.Serialize(true);
                                    await writer.WriteAsync(respose);
                                }

                                break;
                            case "removefromgroup":
                                int gidMemberRemove = reader.ReadInt32();
                                int uidMemberRemove = reader.ReadInt32();

                                Group groupRemove;
                                lock (groupLock)
                                {
                                    groupRemove = DataManager.GroupData.FirstOrDefault(g => g.Gid == gidMemberRemove);
                                }

                                try
                                {
                                    if (groupRemove == null)
                                        throw new InvalidOperationException("No group with gid: " + gidMemberRemove + " found");
                                }
                                catch (Exception e)
                                {
                                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                    {
                                        writer.AutoFlush = true;
                                        await writer.WriteAsync(e.Message);
                                    }
                                    return;
                                }

                                User userRemove;
                                lock (groupLock)
                                {
                                    userRemove = DataManager.UserData.FirstOrDefault(u => u.Value.Uid == uidMemberRemove).Value;
                                }

                                try
                                {
                                    if (userRemove == null)
                                        throw new InvalidOperationException("No user with uid: " + uidMemberRemove + " found");
                                }
                                catch (Exception e)
                                {
                                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                    {
                                        writer.AutoFlush = true;
                                        await writer.WriteAsync(e.Message);
                                    }
                                    return;
                                }

                                lock (groupMemberLock)
                                {
                                    groupRemove.Users.Remove(userRemove);
                                }

                                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                                {
                                    writer.AutoFlush = true;
                                    string respose = JsonSerializer.Serialize(true);
                                    await writer.WriteAsync(respose);
                                }

                                break;
                        }
                    }

                }, client);
            }
        }
    }
}
