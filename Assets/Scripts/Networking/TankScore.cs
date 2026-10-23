using NetworkTankArena.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkTankArena
{
    public class TankScore : NetworkBehaviour, IDamageable
    {
        public NetworkVariable<int> score = new NetworkVariable<int>(0);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                score.Value = PlayerScoreMemory.GetScore(OwnerClientId); //Restores the saved score to the client.
            }
        }

        private void Update()
        {
            //Testing
            if (!IsOwner) return;
            PressG();
        }

        public void AddScore()
        {
            if (!IsServer) return;

            score.Value += 1;

            PlayerScoreMemory.SaveScore(OwnerClientId, score.Value); //Saves the score to memory to the clientID. 
                                                                     //This is done on the server to ensure that
                                                                     //the score is saved only once and not multiple
                                                                     //times by different clients.
        }

        private void OnGUI()
        {
            string who = IsOwner ? "You" : "Opponent";
            float y = IsOwner ? 270f : 295f;   // below the menu box, adjust if it overlaps
            GUI.Label(new Rect(12f, y, 300f, 30f), $"{who} (client {OwnerClientId}): {score.Value}");
        }

        private void PressG() //TestFunction
        {
            //Isowner check in update
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                AddScore();
            }
        }
        public void ApplyDamage(int amount, GameObject source)
        {
            if (amount <= 0 || source == null) return;

            var shooter = source.GetComponentInParent<TankScore>();
            if (shooter == null || shooter == this) return;   //To avoid getting points for self-damage
            shooter.AddScore();
        }
    }
}
