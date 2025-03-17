using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Threading.Tasks;

namespace WindowsGSM.Functions
{
    static class GlobalServerList
    {
        public static async Task<bool> IsServerOnSteamServerList(string publicIP, string port)
        {
            HttpResponseMessage request = await App.httpClient.GetAsync("http://api.steampowered.com/ISteamApps/GetServersAtAddress/v0001?addr=" + publicIP + "&format=json");
            try {
                using StreamReader responseReader = new(request.Content.ReadAsStream());
                string json = responseReader.ReadToEnd();
                string matchString = "\"addr\":\"" + publicIP + ":" + port + "\"";

                if (json.Contains(matchString)) {
                    return true;
                }
            } catch {
                //ignore
            }
            //if (WebRequest.Create("http://api.steampowered.com/ISteamApps/GetServersAtAddress/v0001?addr=" + publicIP + "&format=json") is HttpWebRequest webRequest)
            //{
            //    webRequest.Method = "GET";
            //    webRequest.Headers["User-Agent"] = "Anything";
            //    webRequest.ServicePoint.Expect100Continue = false;

            //    try
            //    {
            //        using StreamReader responseReader = new(webRequest.GetResponse().GetResponseStream());
            //        string json = responseReader.ReadToEnd();
            //        string matchString = "\"addr\":\"" + publicIP + ":" + port + "\"";

            //        if (json.Contains(matchString)) {
            //            return true;
            //        }
            //    }
            //    catch
            //    {
            //        //ignore
            //    }
            //}

            return false;
        }
    }
}
