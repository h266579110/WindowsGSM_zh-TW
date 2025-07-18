﻿using System;
using System.Diagnostics;
using System.IO;

namespace WindowsGSM.Functions
{
    class BackupConfig
    {
        private const int DefaultMaximumBackups = 3;

        static class SettingName
        {
            public const string BackupLocation = "backuplocation";
            public const string MaximumBackups = "maximumbackups";
        }

        private readonly string _serverId;
        public string BackupLocation;
        public int MaximumBackups = DefaultMaximumBackups;

        public BackupConfig(string serverId)
        {
            _serverId = serverId;
            string configPath = ServerPath.GetServersConfigs(_serverId, "BackupConfig.cfg");
            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, $"{SettingName.BackupLocation}=\"{Path.Combine(MainWindow.WGSM_PATH, "Backups", serverId)}\"{Environment.NewLine}{SettingName.MaximumBackups}=\"{DefaultMaximumBackups}\"");
            }

            LoadConfig();
        }

        public void Open()
        {
            Process.Start(ServerPath.GetServersConfigs(_serverId, "BackupConfig.cfg"));
        }

        private void LoadConfig()
        {
            string configPath = ServerPath.GetServersConfigs(_serverId, "BackupConfig.cfg");
            foreach (string line in File.ReadLines(configPath))
            {
                string[] keyvalue = line.Split(['='], 2);
                if (keyvalue.Length == 2)
                {
                    keyvalue[1] = keyvalue[1].Trim('\"');
                    switch (keyvalue[0])
                    {
                        case SettingName.BackupLocation: BackupLocation = keyvalue[1]; break;
                        case SettingName.MaximumBackups: MaximumBackups = int.TryParse(keyvalue[1], out int max) ? ((max <= 0) ? 1 : max) : DefaultMaximumBackups; break;
                    }
                }
            }
        }
    }
}
