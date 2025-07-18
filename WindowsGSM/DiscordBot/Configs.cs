using System.Collections.Generic;
using System.IO;
using System.Linq;
using WindowsGSM.Functions;

namespace WindowsGSM.DiscordBot
{
    static class Configs
    {
		private static readonly string _botPath = ServerPath.Get(ServerPath.FolderName.Configs, "discordbot");

		public static void CreateConfigs()
		{
			Directory.CreateDirectory(_botPath);

            string namePath = Path.Combine(_botPath, "name.txt");

            if (!File.Exists(namePath))
				File.WriteAllText(namePath, "WindowsGSM");

        }

		public static string GetCommandsList()
		{
			string prefix = GetBotPrefix();
			return $"{prefix}wgsm check\n{prefix}wgsm list\n{prefix}wgsm start <SERVERID>\n{prefix}wgsm stop <SERVERID>\n{prefix}wgsm restart <SERVERID>\n{prefix}wgsm update <SERVERID>\n{prefix}wgsm send <SERVERID> <COMMAND>\n{prefix}wgsm backup <SERVERID>\n{prefix}wgsm stats";
		}

		public static string GetBotPrefix()
		{
			try
			{
				return File.ReadAllText(Path.Combine(_botPath, "prefix.txt")).Trim();
			}
			catch
			{
				return string.Empty;
			}
        }


        public static void SetBotPrefix(string prefix)
        {
            Directory.CreateDirectory(_botPath);
            File.WriteAllText(Path.Combine(_botPath, "prefix.txt"), prefix);
        }

        public static string GetBotName()
        {
            try
            {
                return File.ReadAllText(Path.Combine(_botPath, "name.txt")).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void SetBotName(string name)
        {
            Directory.CreateDirectory(_botPath);
            File.WriteAllText(Path.Combine(_botPath, "name.txt"), name);
        }

        public static FileStream GetBotCustomImage()
        {
            try
            {
                return new FileStream(Path.Combine(_botPath, "avatar.png"), FileMode.Open);
            }
            catch
            {
                return null;
            }
        }

        public static string GetBotToken()
		{
			try
			{
				return File.ReadAllText(Path.Combine(_botPath, "token.txt")).Trim();
			}
			catch
			{
				return string.Empty;
			}
		}

		public static void SetBotToken(string token)
		{
			Directory.CreateDirectory(_botPath);
			File.WriteAllText(Path.Combine(_botPath, "token.txt"), token.Trim());
		}

		public static string GetDashboardChannel()
		{
			try
			{
				return File.ReadAllText(Path.Combine(_botPath, "channel.txt")).Trim();
			}
			catch
			{
				return string.Empty;
			}
		}

		public static void SetDashboardChannel(string channel)
		{
			Directory.CreateDirectory(_botPath);
			File.WriteAllText(Path.Combine(_botPath, "channel.txt"), channel.Trim());
		}

		public static int GetDashboardRefreshRate()
		{
			try
			{
				return int.Parse(File.ReadAllText(Path.Combine(_botPath, "refreshrate.txt")).Trim());
			}
			catch
			{
				return 5;
			}
		}

		public static void SetDashboardRefreshRate(int rate)
		{
			Directory.CreateDirectory(_botPath);
			File.WriteAllText(Path.Combine(_botPath, "refreshrate.txt"), rate.ToString());
		}

		public static List<string> GetBotAdminIds()
		{
			try
			{
                List<string> adminIds = [];
                string[] lines = File.ReadAllLines(Path.Combine(_botPath, "adminIDs.txt"));
				foreach (string line in lines)
				{
					string[] items = line.Split([' '], 2);
					adminIds.Add(items[0]);
				}
				return adminIds;
			}
			catch
			{
				return [];
			}
		}

		public static List<string> GetServerIdsByAdminId(string adminId)
		{
			try
			{
                string[] lines = File.ReadAllLines(Path.Combine(_botPath, "adminIDs.txt"));
				foreach (string line in lines)
				{
					string[] items = line.Split([' '], 2);
					if (items[0] == adminId)
					{
						return [.. items[1].Trim().Split(',').Select(s => s.Trim())];
					}
				}

				return [];
			}
			catch
			{
				return [];
			}
		}

		public static List<(string, string)> GetBotAdminList()
		{
			try
			{
                List<(string, string)> adminList = [];
                string[] lines = File.ReadAllLines(Path.Combine(_botPath, "adminIDs.txt"));
				foreach (string line in lines)
				{
					string[] items = line.Split([' '], 2);
					adminList.Add((items[0], items.Length == 1 ? string.Empty : items[1]));
				}
				return adminList;
			}
			catch
			{
				return [];
			}
		}

		public static void SetBotAdminList(List<(string, string)> adminList)
		{
			Directory.CreateDirectory(_botPath);

			List<string> lines = [];
			foreach ((string adminID, string serverIDs) in adminList)
			{
				lines.Add($"{adminID} {serverIDs}");
			}
			File.WriteAllText(Path.Combine(_botPath, "adminIDs.txt"), string.Join("\n", [.. lines]));
		}
	}
}
