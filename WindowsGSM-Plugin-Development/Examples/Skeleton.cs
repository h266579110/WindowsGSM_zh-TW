using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using Newtonsoft.Json.Linq;

namespace WindowsGSM.Plugins
{
    public class Skeleton(ServerConfig serverData) {
        // - Plugin Details
        public Plugin Plugin = new() {
            name = "",
            author = "",
            description = "",
            version = "",
            url = "",
            color = "#ffffff"
        };
        private readonly ServerConfig _serverData = serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        public string StartPath = "";
        public string FullName = "";
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 1;
        public object QueryMethod = null;


        // - Game server default values
        public string Port = "";
        public string QueryPort = "";
        public string Defaultmap = "";
        public string Maxplayers = "";
        public string Additional = "";


        // - Create a default cfg for the game server after installation
        public static async void CreateServerCFG()
        {

        }


        // - Start server function, return its Process to WindowsGSM
        public static async Task<Process> Start()
        {
            return null;
        }


        // - Stop server function
        public static async Task Stop(Process p)
        {

        }


        // - Install server function
        public static async Task<Process> Install()
        {
            return null;
        }


        // - Update server function
        public static async Task<Process> Update()
        {
            return null;
        }


        // - Check if the installation is successful
        public static bool IsInstallValid()
        {
            return false;
        }


        // - Check if the directory contains paper.jar for import
        public static bool IsImportValid(string path)
        {
            return false;
        }


        // - Get Local server version
        public static string GetLocalBuild()
        {
            return "";
        }


        // - Get Latest server version
        public static async Task<string> GetRemoteBuild()
        {
            return "";
        }
    }
}