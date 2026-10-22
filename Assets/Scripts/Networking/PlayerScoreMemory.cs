using System.Collections.Generic;
using UnityEngine;

namespace NetworkTankArena
{
    //Connect with localNetworksessions
    public static class PlayerScoreMemory
    {
        private static readonly Dictionary<ulong, string> clientToPlayer = new Dictionary<ulong, string>();
        private static readonly Dictionary<string, int> savedScores = new Dictionary<string, int>();

        public static void Register(ulong clientId, string playerID) => clientToPlayer[clientId] = playerID;

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
        }
    }
}
