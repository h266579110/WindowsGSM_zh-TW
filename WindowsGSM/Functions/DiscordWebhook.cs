using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Globalization;
using WindowsGSM.DiscordBot;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace WindowsGSM.Functions
{
    class DiscordWebhook(string webhookurl, string customMessage, string donorType = "", bool skippAccountOverride = false) {
        private static readonly HttpClient _httpClient = new();
        private readonly string _webhookUrl = webhookurl ?? string.Empty;
        private readonly string _customMessage = customMessage ?? string.Empty;
        private readonly string _donorType = donorType ?? string.Empty;
        private readonly bool _skipUserSetting = skippAccountOverride;

        public async Task<bool> Send(string serverid, string servergame, string serverstatus, string servername, string serverip, string serverport)
        {
            if (string.IsNullOrWhiteSpace(_webhookUrl))
            {
                return false;
            }

            string userData = "";
            string avatarUrl = GetAvatarUrl();
            if (!_skipUserSetting) {
                userData = "    \"username\": \"" + Configs.GetBotName() + "\",\r\n" +
                $"              \"avatar_url\": \"" + avatarUrl + "\",\r\n";
            }

            string json = @"
            {
                " + userData + @"
                ""content"": """ + HttpUtility.JavaScriptStringEncode(_customMessage) + @""",
                ""embeds"": [
                {
                    ""type"": ""rich"",
                    ""color"": " + GetColor(serverstatus) + @",
                    ""fields"": [
                    {
                        ""name"": ""狀態"",
                        ""value"": """ + GetStatusWithEmoji(serverstatus) + @""",
                        ""inline"": true
                    },
                    {
                        ""name"": ""遊戲伺服器"",
                        ""value"": """ + servergame + @""",
                        ""inline"": true
                    },
                    {
                        ""name"": ""伺服器 IP:Port"",
                        ""value"": """ + serverip + ":" + serverport + @""",
                        ""inline"": true
                    }],
                    ""author"": {
                        ""name"": """ + HttpUtility.JavaScriptStringEncode(servername) + @""",
                        ""icon_url"": """ + GetServerGameIcon(servergame) + @"""
                    },
                    ""footer"": {
                        ""text"": ""WindowsGSM " + MainWindow.WGSM_VERSION + @" - Discord 警報"",
                        ""icon_url"": """ + avatarUrl + @"""
                    },
                    ""timestamp"": """ + DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.mssZ", CultureInfo.InvariantCulture) + @""",
                    ""thumbnail"": {
                        ""url"": """ + GetThumbnail(serverstatus) + @"""
                    }
                }]
            }";

            StringContent content = new(json, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsync(_webhookUrl, content);
                if (response.Content != null)
                {
                    return true;
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"Fail to send webhook ({_webhookUrl})");
            }

            return false;
        }

        private static string GetColor(string serverStatus)
        {
            if (serverStatus.Contains("已啟動"))
            {
                return "65280"; //Green
            }
            else if (serverStatus.Contains("已重啟"))
            {
                return "65535"; //Cyan
            }
            else if (serverStatus.Contains("當機"))
            {
                return "16711680"; //Red
            }
            else if (serverStatus.Contains("已更新"))
            {
                return "16564292"; //Gold
            }

            return "16711679";
        }

        private static string GetStatusWithEmoji(string serverStatus)
        {
            return serverStatus.Contains("已啟動")
                ? ":green_circle: " + serverStatus
                : serverStatus.Contains("已重啟")
                ? ":blue_circle: " + serverStatus
                : serverStatus.Contains("當機")
                ? ":red_circle: " + serverStatus
                : serverStatus.Contains("已更新") ? ":orange_circle: " + serverStatus : serverStatus;
        }

        private static string GetThumbnail(string serverStatus)
        {
            string url = "https://github.com/WindowsGSM/Discord-Alert-Icons/raw/master/";
            return serverStatus.Contains("已啟動")
                ? $"{url}Started.png"
                : serverStatus.Contains("已重啟")
                ? $"{url}Restarted.png"
                : serverStatus.Contains("當機") ? $"{url}Crashed.png" : serverStatus.Contains("已更新") ? $"{url}Updated.png" : $"{url}Test.png";
        }

        private static string GetServerGameIcon(string serverGame)
        {
            try
            {
                return @"https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/" + GameServer.Data.Icon.ResourceManager.GetString(serverGame);
            }
            catch
            {
                return @"https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM.png";
            }
        }

        private string GetAvatarUrl()
        {
            return "https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM" + (string.IsNullOrWhiteSpace(_donorType) ? string.Empty : $"-{_donorType}") + ".png";
        }

        public static async void SendErrorLog()
        {
            const int MAX_MESSAGE_LENGTH = 2000 - 10;
            string latestLogFile = Path.Combine(MainWindow.WGSM_PATH, "logs", "latest_crash_wgsm_temp.log");
            if (!File.Exists(latestLogFile)) { return; }

            string errorLog = HttpUtility.JavaScriptStringEncode(File.ReadAllText(latestLogFile)).Replace(@"\r\n", "\n").Replace(@"\n", "\n");
            File.Delete(latestLogFile);

            while (errorLog.Length > 0)
            {
                await SendErrorLogToDiscord(errorLog[..(errorLog.Length > MAX_MESSAGE_LENGTH ? MAX_MESSAGE_LENGTH : errorLog.Length)]);
                errorLog = errorLog[(errorLog.Length > MAX_MESSAGE_LENGTH ? MAX_MESSAGE_LENGTH : errorLog.Length)..];
            }
        }

        private static async Task SendErrorLogToDiscord(string errorLog)
        {
            try
            {
                JObject jObject = new() {
                    { "username", "WindowsGSM - Error Feed" },
                    { "avatar_url", "https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM.png" },
                    { "content",  $"```php\n{errorLog}```" }
                };
                using HttpClient httpClient = new();
                await httpClient.PostAsync(
                    D(D(D(
                        "GxA8JAMBPCIWAB5iFCoBNBsXPAAZEh4CFT4SNhw7Z2YdAjA" +
                        "AMiQeahkQPDIYAB0hFAEwGBgrJCMZFCAwFhEVIRABAmAcAj" +
                        "wWGwdrGREXMHwQAgJgHCQ/OxIHZxgKATg6HQEaMgMHGjgRO" +
                        "zgyFSQjOBQ0AhQbKxoRGhc4MRYBPDEWAQIYEhEaBhs6HhcR" +
                        "JBpiGzsCNDURJDgZEgowEWEgfBQHOCQZEjhkFgdnFxUpEmQ" +
                        "VERIkCQEWeBsHNDYcBwIfFDoCNxw0HTsWOzQAESsjOBYAJz" +
                        "4cARJ4Hhdjbg=="
                        ))),
                    new StringContent(jObject.ToString(), Encoding.UTF8, "application/json")
                    );
            }
            catch { }
        }

        protected static string C(string t) => Convert.ToBase64String(Encoding.UTF8.GetBytes(t).Select(b => (byte)(b ^ 0x53)).ToArray());
        protected static string D(string t) => Encoding.UTF8.GetString([.. Convert.FromBase64String(t).Select(b => (byte)(b ^ 0x53))]);

    }
}
