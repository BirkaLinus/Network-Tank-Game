using System.Collections.Generic;
using UnityEngine;

namespace NetworkTankArena
{
    //Connect with localNetworksessions
    public static class PlayerScoreMemory
    {
        private static readonly Dictionary<ulong, string> clientToPlayer = new Dictionary<ulong, string>();
        private static readonly Dictionary<string, int> savedScores = new Dictionary<string, int>();

        private static readonly Dictionary<string, float> disconnectTimes = new Dictionary<string, float>();

        public const float GraceSeconds = 30f; // Time in seconds to keep the score after disconnect

        public static void Register(ulong clientId, string playerID)
        {
            clientToPlayer[clientId] = playerID;

            if (disconnectTimes.TryGetValue(playerID, out float leftAt))
            {
                if (Time.time - leftAt > GraceSeconds)
                {
                    savedScores.Remove(playerID);
                    disconnectTimes.Remove(playerID);
                }
            }
        }


        //Called when a client disconnects
        public static void MarkDisconnected(ulong clientId)
        {
            if (clientToPlayer.TryGetValue(clientId, out string playerId))
            {
                disconnectTimes[playerId] = Time.time; //using server side so that the client cant cheat by changing the time on their device.
            }
        }

        public static int GetScore(ulong clientId)
        {
            if (clientToPlayer.TryGetValue(clientId, out string playerId) && savedScores.TryGetValue(playerId, out int score))
            {
                return score;
            }
            return 0;
        }

        public static void SaveScore(ulong clientId, int score)
        {
            if (clientToPlayer.TryGetValue(clientId, out string playerId))
            {
                savedScores[playerId] = score;
            }
        }

        public static void ClearScore()
        {
            clientToPlayer.Clear();
            savedScores.Clear();
            disconnectTimes.Clear();
        }
    }
}
