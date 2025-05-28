using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
  private NetworkRunner _runner;

  [SerializeField] private NetworkPrefabRef _playerPrefab;
  [SerializeField] private TMP_Text _messages;
  [SerializeField] private TMP_InputField _sessionField;

  private GameLiftServer _gameliftServer;
  private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

  private bool _mouseButton0;
  private bool _mouseButton1;

  //private TMP_Text _messages;
  //private TMP_InputField _sessionField;

  private string GameSession;

  private bool _isDedicatedServer = false;
  private bool _isDedicatedHost = false;

  private string region = "us";

  void Awake()
  {
    if (_messages == null)
      _messages = FindObjectOfType<TMP_Text>();

    if (_sessionField == null)
      _sessionField = FindObjectOfType<TMP_InputField>();

    Debug.Log(_messages);

#if UNITY_STANDALONE_LINUX 
    _isDedicatedServer = true;
    _isDedicatedHost = false;
#endif

#if UNITY_STANDALONE_OSX 
    _isDedicatedServer = false;
    _isDedicatedHost = false;
#endif

#if UNITY_EDITOR
    _isDedicatedServer = false;
    _isDedicatedHost = false;
#endif

    Debug.Log($"_isDedicatedHost: {_isDedicatedHost}");
    Debug.Log($"_isDedicatedServer: {_isDedicatedServer}");
  }

  // Start is called before the first frame update
  void Start()
  {
    GameSession = "TestGameRoom001";// + UnityEngine.Random.Range(0, 100);
    if (_isDedicatedServer)
    {
      _messages.text = "server: " +GameSession;
      StartGame(GameMode.Server, GameSession);
    }
    else if (_isDedicatedHost)
    {
      _messages.text = "host: " + GameSession;
      StartGame(GameMode.Host, GameSession);
    }
  }

  // Update is called once per frame
  void Update()
  {
    _mouseButton0 = _mouseButton0 | Input.GetMouseButton(0);
    _mouseButton1 = _mouseButton1 || Input.GetMouseButton(1);
  }

  private void OnGUI()
  {
    if (_runner == null)
    {
      var SessionName = Guid.NewGuid().ToString();
      if (_isDedicatedServer)
      {
        if (GUI.Button(new Rect(10, 10, 200, 40), "Server"))
        {
          StartGame(GameMode.Server, SessionName);
        }
      }
      else if (_isDedicatedHost)
      {
        if (GUI.Button(new Rect(10, 10, 200, 40), "Host"))
        {
          StartGame(GameMode.Host, SessionName);
        }
      }
      else
      {
        if (GUI.Button(new Rect(10, 10, 200, 40), "Client"))
        {
          GameSession = _sessionField.text;

          _sessionField.gameObject.SetActive(false);
          _messages.text = "client: " + GameSession;
          StartGame(GameMode.Client, GameSession);
        }
      }
    }
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

  async void StartGame(GameMode mode, string SessionName="")
  {
    Debug.Log($"SessionName: {SessionName}"); 
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
    // Start or join (depends on gamemode) a session with a specific name
    var result = await _runner.StartGame(new StartGameArgs()
    {
      GameMode = mode,
      SessionName = SessionName,
      Scene = scene,
      SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
      CustomPhotonAppSettings = appSettings,
    });
    
    if (result.Ok)
    {
      Debug.Log($"Region: {_runner.SessionInfo.Region}, SessionName: {_runner.SessionInfo.Name}");
      Debug.Log(result);

      GameObject gameLiftObj = GameObject.Find("GameLift");
      if (gameLiftObj == null)
      {
        GameLiftServer gameLiftComponent = gameObject.AddComponent<GameLiftServer>();
      }
    }
    else
    {
      Debug.LogError($"Failed to Start:");
      _messages.text += $"\n {result.ShutdownReason}";
    }

  }


  public void OnConnectedToServer(NetworkRunner runner)
  {
    Debug.Log("OnConnectedToServer");
  }

  public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
  {
    Debug.Log("OnConnectFailed");
  }

  public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
  {
    Debug.Log("OnConnectRequest");
  }

  public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
  {
    Debug.Log("OnCustomAuthenticationResponse");
  }

  public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
  {
    Debug.Log("OnDisconnectedFromServer");
  }

  public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
  {
    Debug.Log("OnHostMigration");
  }

  public void OnInput(NetworkRunner runner, NetworkInput input)
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

  public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
  {
    throw new NotImplementedException();
  }

  public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
  {
    throw new NotImplementedException();
  }

  public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
  {
    throw new NotImplementedException();
  }

  public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
  {
    if (runner.IsServer)
    {
      Debug.Log($"OnPlayerJoined playerId: {player.PlayerId}");

      // Create a unique position for the player
      Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);
      NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
      
      // Keep track of the player avatars for easy access
      _spawnedCharacters.Add(player, networkPlayerObject);
    }
  }

  public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
  {
    if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
    {
      runner.Despawn(networkObject);
      _spawnedCharacters.Remove(player);
    }
  }

  public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
  {
    Debug.Log("OnReliableDataProgress");
  }

  public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
  {
    Debug.Log("OnReliableDataReceived");
  }

  public void OnSceneLoadDone(NetworkRunner runner)
  {
    Debug.Log("OnSceneLoadDone");
  }

  public void OnSceneLoadStart(NetworkRunner runner)
  {
    Debug.Log("OnSceneLoadStart");
  }

  public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
  {
    Debug.Log("OnSessionListUpdated");
  }

  public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
  {
    Debug.Log("OnShutdown");
  }

  public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
  {
    Debug.Log("OnUserSimulationMessage");
  }

  
}
