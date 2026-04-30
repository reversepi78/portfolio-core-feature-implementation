using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [SerializeField] GameObject title, roomListPanel, makeRoomPanel, waitingRoomPanel;

    static LobbyManager instance;
    static public LobbyManager Instance => instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        roomCache = new();

        PhotonNetwork.AutomaticallySyncScene = true;

        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);

    }

    void Start()
    {
        if (!PhotonNetwork.IsConnected)
            ConnectToMasterServer();
        else if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.CurrentRoom.IsOpen = true;
            PhotonNetwork.CurrentRoom.IsVisible = true;

            ActivePanel("WaitingRoom");
            SetWaitingRoom();
        }
        else
        {
            ActivePanel();
        }
    }

    public override void OnConnectedToMaster() // 마스터 서버 접속 성공 시 자동 실행 
    {
        PhotonNetwork.JoinLobby();

        SetTitle(true);
    }

    public override void OnDisconnected(DisconnectCause cause) // 마스터 서버 접속 실패 시 자동 실행
    {
        ConnectToMasterServer();
    }

    void ConnectToMasterServer()
    {
        ActivePanel();

        SetTitle(false);

        PhotonNetwork.ConnectUsingSettings(); // 마스터 서버 접속 시도
    }

    void SetTitle(bool isConnectedToMasterServer)
    {
        for (int i = 0; i < title.transform.childCount; i++)
        {
            if (title.transform.GetChild(i).TryGetComponent<TMP_Text>(out TMP_Text tmp_Text))
            {
                tmp_Text.gameObject.SetActive(!isConnectedToMasterServer);
            }
            else if (title.transform.GetChild(i).TryGetComponent<Button>(out Button button))
            {
                button.gameObject.SetActive(isConnectedToMasterServer);
            }
        }
    }

    public override void OnJoinedLobby()
    {
    }

    public void ActivePanel(string panelName = "")
    {
        title.gameObject.SetActive(false);
        roomListPanel.gameObject.SetActive(false);
        makeRoomPanel.gameObject.SetActive(false);
        waitingRoomPanel.gameObject.SetActive(false);

        switch (panelName)
        {
            case "RoomList":
                PhotonNetwork.NickName = "Player";
                joinRoom_PlayerName.text = "";
                roomListPanel.gameObject.SetActive(true);
                ActiveFullMemberMessagePanel(false);
                break;
            case "MakeRoom":
                PhotonNetwork.NickName = "Player";
                roomNameInput.text = makeRoom_PlayerName.text = "";
                makeRoomPanel.gameObject.SetActive(true); 
                break;
            case "WaitingRoom": 
                waitingRoomPanel.gameObject.SetActive(true); 
                break;
            default : 
                title.gameObject.SetActive(true);
                if (PhotonNetwork.InRoom)
                {
                    PhotonNetwork.LeaveRoom();
                }
                break;
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    Dictionary<string, (RoomInfo ri, GameObject entryRoom)> roomCache = new Dictionary<string, (RoomInfo, GameObject)>();
    [SerializeField] Transform roomListScrollContent;
    [SerializeField] Button entryRoomButtonPrefab; 
    public override void OnRoomListUpdate(List<RoomInfo> roomList) // 룸 리스트에 변경이 생기면 실행
    {
        foreach (RoomInfo room in roomList)
        {
            if (room.RemovedFromList)
            {
                if (!roomCache.TryGetValue(room.Name, out var cachedRoom))
                    return;

                Destroy(roomCache[room.Name].entryRoom);
                roomCache.Remove(room.Name);
            }
            else if (!roomCache.ContainsKey(room.Name))
            {
                GameObject entryRoom = Instantiate(entryRoomButtonPrefab.gameObject, roomListScrollContent);
                entryRoom.SetActive(true);
                entryRoom.GetComponent<Button>().onClick.AddListener(() => JoinRoom(room.Name));
                roomCache[room.Name] = (room, entryRoom);
                UpdateEntryRoomButtonPrefab(room, entryRoom);
            }
            else if (roomCache.ContainsKey(room.Name))
            {
                (RoomInfo ri, GameObject entryRoom) oldRoom = roomCache[room.Name];

                if (oldRoom.ri.PlayerCount != room.PlayerCount)
                {
                    UpdateEntryRoomButtonPrefab(oldRoom.ri, oldRoom.entryRoom);
                }
            }
        }

        void UpdateEntryRoomButtonPrefab(RoomInfo ri, GameObject entryRoom)
        {
            TMP_Text roomName = entryRoom.GetComponentInChildren<TMP_Text>();
            roomName.text = ri.Name;

            Slider members = entryRoom.GetComponentInChildren<Slider>();
            members.value = ri.PlayerCount;
        }
    }

    [SerializeField] TMP_InputField joinRoom_PlayerName;
    public void JoinRoom(string roomName)
    {
        PhotonNetwork.NickName = joinRoom_PlayerName.text;

        PhotonNetwork.JoinRoom(roomName);
    }

    [SerializeField] GameObject fullMemberMessage;
    public void ActiveFullMemberMessagePanel(bool active)
    {
        fullMemberMessage.SetActive(active);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        ActiveFullMemberMessagePanel(true);
    }

    [SerializeField] TMP_InputField roomNameInput, makeRoom_PlayerName;
    public void MakeRoom()
    {
        RoomOptions options = new RoomOptions()
        {
            MaxPlayers = 4,
            IsOpen = true,
            IsVisible = true // 룸 리스트에 보이게 할지 여부
        };

        PhotonNetwork.NickName = makeRoom_PlayerName.text;

        string roomNameStr = roomNameInput.text;
        if (string.IsNullOrEmpty(roomNameStr))
            roomNameStr = "Game Room";
        if (roomCache.ContainsKey(roomNameStr))
            roomNameStr += "_";
        PhotonNetwork.CreateRoom(roomNameStr, options);
    }

    public override void OnJoinedRoom()
    {
        ActivePanel("WaitingRoom");

        SetWaitingRoom();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        SetWaitingRoom();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        SetWaitingRoom();
    }

    public override void OnLeftRoom()
    {
        ActivePanel();
    }

    [SerializeField] Transform playerName, namesContent;
    [SerializeField] Button startButton;
    [SerializeField] TMP_Text roomName;
    void SetWaitingRoom()
    {
        roomName.text = PhotonNetwork.CurrentRoom.Name;

        for (int i = namesContent.childCount - 1; i >= 0; i--)
        {
            Destroy(namesContent.GetChild(i).gameObject);
        }

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string playerNameStr = player.NickName;
            if (player.IsMasterClient)
                playerNameStr += " (Host)";

            GameObject _playerName = Instantiate(playerName.gameObject, namesContent);
            _playerName.gameObject.SetActive(true);
            _playerName.GetComponentInChildren<TMP_Text>().text = playerNameStr;
        }

        if (PhotonNetwork.IsMasterClient)
            startButton.interactable = true;
        else
            startButton.interactable = false;
    }

    public void StartGame()
    {
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        StartCoroutine(EnterIngameAfterFading());

        IEnumerator EnterIngameAfterFading()
        {
            photonView.RPC("FadeOnEnterGame", RpcTarget.All, true);

            while (FadeManager.Instance.isFading)
                yield return null;

            PhotonNetwork.LoadLevel("Main");
        }
    }

    [PunRPC]
    void FadeOnEnterGame(bool fadeIn)
    {
        FadeManager.Instance.Fade(fadeIn);
    }
}
