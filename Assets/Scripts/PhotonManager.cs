using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Amazon.GameLift.Model;
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using YamlDotNet.Core.Tokens;
using GameSession = Aws.GameLift.Server.Model.GameSession;

public struct ConnectionState
{
  public GameSession gamesession;
  public String playerSessionId;
  public String gameSessionId;
  public String sessionName;
  public String gameAddress;
  public ushort gamePort;
}

public class PhotonManager : MonoBehaviour, INetworkRunnerCallbacks
{
  private NetworkRunner _runner;

  private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
  private Dictionary<String, bool> _playerSessionIds = new Dictionary<String, bool>();

  public bool _isDedicatedServer = false;
  public bool _isDedicatedHost = false;

  public NetworkPrefabRef _playerPrefab;
  public TMP_Text _messages;
  public TMP_InputField _sessionField;

  private bool _mouseButton0;
  private bool _mouseButton1;

  private ReliableKey playerSessionIdkey;

  // Start is called before the first frame update
  void Start()
  {
    playerSessionIdkey = ReliableKey.FromInts(42, 0, 0, 0);
  }

  // Update is called once per frame
  void Update()
  {
    _mouseButton0 = _mouseButton0 | Input.GetMouseButton(0);
    _mouseButton1 = _mouseButton1 || Input.GetMouseButton(1);
  }


  private FusionAppSettings BuildCustomAppSetting(string region, string customAppID = null, string appVersion = "2.0.0")
  {

    var appSettings = PhotonAppSettings.Global.AppSettings.GetCopy(); ;

    appSettings.UseNameServer = true;
    appSettings.AppVersion = appVersion;

    if (string.IsNullOrEmpty(customAppID) == false)
    {
      appSettings.AppIdFusion = customAppID;
    }

    if (string.IsNullOrEmpty(region) == false)
    {
      appSettings.FixedRegion = region.ToLower();
    }

    // If the Region is set to China (CN),
    // the Name Server will be automatically changed to the right one
    // appSettings.Server = "ns.photonengine.cn";
    Debug.Log(appSettings);
    return appSettings;
  }

  public void StopGame()
  {
    Debug.Log($"StopGame");
    if (_runner != null && _runner.IsRunning)
    {
      _runner.Shutdown();
    }
  }

  public async void StartGame(GameMode mode, ConnectionState state)
  {
    Debug.Log($"StartGame GameMode: {mode}");

    try
    {
      GameSession gamesession = state.gamesession;
      string SessionName = state.sessionName;
      string gameAddress = state.gameAddress;
      ushort gameport = state.gamePort;

      // Create the Fusion runner and let it know that we will be providing user input
      _runner = gameObject.AddComponent<NetworkRunner>();
      _runner.ProvideInput = true;

      var runnerSimulatePhysics3D = gameObject.AddComponent<RunnerSimulatePhysics3D>();
      runnerSimulatePhysics3D.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateAlways;

      // Create the NetworkSceneInfo from the current scene
      var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
      var sceneInfo = new NetworkSceneInfo();
      if (scene.IsValid)
      {
        sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
      }

      var appSettings = BuildCustomAppSetting("us", "e25f8097-ae15-4ecc-ac2e-a9b31037904a");

      StartGameResult result;
      if (mode == GameMode.Client)
      {
        Debug.Log($"STATE: SessionName: {SessionName}, gameAddress: {gameAddress}, gameport: {gameport}");
        // Start or join (depends on gamemode) a session with a specific name

        result = await _runner.StartGame(new StartGameArgs()
        {
          GameMode = mode,
          SessionName = SessionName,
          Scene = scene,
          EnableClientSessionCreation = false,
          SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
          //Address = NetAddress.CreateFromIpPort("0.0.0.0", gameport),
          // CustomPublicAddress = NetAddress.CreateFromIpPort(gameAddress, gameport),
          CustomPhotonAppSettings = appSettings,
          MatchmakingMode = MatchmakingMode.FillRoom,
          DisableNATPunchthrough = false,
          ConnectionToken = System.Text.Encoding.UTF8.GetBytes(state.playerSessionId)
        });
      }
      else
      {
        // SERVER: get game session info
        if (gamesession != null)
        {
          gameAddress = gamesession.IpAddress;
          gameport = (ushort)gamesession.Port;
          var maxPlayers = gamesession.MaximumPlayerSessionCount;
          var gameProperties = gamesession.GameProperties;
          gameProperties.TryGetValue("SessionName", out SessionName);
          Debug.Log($"maxPlayers: {maxPlayers}");
        }

        var customProps = new Dictionary<string, SessionProperty>();
        customProps["playerSessionId"] = $"benxiwan-{UnityEngine.Random.Range(0, 1000)}";
        customProps["gameSessionId"] = SessionName;

        //appSettings.Server = gameAddress;
        //appSettings.Port = port;
        Debug.Log($"STATE: SessionName: {SessionName}, gameAddress: {gameAddress}, gameport: {gameport}");
        // Start or join (depends on gamemode) a session with a specific name
        result = await _runner.StartGame(new StartGameArgs()
        {
          GameMode = mode,
          SessionName = SessionName,
          Scene = scene,
          EnableClientSessionCreation = false,
          SessionProperties = customProps,
          SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
          Address = NetAddress.CreateFromIpPort("0.0.0.0", gameport),
          CustomPublicAddress = NetAddress.CreateFromIpPort(gameAddress, gameport),
          CustomPhotonAppSettings = appSettings,
          DisableNATPunchthrough = false,
          MatchmakingMode = MatchmakingMode.FillRoom
        });
      }

      if (result.Ok)
      {
        Debug.Log($"Region: {_runner.SessionInfo.Region}, SessionName: {_runner.SessionInfo.Name},\n" +
          $" Ready: {_runner.IsCloudReady}, Connection: {_runner.CurrentConnectionType} \n" +
          $" NATType: {_runner.NATType} Mode: {_runner.IsSharedModeMasterClient} \n");
        Debug.Log(result);
      }
      else
      {
        Debug.LogError($"Failed to Start: {result.ShutdownReason}");
        //_messages.text += $"\n {result.ShutdownReason}";
      }

    }
    catch (Exception ex)
    {
      Debug.Log(ex);
    }
  }


  void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
  {
    Debug.Log("OnConnectedToServer");
  }

  void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
  {
    Debug.Log("OnConnectFailed");
  }

  void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
  {
    Debug.Log("OnConnectRequest");
  }

  void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
  {
    Debug.Log("OnCustomAuthenticationResponse");
  }

  void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
  {
    Debug.Log("OnDisconnectedFromServer");
  }

  void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
  {
    Debug.Log("OnHostMigration");
  }

  void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
  {
    var data = new NetworkInputData();

    if (Input.GetKey(KeyCode.W))
      data.direction += Vector3.forward;

    if (Input.GetKey(KeyCode.S))
      data.direction += Vector3.back;

    if (Input.GetKey(KeyCode.A))
      data.direction += Vector3.left;

    if (Input.GetKey(KeyCode.D))
      data.direction += Vector3.right;

    data.buttons.Set(NetworkInputData.MOUSEBUTTON0, _mouseButton0);
    _mouseButton0 = false;
    data.buttons.Set(NetworkInputData.MOUSEBUTTON1, _mouseButton1);
    _mouseButton1 = false;

    input.Set(data);

  }

  void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
  {
    Debug.Log("OnInputMissing");
  }

  void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
  {
    Debug.Log("OnObjectEnterAOI");
  }

  void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
  {
    Debug.Log("OnObjectExitAOI");
  }

  void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
  {
    if (runner.IsServer)
    {

      Debug.Log($"Photon OnPlayerJoined playerId: {player.PlayerId}");

      // Create a unique position for the player
      Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);
      NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);

      // Keep track of the player avatars for easy access
      _spawnedCharacters.Add(player, networkPlayerObject);

      byte[] token = runner.GetPlayerConnectionToken(player);
      var playerSessionId = System.Text.Encoding.UTF8.GetString(token);

      Debug.Log($"GameLift OnPlayerJoined playerId: {playerSessionId}");
      GameLiftServerAPI.AcceptPlayerSession(playerSessionId);

    }

  }

  void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
  {
    if (runner.IsServer && _spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
    {
      Debug.Log($"OnPlayerLeft playerId: {player.PlayerId}");
      runner.Despawn(networkObject);
      _spawnedCharacters.Remove(player);

      byte[] token = runner.GetPlayerConnectionToken(player);
      var playerSessionId = System.Text.Encoding.UTF8.GetString(token);
      Debug.Log($"RemovePlayerSession:{playerSessionId}");
      GameLiftServerAPI.RemovePlayerSession(playerSessionId);
    }
  }

  void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
  {
    Debug.Log("OnReliableDataProgress");
  }

  void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
  {
    //var playerSessionId = System.Text.Encoding.UTF8.GetString(data);
    //_playerSessionIds.Add(playerSessionId, false);

  }

  void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
  {
    Debug.Log("OnSceneLoadDone");
  }

  void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
  {
    Debug.Log("OnSceneLoadStart");
  }

  void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
  {
    Debug.Log("OnSessionListUpdated");
  }

  void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
  {
    Debug.Log("OnShutdown");
    GameLiftServerAPI.ProcessEnding();
  }

  void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
  {
    Debug.Log("OnUserSimulationMessage");
  }

  

}
