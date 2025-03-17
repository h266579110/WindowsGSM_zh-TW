﻿using System.Threading.Tasks;
using System.IO;
using System.Net;
using System;
using System.Collections.Generic;
using Discord;

namespace WindowsGSM.Functions
{
    //Link: https://github.com/WindowsGSM/Game-Server-Configs

    public static class Github
    {
        // Old function
        public static async Task<bool> DownloadGameServerConfig(string filePath, string gameFullName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            try
            {
                Stream stream = await App.httpClient.GetStreamAsync($"https://github.com/WindowsGSM/Game-Server-Configs/raw/master/{gameFullName.Replace(":", "")}/{System.IO.Path.GetFileName(filePath)}");
                using FileStream fileStream = File.Create(filePath);
                await stream.CopyToAsync(fileStream);
                //using WebClient webClient = new();
                //await webClient.DownloadFileTaskAsync($"https://github.com/WindowsGSM/Game-Server-Configs/raw/master/{gameFullName.Replace(":", "")}/{System.IO.Path.GetFileName(filePath)}", filePath);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Github.DownloadGameServerConfig {e}");
            }

            return File.Exists(filePath);
        }

        // New function
        /// <summary>
        /// Download the config from https://github.com/WindowsGSM/Game-Server-Configs/
        /// </summary>
        /// <param name="configPath">Local config location</param>
        /// <param name="serverGame">Server Game FullName</param>
        /// <param name="replaceValues">Replace Values</param>
        /// <returns></returns>
        public static async Task<bool> DownloadGameServerConfig(string configPath, string serverGame, List<(string, string)> replaceValues)
        {
            // Create Directory for the config file
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));

            // Remove existing config file if exists
            if (File.Exists(configPath))
            {
                await Task.Run(() => File.Delete(configPath));
            }

            try
            {
                // Download config file from github
                Stream stream = await App.httpClient.GetStreamAsync($"https://github.com/WindowsGSM/Game-Server-Configs/raw/master/{serverGame.Replace(":", "")}/{Path.GetFileName(configPath)}");
                using FileStream fileStream = File.Create(configPath);
                await stream.CopyToAsync(fileStream);
                //using WebClient webClient = new();
                //await webClient.DownloadFileTaskAsync($"https://github.com/WindowsGSM/Game-Server-Configs/raw/master/{serverGame.Replace(":", "")}/{Path.GetFileName(configPath)}", configPath);

                // Replace values
                string configText = File.ReadAllText(configPath);
                replaceValues.ForEach(x => configText = configText.Replace(x.Item1, x.Item2));
                File.WriteAllText(configPath, configText);
            }
            catch
            {
                return false;
            }

            return File.Exists(configPath);
        }
    }
}
