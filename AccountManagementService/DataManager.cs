using System;
using System.Collections.Generic;
using System.IO;
using AccountManagementLibrary;

namespace AccountManagementService
{
    internal class DataManager
    {
        private static readonly string usersFile = "users.csv";
        private static readonly string groupsFile = "groups.csv";
        private static readonly string groupMembersFile = "memberships.csv";

        private static readonly Dictionary<int, User> users = new();
        private static readonly List<Group> groups = new();

        internal static Dictionary<int, User> UserData
        {
            get
            {
                // TODO: load user data (but only once)
                if (users.Count > 0)
                    return users;

                string[] lines = File.ReadAllLines(usersFile);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] split = lines[i].Split(';');
                    users.Add(int.Parse(split[0]), new User(int.Parse(split[0]), split[1], split[2], split[3], split[4]));
                }
                return users;
            }
        }

        internal static List<Group> GroupData
        {
            get
            {
                if (groups.Count > 0)
                    return groups;

                string[] groupLines = File.ReadAllLines(groupsFile);
                for (int i = 1; i < groupLines.Length; i++)
                {
                    string[] split = groupLines[i].Split(';');
                    groups.Add(new Group(int.Parse(split[0]), split[1]));
                }

                string[] membershipLines = File.ReadAllLines(groupMembersFile);
                for (int i = 1; i < membershipLines.Length; i++)
                {
                    string[] split = membershipLines[i].Split(';');
                    Tuple<int, int> tuple = new Tuple<int, int>(int.Parse(split[0]), int.Parse(split[1]));
                    groups.Find(g => g.Gid == tuple.Item1).Users.Add(UserData[tuple.Item2]);
                }

                return groups;
            }
        }
    }
}
