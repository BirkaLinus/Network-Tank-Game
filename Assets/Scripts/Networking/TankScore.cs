using NetworkTankArena.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkTankArena
{
    public class TankScore : NetworkBehaviour, IDamageable
    {
        public NetworkVariable<int> score = new NetworkVariable<int>(0);

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

        }

        private void OnGUI()
        {
            if (!IsOwner) return;
            GUI.Label(new Rect(12, 200, 300, 30), "Score: " + score.Value);
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
