using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Amazon;
using Amazon.GameLift;
using Amazon.GameLift.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Aws.GameLift.Server.Model;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEngine;

public class AWSGameManager : MonoBehaviour
{
  private GameLiftServer _gameLiftComponent;
  private PhotonManager _photonManager;

  private string gameRegion = "us";
  private ushort gamePort = 7778;
  private RegionEndpoint awsRegion = RegionEndpoint.USEast1;

  private bool _isDedicatedServer = false;
  private bool _isDedicatedHost = false;

  [SerializeField] private NetworkPrefabRef _playerPrefab;
  [SerializeField] private TMP_Text _messages;
  [SerializeField] private TMP_InputField _sessionField;

  private Dictionary<string, string> parameters;

  void Awake()
  {

#if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
    parameters = new Dictionary<string, string>();
    ParseCommandLine();
#endif

#if UNITY_STANDALONE_LINUX
    _isDedicatedServer = true;
    _isDedicatedHost = false;
#endif

#if UNITY_STANDALONE_OSX
    _isDedicatedServer = false;
    _isDedicatedHost = true;
#endif

#if UNITY_EDITOR
    _isDedicatedServer = false;
    _isDedicatedHost = false;
#endif

    Debug.Log($"_isDedicatedHost: {_isDedicatedHost}");
    Debug.Log($"_isDedicatedServer: {_isDedicatedServer}");

    if (GameObject.Find("GameLift") == null)
    {
      if (_isDedicatedServer || _isDedicatedHost)
      {
        _gameLiftComponent = gameObject.AddComponent<GameLiftServer>();
      }
      
      _photonManager = gameObject.AddComponent<PhotonManager>();
    }
  }

  // Start is called before the first frame update
  void Start()
  {
    // photon settings
    _photonManager._isDedicatedHost = _isDedicatedHost;
    _photonManager._isDedicatedServer = _isDedicatedServer;
    _photonManager._playerPrefab = _playerPrefab;
    _photonManager._messages = _messages;
    _photonManager._sessionField = _sessionField;
    if (_isDedicatedServer || _isDedicatedHost)
    {
      // gamelift settings
      _gameLiftComponent._isDedicatedHost = _isDedicatedHost;
      _gameLiftComponent._isDedicatedServer = _isDedicatedServer;
      _gameLiftComponent.gamePort = gamePort;
      _gameLiftComponent._photonManager = _photonManager;
    }
  }

  private void OnGUI()
  {

    if (_isDedicatedServer)
    {
      if (GUI.Button(new Rect(10, 10, 200, 40), "Server"))
      {
        //StartGame(GameMode.Server, SessionName);
      }
    }
    else if (_isDedicatedHost)
    {
      if (GUI.Button(new Rect(10, 10, 200, 40), "Host"))
      {
        //StartGame(GameMode.Host, SessionName);
      }
    }
    else
    {
      if (GUI.Button(new Rect(10, 10, 200, 40), "Client"))
      {
        var sessionName = _sessionField.text;

        _sessionField.gameObject.SetActive(false);
        _messages.text = "client: " + sessionName;

        var playerSessionId = $"benxiwan-{UnityEngine.Random.Range(0, 10000)}";
        var gameSessionId = "arn:aws:gamelift:us-east-1::gamesession/fleet-17bf0e11-3443-4aff-abc4-85f8512b04fc/custom-location-1/gsess-ff3ee145-01bd-4415-af1a-17a5e7c00c07";

        var connState = new ConnectionState
        {
          sessionName = sessionName,
          playerSessionId = playerSessionId,
          gameSessionId = gameSessionId
        };
        

        CreatePlayerSession(connState);
      }
    }
  }

  async void CreatePlayerSession(ConnectionState connState)
  {
    try
    {
      var client = new AmazonSecurityTokenServiceClient(awsRegion);
      var request = new AssumeRoleRequest
      {
        RoleArn = "arn:aws:iam::614954710407:role/GameLiftS3Access",
        RoleSessionName = System.Guid.NewGuid().ToString()
      };
      var response = await client.AssumeRoleAsync(request);
      var gameLiftClient = new AmazonGameLiftClient(response.Credentials, awsRegion);

      var createPlayerSessionRequest = new CreatePlayerSessionRequest
      {
        GameSessionId = connState.gameSessionId,
        PlayerId = connState.playerSessionId
      };
      var result = await gameLiftClient.CreatePlayerSessionAsync(createPlayerSessionRequest);
      if (result.HttpStatusCode == System.Net.HttpStatusCode.OK)
      {
        Debug.Log("Player session created successfully!");
        if (result.PlayerSession != null)
        {
          Debug.Log($"Gamelift PlayerId: {connState.playerSessionId}");
          Debug.Log($"Player Session ID: {result.PlayerSession.PlayerSessionId}");
          Debug.Log($"Status: {result.PlayerSession.Status}");
          Debug.Log($"IP Address: {result.PlayerSession.IpAddress}");
          Debug.Log($"Port: {result.PlayerSession.Port}");

          connState.playerSessionId = result.PlayerSession.PlayerSessionId;
          _photonManager.StartGame(GameMode.Client, connState);
        }
        else
        {
          throw new Exception("Response was OK but player session data is missing!");
        }
      }
      else
      {
        throw new Exception($"Failed to create player session. HTTP Status: {response.HttpStatusCode}");
      }
    }
    catch (Exception ex)
    {
      Debug.LogError($"General error: {ex.Message}");
    }
  }

  void ParseCommandLine()
  {
    try
    {
      string[] args = System.Environment.GetCommandLineArgs();
      for (int i = 1; i < args.Length; i++)
      {
        if (args[i].StartsWith("-") || args[i].StartsWith("/"))
        {
          string key = args[i].TrimStart('-', '/');

          if (i + 1 < args.Length && !args[i + 1].StartsWith("-") && !args[i + 1].StartsWith("/"))
          {
            parameters[key] = args[i + 1];
            i++;
          }
          else
          {
            parameters[key] = "true";
          }
        }
      }
    }
    catch (Exception ex)
    {
      Debug.LogError(ex);
    }
    finally
    {
      if (parameters.TryGetValue("region", out string region))
      {
        gameRegion = region;
        Debug.Log($"GAME REGION: {gameRegion}");
      }
      if (parameters.TryGetValue("port", out string port))
      {
        gamePort = ushort.Parse(port);
        Debug.Log($"GAME PORT: {gamePort}");
      }
    }
    
  }

  // Update is called once per frame
  void Update()
  {
        
  }
}
