using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Amazon;
using Amazon.GameLift;
using Amazon.GameLift.Model;
using AmazonGameLift.Runtime;
using AmazonGameLiftPlugin.Core.CredentialManagement;
using AmazonGameLiftPlugin.Core.CredentialManagement.Models;
using AmazonGameLiftPlugin.Core.Shared.FileSystem;
using Aws.GameLift;
using Aws.GameLift.Server;
using System;
using Fusion;
using TMPro;
using System.Threading;
using Aws.GameLift.Server.Model;
using GameSession = Aws.GameLift.Server.Model.GameSession;

public class GameLiftServer : MonoBehaviour
{
  //Identify port number (hard coded here for simplicity) the game server is listening on for player connections
  public ushort gamePort = 7778;

  private static SynchronizationContext _unityContext;

  public PhotonManager _photonManager;
  public bool _isDedicatedServer = false;
  public bool _isDedicatedHost = false;

  private void Awake()
  {
    _unityContext = SynchronizationContext.Current;
  }

  //This example is a simple integration that initializes a game server process 
  //that is running on an Amazon GameLift Servers Anywhere fleet.
  void Start()
  {
    
    GenericOutcome initSDKOutcome = new();

#if UNITY_STANDALONE_OSX
    try
    {
      Debug.Log("UNITY_EDITOR || UNITY_STANDALONE_OSX");
      //WebSocketUrl from RegisterHost call
      string webSocketUrl = "wss://us-east-1.api.amazongamelift.com";

      //Unique identifier for your fleet that this host belongs to
      string fleetId = "arn:aws:gamelift:us-east-1:614954710407:fleet/fleet-17bf0e11-3443-4aff-abc4-85f8512b04fc";

      //Authorization token for this host process
      string authToken = "7fdf3671-bef2-4037-9375-eb26d697162c";

      //Unique identifier for your host that this process belongs to
      string hostId = "benxiwanAnywhere003";

      int _randomId = UnityEngine.Random.Range(0, 1000);
      //Unique identifier for this process
      string processId = $"myProcess-{_randomId}";

      Debug.Log($"webSocketUrl: {webSocketUrl} \n processId: {processId} \n hostId: {hostId} \n fleetId: {fleetId} \n authToken: {authToken}");

      //Server parameters are required for an Amazon GameLift Servers Anywhere fleet.
      //They are not required for an Amazon GameLift Servers managed EC2 fleet.
      ServerParameters serverParameters = new(
          webSocketUrl,
          processId,
          hostId,
          fleetId,
          authToken);
      //InitSDK establishes a local connection with an Amazon GameLift Servers agent 
      //to enable further communication.
      initSDKOutcome = GameLiftServerAPI.InitSDK(serverParameters);
    }
    catch (Exception ex)
    {
      Debug.Log(ex);
    }

#endif

#if UNITY_STANDALONE_LINUX
    initSDKOutcome = GameLiftServerAPI.InitSDK();
#endif

    var logParameters = new LogParameters(new List<string>() {
        //Here, the game server tells Amazon GameLift Servers where to find game session log files.
        //At the end of a game session, Amazon GameLift Servers uploads everything in the specified 
        //location and stores it in the cloud for access later.
        "/local/game/logs/myserver.log"
    });

    if (initSDKOutcome.Success)
    {
      //Implement callback functions
      ProcessParameters processParameters = new ProcessParameters(
          OnStartGameSession,
          OnUpdateGameSession,
          OnProcessTerminate,
          OnHealthCheck,
          gamePort,
          logParameters);

      //The game server calls ProcessReady() to tell Amazon GameLift Servers it's ready to host game sessions.
      var processReadyOutcome = GameLiftServerAPI.ProcessReady(processParameters);
      if (processReadyOutcome.Success)
      {
        Debug.Log("ProcessReady success.");
      }
      else
      {
        Debug.Log("ProcessReady failure : " + processReadyOutcome.Error.ToString());
      }
    }
    else
    {
      Debug.Log("InitSDK failure : " + initSDKOutcome.Error.ToString());
    }
  }

  private void StartFusion(GameSession gameSession)
  {
    Debug.Log(Thread.CurrentThread.ManagedThreadId == 1);
    Debug.Log(SynchronizationContext.Current != null);

    var gameSessionName = $"TestGameRoom{UnityEngine.Random.Range(0, 10000)}";
    var connState = new ConnectionState();
    connState.gamesession = gameSession;
    connState.sessionName = gameSessionName;

    if (_isDedicatedServer)
    {
      Debug.Log($"gameSessionName: {gameSessionName} SERVER");
      _photonManager.StartGame(GameMode.Server, connState);
    }
    else if (_isDedicatedHost)
    {
      Debug.Log($"gameSessionName: {gameSessionName} HOST");
      _photonManager.StartGame(GameMode.Host, connState);
    }
    else
    {
      Debug.Log($"gameSessionName: {gameSessionName} CLIENT");
    }
  }

  //Implement OnStartGameSession callback
  private void OnStartGameSession(GameSession gameSession)
  {
    //Amazon GameLift Servers sends a game session activation request to the game server 
    //with game session object containing game properties and other settings.
    //Here is where a game server takes action based on the game session object.
    //When the game server is ready to receive incoming player connections, 
    //it invokes the server SDK call ActivateGameSession().

    var outcome = GameLiftServerAPI.ActivateGameSession();
    if (!outcome.Success)
    {
      Debug.LogError($"OnStartGameSession Failed: {outcome.Error.ErrorMessage}");
    }
    else
    {
      Debug.Log($"OnStartGameSession Success");
      _unityContext.Post(state => {
        StartFusion(state as GameSession);
      }, gameSession);
    }
  }

  private void OnUpdateGameSession(UpdateGameSession updateSession)
  {
    //Amazon GameLift Servers sends a request when a game session is updated (such as for 
    //FlexMatch backfill) with an updated game session object. 
    //The game server can examine matchmakerData and handle new incoming players.
    //updateReason explains the purpose of the update.
  }

  private void OnProcessTerminate()
  {
    //Implement callback function OnProcessTerminate
    //Amazon GameLift Servers invokes this callback before shutting down the instance hosting this game server.
    //It gives the game server a chance to save its state, communicate with services, etc., 
    //and initiate shut down. When the game server is ready to shut down, it invokes the 
    //server SDK call ProcessEnding() to tell Amazon GameLift Servers it is shutting down.
    var outcome = GameLiftServerAPI.ProcessEnding();
    if (!outcome.Success)
    {
      Debug.LogError($"OnProcessTerminate Failed: {outcome.Error.ErrorMessage}");
    }
    else
    {
      Debug.Log($"OnProcessTerminate Success");
      _unityContext.Post((state) => {
        _photonManager.StopGame();
      }, null);
    }
  }

  private bool OnHealthCheck()
  {
    //Implement callback function OnHealthCheck
    //Amazon GameLift Servers invokes this callback approximately every 60 seconds.
    //A game server might want to check the health of dependencies, etc.
    //Then it returns health status true if healthy, false otherwise.
    //The game server must respond within 60 seconds, or Amazon GameLift Servers records 'false'.
    //In this example, the game server always reports healthy.
    Debug.Log("Health check!");
    return true;
  }

  void OnApplicationQuit()
  {
    Debug.Log($"OnApplicationQuit Success");
    //Make sure to call GameLiftServerAPI.ProcessEnding() and GameLiftServerAPI.Destroy() before terminating the server process.
    //These actions notify Amazon GameLift Servers that the process is terminating and frees the API client from memory. 
    GenericOutcome processEndingOutcome = GameLiftServerAPI.ProcessEnding();
    GameLiftServerAPI.Destroy();
    if (processEndingOutcome.Success)
    {
      //Environment.Exit(0);
    }
    else
    {
      Debug.Log("ProcessEnding() failed. Error: " + processEndingOutcome.Error.ToString());
      //Environment.Exit(-1);
    }
  }

  

}
