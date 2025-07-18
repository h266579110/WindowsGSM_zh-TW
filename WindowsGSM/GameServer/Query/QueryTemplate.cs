using System;
using System.Collections.Generic;   
using System.Threading.Tasks;

namespace WindowsGSM.GameServer.Query
{
    public interface IQueryTemplate
    {
        void SetAddressPort(string address, int port, int timeout = 5);
        Task<Dictionary<string, string>> GetInfo();
        Task<List<PlayerData>> GetPlayersData();
        Task<string> GetPlayersAndMaxPlayers();
    }

    public struct PlayerData(int id, string name, long score = 0, TimeSpan? timeConnected = null) {
        public int Id = id;
        public string Name = name;
        public long Score = score;
        public TimeSpan? TimeConnected = timeConnected;

        public override readonly string ToString() 
        {
            return $"{Id}:{Name}, Score:{Score}, connected:{TimeConnected?.TotalMinutes}";
        }
    }
}
