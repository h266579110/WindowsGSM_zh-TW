﻿using System.Collections.Generic;
using System.IO;

namespace WindowsGSM.Functions
{
    class ServerAddon(string serverId, string serverGame) {
        private readonly string _serverId = serverId;
        private readonly string _serverGame = serverGame;
        private readonly dynamic _gameServer = GameServer.Data.Class.Get(serverGame);

        public List<string> GetLeftListBox()
        {
            List<string> list = [];

            if (_serverGame == GameServer.DAYZ.FullName)
            {
                string modPath = ServerPath.GetServersConfigs(_serverId, "DayZActivatedMods.cfg");
                string activatedMods = File.Exists(modPath) ? File.ReadAllText(modPath) : string.Empty;
                string[] folders = Directory.GetDirectories(ServerPath.GetServersServerFiles(_serverId), "@*", SearchOption.TopDirectoryOnly);

                foreach (string folder in folders)
                {
                    string metaFile = Path.Combine(folder, "meta.cpp");
                    if (!File.Exists(metaFile))
                    {
                        continue;
                    }

                    string folderName = Path.GetFileName(folder);
                    if (activatedMods.Contains(folderName))
                    {
                        continue;
                    }

                    list.Add(folderName);
                }
            }

            dynamic gameServer = GameServer.Data.Class.Get(_serverGame);
            if (gameServer is GameServer.Engine.Source)
            {
                string dpluginPath = ServerPath.GetServersServerFiles(_serverId, gameServer.Game, @"addons\sourcemod\plugins\disabled");
                if (Directory.Exists(dpluginPath))
                {
                    string[] smxFiles = Directory.GetFiles(dpluginPath, "*.smx", SearchOption.TopDirectoryOnly);
                    foreach (string smxFile in smxFiles)
                    {
                        list.Add(Path.GetFileName(smxFile));
                    }
                }

                return list;
            }

            return list;
        }

        public List<string> GetRightListBox()
        {
            List<string> list = [];

            if (_serverGame == GameServer.DAYZ.FullName)
            {
                string modPath = ServerPath.GetServersConfigs(_serverId, "DayZActivatedMods.cfg");
                if (File.Exists(modPath))
                {
                    foreach (string folderName in File.ReadLines(modPath))
                    {
                        string metaPath = ServerPath.GetServersServerFiles(_serverId, folderName.Trim());
                        if (Directory.Exists(metaPath))
                        {
                            if (File.Exists(Path.Combine(metaPath, "meta.cpp")))
                            {
                                list.Add(folderName.Trim());
                            }
                        }
                    }
                }

                return list;
            }

            dynamic gameServer = GameServer.Data.Class.Get(_serverGame);
            if (gameServer is GameServer.Engine.Source)
            {
                string pluginPath = ServerPath.GetServersServerFiles(_serverId, gameServer.Game, @"addons\sourcemod\plugins");
                if (Directory.Exists(pluginPath))
                {
                    string[] smxFiles = Directory.GetFiles(pluginPath, "*.smx", SearchOption.TopDirectoryOnly);
                    foreach (string smxFile in smxFiles)
                    {
                        list.Add(Path.GetFileName(smxFile));
                    }
                }

                return list;
            }

            return list;
        }

        public bool AddToLeft(List<string> rItems, string itemName = "")
        {
            if (_serverGame == GameServer.DAYZ.FullName)
            {
                string modPath = ServerPath.GetServersConfigs(_serverId, "DayZActivatedMods.cfg");
                string text = string.Join("\n", [.. rItems]);
                File.WriteAllText(modPath, text);
                return true;
            }

            dynamic gameServer = GameServer.Data.Class.Get(_serverGame);
            if (gameServer is GameServer.Engine.Source)
            {
                string pluginPath = ServerPath.GetServersServerFiles(_serverId, gameServer.Game, @"addons\sourcemod\plugins");
                string dpluginPath = ServerPath.GetServersServerFiles(_serverId, gameServer.Game, @"addons\sourcemod\plugins\disabled");
                if (Directory.Exists(pluginPath) && Directory.Exists(dpluginPath))
                {
                    try
                    {
                        File.Move(Path.Combine(pluginPath, itemName), Path.Combine(dpluginPath, itemName));
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        public bool AddToRight(List<string> rItems, string itemName = "")
        {
            if (_serverGame == GameServer.DAYZ.FullName)
            {
                string modPath = ServerPath.GetServersConfigs(_serverId, "DayZActivatedMods.cfg");
                string text = string.Join("\n", [.. rItems]);
                File.WriteAllText(modPath, text);
                return true;
            }

            dynamic gameServer = GameServer.Data.Class.Get(_serverGame);
            if (gameServer is GameServer.Engine.Source)
            {
                string pluginPath = ServerPath.GetServersServerFiles(_serverId, gameServer.Game, @"addons\sourcemod\plugins");
                string dpluginPath = ServerPath.GetServersServerFiles(_serverId, gameServer.Game, @"addons\sourcemod\plugins\disabled");
                if (Directory.Exists(pluginPath) && Directory.Exists(dpluginPath))
                {
                    try
                    {
                        File.Move(Path.Combine(dpluginPath, itemName), Path.Combine(pluginPath, itemName));
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        public string GetModsName()
        {
            if (_serverGame == GameServer.DAYZ.FullName)
            {
                return "Steam Workshop";
            }

            dynamic gameServer = GameServer.Data.Class.Get(_serverGame);
            return gameServer is GameServer.Engine.Source ? "SourceMod Plugins" : string.Empty;
        }

        public static bool IsGameSupportManageAddons(string serverGame)
        {
            dynamic gameServer = GameServer.Data.Class.Get(serverGame);
            return gameServer is GameServer.Engine.Source || serverGame == GameServer.DAYZ.FullName;
        }
    }
}
