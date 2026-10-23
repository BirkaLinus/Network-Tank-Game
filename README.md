# Network Tank Arena

A two-player authoritative movement and combat starter for the Network Programming course.
Thursday's lab explores network conditions; Friday adds discrete server-authoritative combat.

Use **Unity 6.3.26f1 (6000.3.26f1)**. In Unity Hub, choose **Add project from disk**
and select this repository folder, then open `Assets/Scenes/SampleScene.unity`.
The project includes Netcode for GameObjects 2.13.3, Unity Transport 2.7.4,
Multiplayer Tools 2.2.12 and Multiplayer Play Mode 2.0.2.

## Current baseline

Exactly two players connect through localhost Host/Client sessions. Ownership
binds each process's input and camera to its own tank. Owners send intent; the
server simulates both tanks and writes their positions, chassis rotation and
canonical turret aim. Clients display replicated movement with NetworkTransform
interpolation enabled by default. Turret yaw is separately replicated and applied
directly, without interpolation.

There is no client movement prediction or reconciliation. Left click submits one
reliable fire request; only the server fires, sweeps projectiles, and damages the
persistent test target. No tank health/death/respawn/score is implemented.

**Before starting this contribution in Play Mode, complete the Inspector wiring in
[docs/NETWORK_COMBAT.md](docs/NETWORK_COMBAT.md).** The committed scene/prefabs still
have the movement-lab wiring. Session startup rejects missing combat target setup.

## Start with Multiplayer Play Mode

1. Open `Assets/Scenes/SampleScene.unity`.
2. Open **Window > Play Mode > Scenarios** (or the Play Mode dropdown's
   **Configure play mode scenarios**). Create/select a scenario with the **Main
   Editor** and exactly one **Additional Editor Instance**. Set **Initial Scene**
   to SampleScene. This setup is manual; a scenario and machine-local virtual
   player state are not supplied by the repository.
3. Select that scenario and start Play Mode. Wait for both Game views.
4. In the Main Editor Game view, click **Start Host**.
5. In the additional Editor Game view, click **Start Client (localhost)**.
6. Confirm both connected labels and two tanks with different Owner Client IDs.
   Focus each Game view and release movement keys before controlling it: **W/S**
   moves, **A/D** turns, and the **mouse** aims. Each process must control only
   its owned tank, with its camera following that tank.

The Host's own player has no genuine remote client/server transport round trip.
**Control the Client-owned tank to experience owner-side network delay.** The
Host uses the same authority rules but handles its own input locally.

Use **Stop session** to disconnect; stop Client before Host and wait for shutdown
before starting again.

## Network Simulator setup

Use **Multiplayer Tools Network Simulator**. UnityTransport's obsolete
**DebugSimulator** fields and `SetDebugSimulatorParameters` have no effect in
this installed NGO version.

If the scene/presets have not yet been prepared, perform this one-time setup
outside Play Mode through Unity:

1. Open **Window > Multiplayer > Multiplayer Tools** and choose **Network
   Simulator > Add to Scene**. Keep exactly one Network Simulator component on
   a scene object outside all spawned tank prefabs. Alternatively, use
   **Component > Netcode > Network Simulator** on a scene object.
2. In the Project window, create `Assets/Settings/NetworkConditions`. Use
   **Create > Multiplayer > NetworkSimulatorPresetAsset** to create the five
   presets below. Set their **Name**, delay, jitter and loss values in the
   Inspector. Keep **Packet Loss Interval** at **0** for every preset; it means
   every Nth packet, not a time interval.
3. Set the component's **Connection Preset** to **Custom** and **Custom Preset**
   to Clean localhost. Leave **Scenario** at **None** and **Auto Run Scenario**
   off. Disable the component using its Inspector header checkbox, then save
   the assets and SampleScene. Both processes must start with it disabled.

| Profile | Delay | Jitter | Random loss |
| --- | --- | --- | --- |
| Clean localhost | 0 ms | 0 ms | 0% |
| Moderate latency | 100 ms | 0 ms | 0% |
| Latency plus jitter | 100 ms | 50 ms | 0% |
| Lossy connection | 0 ms | 0 ms | 10% |
| Combined impairment | 100 ms | 50 ms | 5% |

During Play Mode, enable the component **only in the Main Editor Host** and
select a prepared Custom Preset. Leave the virtual Client's simulator disabled;
confirm this in its Inspector. Do not apply the same preset at both endpoints
unless deliberately demonstrating compounded delay. Select presets rather than
editing their values during an exercise. Random loss reproduces a condition,
not an identical packet trace.

With Host-only impairment, Client input passes through the impaired receive
path and authoritative state returns through the impaired send path. A delay of
D in each direction adds approximately **2D network RTT**: 100 ms configured
delay adds roughly 200 ms RTT for the remote Client. Input scheduling, server
simulation, replication and interpolation add further visible delay.

## Reset to clean localhost

1. Restore the simulator **Connection** to **Connected**.
2. Pause/clear active scenarios, leave Auto Run Scenario off, and let any active
   lag spike finish.
3. Select Clean localhost (or None / zero all impairment).
4. Disable the simulator component.
5. Allow queued traffic to drain before comparing clean behavior.
6. For a definitive clean restart, stop Client, then Host, wait for shutdown,
   and restart both sessions with the simulator disabled.

Disabling/re-enabling can retain the previously selected preset. Editing preset
assets in Play Mode may persist changes after Play Mode ends. Check the profile
and saved disabled/clean defaults before the next exercise.

## Lab observations

- **Server authority:** the server decides gameplay state.
- **Interpolation:** clients smooth received states, trading some immediacy for
  smoothness; it does not give them authority.
- **Client prediction:** the owner simulates input before server confirmation.
- **Reconciliation:** predicted state is corrected against authoritative results.
- **Presentation smoothing:** visual motion is eased without changing gameplay
  decisions; hover/bank remains below the stable tank root.

This starter implements authority, replication and interpolation. Prediction and
reconciliation are deliberately deferred.

1. Observe the clean baseline from both processes.
2. Apply Moderate latency; compare Client responsiveness with Host responsiveness.
3. Apply Latency plus jitter; look for variation in movement arrival/smoothness.
4. Apply Lossy connection, then Combined impairment. Isolated lost movement updates
   may be subtle: newer intent replaces older intent, the server temporarily
   retains accepted intent, and transform interpolation can hide some loss.
5. Under the same impairment, compare interpolation enabled and disabled. In the
   Main Editor during Play Mode, select a **spawned tank's server copy** and toggle
   its NetworkTransform **Interpolate** setting. Observe that tank in the Client
   and confirm the replicated setting there. Repeat the same movement with all
   other settings unchanged. Keep server authority and the component enabled.
6. Restore Interpolate on, reset impairment, then exit/re-enter Play Mode to restore
   runtime-only edits. Keep prefab interpolation enabled and the saved simulator
   disabled/clean.

The prefab uses NGO's LegacyLerp interpolation with extra smoothing. Compare
client-rendered movement; the Host's authoritative copies do not demonstrate the
same interpolation delay. Optional ownership labels are available through
Multiplayer Tools' **Hierarchy Network Debug View**.

## Known limits

- Capacity is two connected players; a third is rejected. Disconnecting frees a
  spawn slot for a new Client.
- Transport is direct localhost (`127.0.0.1:7777`); there is no Relay or
  dedicated-server startup.
- NGO scene management is disabled: both instances load SampleScene themselves.
- Combat requires the one-time wiring in [NETWORK_COMBAT.md](docs/NETWORK_COMBAT.md).
- Projectile interpolation can lag the authoritative collision; very short-lived
  projectiles may disappear before a remote frame renders them. Prediction,
  reconciliation, tank health, death, respawn, and score remain deferred.
- Movement intent becomes neutral after **0.5 seconds without an accepted update
  at the server**. This measures server receipt silence, not a command's original
  age. A delayed newer command can still be accepted. After focus loss, movement
  should eventually stop; release movement keys before resuming control.


## MY CHANGES AND NOTES FOR THIS PROJECT

1. Summary

- I chose to focus on the connecting, disconnecting and rejoining issues that can occur in a network game.
- The dictionary works as a memory book of the scores for the players.
- I implemented a 30 second rejoin timer, just to try that out, it simply gives the player a set timer to rejoin the game, when a disconnect occurs.
- I made it so that a player disconnecting from the game, gets a chance of rejoining the game before a new player can take that place.

- In my version of the copied base network game I got, you are able to join the game as a client or host, and disconnect to rejoin within 30 seconds having the same score as when you left, if a different player would join they would not have the same score as the player who left. (I didnt actually try this out on different computers, but I feel fairly safe to say that it would work in that case as well).

2. How to run and test

- The project is using Unity(6000.3.26f1)
- To play you simply have one guy starting by pressing "Start Host" and the other one joining by pressing "Start Client(local host)", from the "menu".
- Both players control a tank (one each) and press LMB to fire using the cursor to aim while moving with "WASD" getting 1p of score hitting the other tank.
- You disconnect by pressing "stop session" or "alt F4" or using the task manager.
- I added an option to have a third guy joining the game by having a suffix when pressing "Start Client(local host) while holding shift. To check if its eligible to join the game or not.

3. What I implemented

- Since the focus wasnt really on gameplay, I simply added a score tracker so that whenever a player's tank shoots the other player's tank you will get 1 score accordingly.

- I made a **LocalPlayerTracker**script which tracks the player using a string for PlayerPrefs, I also added a suffix so that I could test stuff on one computer since I am using PlayerPrefs and needed to separate the host from the client when testing on one computer.

- I made **TankScore**script which tracks if a tank gets hit using the interface "IDamageable" together with an ApplyDamage function which also tracks the source which then also tracks who gets the point. When a player gets a point I also save the score to another script "PlayerScoreMemory".

- I made **PlayerScoreMemory**script which uses a Dictionary that saves not only the score but also the time when a disconnection happens giving the player a 30 seconds timer **float GraceSeconds** to rejoin the game again getting the same amount of score he/she had when the disconnect occurred. I simply check if the playerID string is equal to the player joining the game, to keep track of whether the joining player should get the old score or if its a new player (getting 0 score even though the rejoin timer). When I spawn a tank (from joining the game) I call [PlayerScoreMemory.GetScore] (from the server) inside TankScore(which is used on the tanks).

- I also had "last seen" timestamp in PlayerPrefs that shows the rejoin countdown after a Alt+F4.

4. Authority and replication decisions

- The server owns the score using it inside a dictionary and telling the client its score. I protect the [AddScore()] by simply adding "if (!IsServer) return;"
- The score is a NetworkVariable so that it gets replicated to all clients, which is handled only by the server.
- The dictionary works as a memory for the score when a tank gets removed(player disconnect).
- Even though the NGO creates a ClientId I need to have a persistent PlayerID because NGO makes a new ClientID as soon as a player joins(even if its the same player that join, disconnect, join) by creating my own PlayerID (followed with a suffix primarily for testing) I get much more control of who is inside the game, **is it a new player or is it an old player?** by knowing that I can give the correct score accordingly and I can also scale the game with further more mechanics using this system instead.
- The shown countdown(GraceSeconds) for the client I am using is only for the cosmetics the actual timer for this is checked from the dictionary(disconnectTimes) which registers when a player left and after that it checks **if (Time.time - leftAt > GraceSeconds)** which simply means, if the time the game has run minus the time the player left is bigger than the GraceSeconds(30) then the saved score and the disconnected player get removed from the dictionaries (disconnectTimes, savedScores). Simplified, Expiry is checked lazily: when a player reconnects, the server compares Time.time - leftAt with GraceSeconds, and if it is larger it deletes that player’s saved score and disconnect time.

5. Network conditions used for testing

**Conditions**
- Multiplayer Tools Network Simulator, enabled only on the host, I changed everything quite a bit, but since nothing really affected the setups I've made for this project it doesnt feel necessary to write down more details to this as I tried all kinds of settings without noticing any issues(for my focus of this game).

Packet Delay Range (ms): *189-235*
Packet Delay(+ms): *212*
Packet Jitter (±ms) *27*
Packet Loss (%) *33*
Packet Loss Interval *0*

- I also tried other settings while experimenting and saw no problems with reconnecting or the seat reservation. The only thing I noticed was a very small delay when joining.

**Observation/Experiment**
- Score: always identical on both screens, some slight delay making the host see the score before the client on the impaired version.
- Movement and shooting: Very laggy for the client. The other tank's movement appeared earlier on the host than on the client. Hits did not always look like hits.
- Reconnect, rejoin with kept score, and the "stranger/player 3" being rejected during the grace period: behaved the same as on clean localhost.

**Explanation/Thoughts**
- The hit is decided on the server using server positions. The client sees a delayed, interpolated view, so what it sees can be different from what the server sees (the server's version is the real one).
- The score is replicated state and stays correct. The host sees changes first because it is the server, while the client receives the update after it has crossed the impaired connection.
- Reconnect, seat reservation and score memory run on the server and use the connection handshake, which is reliable. Loss causes resends and delay makes it slower, but the outcome is the same. GraceSeconds is measured on the server, so a network delay of a few hundred ms does not matter for a 30 s window(GraceSeconds), and my tests matched that.
- Fixing the visual mismatch would probably need lag compensation or some sort of client-side prediction. I did not implement this, since my focus was reconnect and score state.
- My changes were more or less only server sided, which made these things not bothered by these scenarios.

6. A problem I found and how I diagnosed it

- The ClientID problem described earlier, NGO creates a new ClientID every time you join the session, I made sure to make my own "ClientID" -> PlayerID which gives me a unique set of numbers to track the player, which led to the next problem, Host and Client gets the same PlayerID since its on the same computer, to fix that I added a suffix(only necessary when using same computer) which made a bunch of numbers followed with "_host" or "_Client" depending on if I press "Start Host" or "Start Client(local host)". I verified this using Debug.Logs. - Using PlayerPrefs.

7. Networking improvement

- Problem/cause: disconnecting from a game leaves the seat open for another player to take without giving the player a chance to take it back.
- Simulation: I made another suffix by pressing "shift" which produces an imaginary strangerclient that tries to join the game.
- Fix: I made a [Reservation-Class] that keeps track of playerID that has disconnected it gets checked inside the [ApproveConnection] and is valid for the **GraceSeconds** period of time.
- Result: If a new playerID tries to join the seat it simply doesnt let it, while the playerID that can rejoin, can rejoin the game.

- Reflection: The result is what I wanted, however, it felt a bit spaghetti and hard to follow when I implemented it. For example I have two places that records/keeps track of when a player left, which is confusing. An improvement would probably be to have it in one place, but this got very rushed to be finished in time, but is something I would've done differently giving more time. The code structure overall on the project I feel happy about, its just this last part of the project I didnt quite like. I think understanding what Ive done for an outside programmer on the project as a whole would be easy to get in to, except for this last part, I still want to leave it in though as its a meaningful improvement in my opinion.

8. Known limitations

- The scores live inside the host's memory, so if the host would quit the game, its all lost, could be an issue depending on what we want to go for here.
- a rejected player only sees the general “room for two players” message, not that the seat is reserved and/or for how long. Its kinda hard to try this out using one computer, I ended up not implementing a solution to this, because of time.
- I think the client's countdown is an estimate, the server's timer is the true timer that counts, I did try to check if it was not synced, I didnt discover any bugs there but there could for sure be something "fishy" there, at least in my mind.
- I guess the playerID could be stolen so that a different player joins using another player's playerID, so having some sort of authenticator to solve this(not sure how to do it practically for now though).
- Another problem for this would be if a player actually wants to leave the game, the third player wanting to join, has to wait the **GraceSeconds** anyways, a better implementation would maybe be for a client to "hard-leave" the game or something in that area.
- The returning player gets a free seat, not necessarily their old one. It works because there are only two seats and the host takes seat 0.