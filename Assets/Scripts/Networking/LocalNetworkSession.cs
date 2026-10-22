using NetworkTankArena.Presentation;
using NetworkTankArena.Tanks;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NetworkTankArena.Networking
{
    /// <summary>Scene-owned localhost buttons, two spawn slots, and one local input/camera binding.</summary>
    [DisallowMultipleComponent]
    public sealed class LocalNetworkSession : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private LocalTankController localInput;
        [SerializeField] private TankFollowCamera followCamera;
        [SerializeField] private Transform hostSpawn;
        [SerializeField] private Transform clientSpawn;
        [SerializeField] private NetworkDamageTestTarget damageTargetPrefab;
        [SerializeField] private Transform damageTargetSpawn;
        [SerializeField] private ushort port = 7777;

        [SerializeField] private float reconnectDeadline;
        [SerializeField] private bool hadConnection;

        [SerializeField] private const string LastSeenKey = "lastSeenConnected";
        [SerializeField] private float nextSeenSave;

        private readonly Dictionary<ulong, int> spawnSlots = new Dictionary<ulong, int>();
        private NetworkTankController localPlayer;
        private Vector3 idleCameraPosition;
        private Quaternion idleCameraRotation;
        private string status = "Choose Host or Client (localhost).";

        private void Awake()
        {
            if (localInput != null)
                localInput.BindNetworkTank(null, null);
            if (followCamera != null)
            {
                idleCameraPosition = followCamera.transform.position;
                idleCameraRotation = followCamera.transform.rotation;
                followCamera.SetTarget(null);
            }

            if (PlayerPrefs.HasKey(LastSeenKey) && long.TryParse(PlayerPrefs.GetString(LastSeenKey), out long savedBinary))
            {
                double elapsed = (System.DateTime.UtcNow - System.DateTime.FromBinary(savedBinary)).TotalSeconds;

                if (elapsed >= 0 && elapsed < PlayerScoreMemory.GraceSeconds)
                {
                    reconnectDeadline = Time.time + PlayerScoreMemory.GraceSeconds - (float)elapsed;
                }
            }
        }

        private void OnEnable()
        {
            if (networkManager == null)
                return;

            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnClientStopped += OnClientStopped;
            networkManager.OnServerStopped += OnServerStopped;
            networkManager.OnClientConnectedCallback += OnClientConnected;
        }

        private void OnDisable()
        {
            ClearLocalPlayer();
            spawnSlots.Clear();
            //ADDDED
            PlayerScoreMemory.ClearScore(); //When the host or client disconnects,
                                            //clear the score memory to avoid retaining scores from previous sessions.
            //ADDED
            if (networkManager == null)
                return;

            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientStopped -= OnClientStopped;
            networkManager.OnServerStopped -= OnServerStopped;
            networkManager.OnClientConnectedCallback -= OnClientConnected;

            if (networkManager.ConnectionApprovalCallback == ApproveConnection)
                networkManager.ConnectionApprovalCallback = null;
            if (networkManager.IsListening)
                networkManager.Shutdown();
        }

        private void Update()
        {
            //In case of a client crash or disconnect, 
            //we want to save the last seen time to PlayerPrefs so that the player can rejoin within the grace period.
            //Makes us see the correct(more or less) timer to rejoin with the same score.
            if (networkManager == null || !networkManager.IsConnectedClient || networkManager.IsServer) return;

            if (Time.time < nextSeenSave) return;

            nextSeenSave = Time.time + 2f;
            PlayerPrefs.SetString(LastSeenKey, System.DateTime.UtcNow.ToBinary().ToString());
            PlayerPrefs.Save(); //Writes to the disk immediately to ensure the last seen time is saved.
        }

        public void StartHost() => StartSession(true);
        public void StartClient() => StartSession(false);

        private void StartSession(bool host)
        {
            if (!host)
            {
                PlayerPrefs.DeleteKey(LastSeenKey);
            }

            if (networkManager == null || networkManager.IsListening || networkManager.ShutdownInProgress)
                return;

            if (localInput == null || followCamera == null || hostSpawn == null || clientSpawn == null ||
                port == 0 || networkManager.DistributedAuthorityMode ||
                !(networkManager.NetworkConfig.NetworkTransport is UnityTransport transport) ||
                networkManager.NetworkConfig.PlayerPrefab == null ||
                networkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkTankController>() == null ||
                damageTargetPrefab == null || !damageTargetPrefab.enabled || !damageTargetPrefab.gameObject.activeSelf ||
                !damageTargetPrefab.TryGetComponent(out NetworkObject _) || damageTargetSpawn == null ||
                !TankMath.IsFinite(damageTargetSpawn.position) || !TankMath.IsFinite(damageTargetSpawn.eulerAngles.y) ||
                !networkManager.NetworkConfig.Prefabs.Contains(damageTargetPrefab.gameObject) ||
                !networkManager.NetworkConfig.PlayerPrefab.TryGetComponent(out NetworkTransform rootTransform) ||
                rootTransform.AuthorityMode != NetworkTransform.AuthorityModes.Server)
            {
                status = "Session setup is incomplete; see the Console.";
                Debug.LogError("Assign the scene input/camera, two clear spawn points, a UnityTransport, " +
                    "the network tank Player Prefab, and a registered damage target prefab/marker " +
                    "on a client-server NetworkManager. See docs/NETWORK_COMBAT.md.", this);
                return;
            }

            ClearLocalPlayer();
            spawnSlots.Clear();
            PlayerScoreMemory.ClearScore(); //Clear the score memory when starting a new session to avoid retaining scores from previous sessions.
            reconnectDeadline = 0f; //Reset the reconnect deadline when starting a new session.
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            //ADDED
            LocalPlayerTracker.Suffix = host ? "_host" : "_client"; // adding host or client suffix depending on the session type.
            networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(LocalPlayerTracker.Id); //local player ID is sent to the server for connection approval and score tracking.
            //ADDED
            bool started = host ? networkManager.StartHost() : networkManager.StartClient();
            if (started && host)
            {
                // Exactly one server-owned target per new Host session. Clients receive this spawn
                // and its current NetworkVariables, including on a later connection.
                NetworkDamageTestTarget target = Instantiate(damageTargetPrefab, damageTargetSpawn.position,
                    Quaternion.Euler(0f, damageTargetSpawn.eulerAngles.y, 0f));
                target.NetworkObject.Spawn();
            }
            status = started ? (host ? $"Hosting on localhost:{port}." : "Connecting to localhost...") : "Session could not start.";
        }

        public void StopSession()
        {
            ClearLocalPlayer();
            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();
            status = "Stopping session...";
        }

        internal void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Reserve approval slots too, so simultaneous connection attempts cannot overlap spawns.
            int slot = !spawnSlots.ContainsValue(0) ? 0 : !spawnSlots.ContainsValue(1) ? 1 : -1;
            string playerId = Encoding.UTF8.GetString(request.Payload);
            Debug.Log($"Connection from clientId {request.ClientNetworkId} has player id {playerId}");
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (slot < 0 || spawnSlots.ContainsKey(request.ClientNetworkId))
            {
                response.Reason = "This lab session has room for two players.";
                return;
            }

            Transform spawn = slot == 0 ? hostSpawn : clientSpawn;
            // References can disappear after StartSession validation. Reject without reserving a
            // slot or letting NGO fall back to the prefab pose (the host cannot be declined by NGO).
            if (spawn == null || !TankMath.IsFinite(spawn.position) || !TankMath.IsFinite(spawn.eulerAngles.y))
            {
                response.Reason = "The reserved player spawn marker is missing or has an invalid pose.";
                return;
            }

            response.Position = spawn.position;
            response.Rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
            spawnSlots.Add(request.ClientNetworkId, slot);
            //ADDED
            PlayerScoreMemory.Register(request.ClientNetworkId, playerId); //Register the player ID with the client ID for score tracking.
            //ADDED
            response.Approved = true;
            response.CreatePlayerObject = true;
            // NGO creates exactly one owned PlayerObject using the registered Player Prefab.
        }

        internal void BindLocalPlayer(NetworkTankController player)
        {
            if (player == null || !player.CanReadLocalInput)
                return;

            ClearLocalPlayer();
            localPlayer = player;
            followCamera.SetTarget(player.transform);
            localInput.BindNetworkTank(player, followCamera.GetComponent<Camera>());
        }

        internal void ReleaseLocalPlayer(NetworkTankController player)
        {
            if (localPlayer == player)
                ClearLocalPlayer();
        }

        private void ClearLocalPlayer()
        {
            if (localInput != null)
                localInput.BindNetworkTank(null, null);
            if (followCamera != null)
            {
                followCamera.SetTarget(null);
                followCamera.transform.SetPositionAndRotation(idleCameraPosition, idleCameraRotation);
            }
            localPlayer = null;
        }

        internal void OnClientDisconnected(ulong clientId)
        {
            //ADDED
            if (networkManager != null && networkManager.IsServer)
                PlayerScoreMemory.MarkDisconnected(clientId); //Server marks the client as disconnected for score tracking,
                                                              //allowing for grace period handling.
                                                              //Making sure that the only the server updates this.

            spawnSlots.Remove(clientId);

            if (networkManager != null && clientId == networkManager.LocalClientId)
            {
                ClearLocalPlayer();
                status = string.IsNullOrEmpty(networkManager.DisconnectReason) ?
                    "Disconnected." : networkManager.DisconnectReason;
            }
        }

        private void OnClientStopped(bool wasHost)
        { 
            if (!wasHost && hadConnection)
            {
                reconnectDeadline = Time.time + PlayerScoreMemory.GraceSeconds;
                hadConnection = false;
            }

            ClearLocalPlayer();
            spawnSlots.Clear();
        }

        internal void OnServerStopped(bool wasHost) => spawnSlots.Clear();

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 300f, 190f), GUI.skin.box); //Changed the box to be bigger...
            GUILayout.Label("Network Tank Arena — two-player lab");
            GUILayout.Label(networkManager != null && networkManager.IsConnectedClient ?
                (networkManager.IsHost ? "Host connected" : "Client connected") : status);
            GUI.enabled = networkManager != null && !networkManager.ShutdownInProgress;

            if (reconnectDeadline> 0f)
            {
                float left = reconnectDeadline - Time.time;
                GUILayout.Label(left > 0f ? $"Rejoin within {Mathf.CeilToInt(left)}s to keep your score." : "Join already started game, starting on 0 score.");
            }

            if (networkManager != null && networkManager.IsListening)
            {
                if (GUILayout.Button("Stop session"))
                    StopSession();
            }
            else
            {
                if (GUILayout.Button("Start Host"))
                    StartHost();
                if (GUILayout.Button("Start Client (localhost)"))
                    StartClient();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void OnClientConnected(ulong clientID)
        {
            if (networkManager != null && !networkManager.IsServer && clientID == networkManager.LocalClientId)
            {
                hadConnection = true;
            }
        }
    }
}
