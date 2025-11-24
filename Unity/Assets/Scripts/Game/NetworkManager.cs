using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine.UI;
using System;
using Unity.VisualScripting;

[Serializable]
public class NetworkMessage
{
    public string type;
    public string playerId;
    public string message;
    public Vecter3Data position;
    public Vecter3Data rotation;   // 회전 추가
}


[Serializable]
public class Vecter3Data
{
    public float x;
    public float y;
    public float z;

    public Vecter3Data(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

public class NetworkManager : MonoBehaviour
{
    private WebSocket webSocket;
    [SerializeField] private string serverUrl = "ws://localhost:3000";

    [Header("UI Elements")]
    [SerializeField] private InputField messageInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private Text chatLog;
    [SerializeField] private Text statusText;

    [Header("PlayerSetting")]
    [SerializeField] private Transform localPlayer;          // 내 플레이어
    [SerializeField] private GameObject remotePlayerPrefabs; // 다른 플레이어 Prefabs
    [SerializeField] private float positionSendRate = 0.1f;  // 위치 전송 간격

    private string myPlayerId;
    private Dictionary<string, GameObject> remotePlayers = new Dictionary<string, GameObject>();
    private float lastPositionSendTime;


    void Start()
    {
        sendButton.onClick.AddListener(SendChatMessage);
        connectButton.onClick.AddListener(ConnectToServer);

        //enter 키로 메세지 전송
        if (messageInput != null)
        {
            messageInput.onEndEdit.AddListener((text) =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SendChatMessage();
                }
            });
        }
    }


    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (webSocket != null)
        {
            webSocket.DispatchMessageQueue();
        }
#endif
        // 일정 간격으로 내 위치/회전 값 전송
        if (webSocket != null && webSocket.State == WebSocketState.Open && localPlayer != null)
        {
            if (Time.time - lastPositionSendTime >= positionSendRate)
            {
                SendPositionUpdate();
                lastPositionSendTime = Time.time;
            }
        }

    }

    private async void ConnectToServer()
    {
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            AddToChatLog("[시스템] 이미 연결되어 있습니다.");
            return;
        }

        UpdateStatusText("연결 중...", Color.yellow);

        webSocket = new WebSocket(serverUrl);

        webSocket.OnOpen += () =>
        {
            UpdateStatusText("연결됨", Color.green);
            AddToChatLog("[시스템] 서버에 연결 되었습니다.");
        };

        webSocket.OnError += (e) =>
        {
            UpdateStatusText("연결됨", Color.green);
            AddToChatLog("[시스템] 에러 : {e}");
        };

        webSocket.OnClose += (e) =>
        {
            UpdateStatusText("연결됨", Color.red);
            AddToChatLog("[시스템] 서버와의 연결이 끊어졌습니다.");

            //연결 끊김 시 모든 원결 플레이어 제거
            foreach (var player in remotePlayers.Values)
            {
                if (player != null) Destroy(player);
            }
            remotePlayers.Clear();
        };

        webSocket.OnMessage += (bytes) =>
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            HandleMessage(message);
        };

        await webSocket.Connect();
    }


    private void HandleMessage(string json)
    {
        try
        {
            NetworkMessage message = JsonConvert.DeserializeObject<NetworkMessage>(json);

            switch (message.type)
            {
                case "connection":
                    myPlayerId = message.playerId;
                    AddToChatLog($"[시스템 {message.message} (당신의 ID : {myPlayerId}");
                    break;
                case "chat":
                    string displayName = message.playerId == myPlayerId ? "나" : message.playerId;
                    AddToChatLog($"[{displayName}] {message.message}");
                    break;
                case "playerJoin":
                    if (message.playerId != myPlayerId)
                    {
                        AddToChatLog($"[시스템] {message.playerId} 님이 입장했습니다.");
                        CreateRemotePlayer(message.playerId, message.position, message.rotation);
                    }
                    break;
                case "playerDisconnect":
                    AddToChatLog($"[시스템] {message.playerId} 님이 퇴장 했습니다. ");
                    RemoveRemotePlayer(message.playerId);
                    break;
                case "positionUpdate":
                    if (message.playerId != myPlayerId)
                    {
                        UpdateRemotePlayer(message.playerId, message.position, message.rotation);
                    }
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"메세지 처리중 에러 : {e.Message}");

        }
    }

    private async void SendChatMessage()
    {
        if (string.IsNullOrEmpty(messageInput.text)) return;

        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            AddToChatLog("[시스템] 서버에 연결되지 않았습니다.");
            return;
        }

        NetworkMessage message = new NetworkMessage()
        {
            type = "chat",
            message = messageInput.text
        };

        await webSocket.SendText(JsonConvert.SerializeObject(message));
        messageInput.text = "";
        messageInput.ActivateInputField();              //입력창 다시 활성화
    }

    private async void SendPositionUpdate()
    {
        if (localPlayer == null) return;

        NetworkMessage message = new NetworkMessage
        {
            type = "positionUpdate",
            position = new Vecter3Data(localPlayer.position),
            rotation = new Vecter3Data(localPlayer.eulerAngles)
        };

        await webSocket.SendText(JsonConvert.SerializeObject(message));
    }

    private void AddToChatLog(string message)
    {
        if (chatLog != null)
        {
            chatLog.text += $"\n{message}";
        }
    }

    private void UpdateStatusText(string status, Color color)
    {
        if (statusText != null)
        {
            statusText.text = status;
            statusText.color = color;
        }
    }

    private async void OnApplicationQuit()
    {
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            await webSocket.Close();
        }
    }

    private void CreateRemotePlayer(string playerId, Vecter3Data position, Vecter3Data rotation)
    {
        if (remotePlayers.ContainsKey(playerId)) return;
        if (remotePlayerPrefabs == null)
        {
            Debug.LogError("RemotePlayerPrefab이 설정 되지 않았습니다.");
            return;
        }

        Vector3 pos = position != null ? position.ToVector3() : Vector3.zero;
        Vector3 rot = rotation != null ? rotation.ToVector3() : Vector3.zero;

        GameObject player = Instantiate(remotePlayerPrefabs, pos, Quaternion.Euler(rot));
        player.name = "RemotePlayer_" + playerId;
        remotePlayers.Add(playerId, player);

        Debug.Log($"원격 플레이어 생성 : {playerId} at {pos} , rotation {rot}");
    }

    private void RemoveRemotePlayer(string playerId)
    {
        if (remotePlayers.ContainsKey(playerId))
        {
            Destroy(remotePlayers[playerId]);
            remotePlayers.Remove(playerId);
            Debug.Log($"원격 플레이어 제거 : {playerId}");
        }
    }

    private void UpdateRemotePlayer(string playerId, Vecter3Data position, Vecter3Data rotation)
    {
        if (!remotePlayers.ContainsKey(playerId))       // 플레이어가 없으면 생성
        {
            CreateRemotePlayer(playerId, position, rotation);
            return;
        }

        GameObject player = remotePlayers[playerId];
        if (player == null) return;

        if (position != null)       // 부드러운 이동
        {
            player.transform.position = Vector3.Lerp(player.transform.position, position.ToVector3(), Time.deltaTime * 10f);
        }

        if (rotation != null)       // 부드러운 회전
        {
            Quaternion targetRotation = Quaternion.Euler(rotation.ToVector3());
            player.transform.rotation = Quaternion.Lerp(player.transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }
    
    private void OnDestroy()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(SendChatMessage);
        }
        if (connectButton != null)
        {
            connectButton.onClick.RemoveListener(ConnectToServer);
        }
    }
}
