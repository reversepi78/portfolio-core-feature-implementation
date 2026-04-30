using Cinemachine;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Analytics;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviourPunCallbacks, IPunObservable
{
    public static GameManager instance
    {
        get => m_instance;
    }

    static GameManager m_instance;

    private int score = 0; // 현재 게임 점수
    public int Score => score;
    public bool isInGame { get; set; }
    public bool isGameover { get; private set; }

    public static event Action GameOver;

    private void Awake()
    {
        if(m_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_instance = this;

        isInGame = isGameover = false;

        players = new();

        countDown.gameObject.SetActive(false);

        GameOver += GameOverSetting;
    }

    [SerializeField] GameObject playerPrefab;
    private void Start()
    {
        StartCoroutine(EnterIngame());
    }

    public List<PlayerHealth> players { get; set; }
    private void Update()
    {
        if(PhotonNetwork.IsMasterClient && isInGame && !isGameover)
        {
            foreach (PlayerHealth ph in players)
            {
                if (!ph.dead)
                    return;
            }

            GameOver(); // 플레이어가 모두 사망한 경우
        }
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("Lobby");
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(score);
            stream.SendNext(isGameover);
        }
        else
        {
            score = (int)stream.ReceiveNext();
            UIManager.instance.UpdateScoreText(score);
            isGameover = (bool)stream.ReceiveNext();
        }
    }

    [SerializeField] TMP_Text countDown;
    [SerializeField] AudioClip countDownSFX;
    IEnumerator EnterIngame()
    {
        photonView.RPC("FadeOnEnterGame", RpcTarget.All, false);

        SetLocalPlayer();

        while (FadeManager.Instance.isFading)
            yield return null;

        FadeManager.Instance.FadeImage.gameObject.SetActive(false);

        AudioSource audioSource = GetComponent<AudioSource>();

        countDown.gameObject.SetActive(true);
        int count = 3;
        while (count > 0)
        {
            countDown.text = count.ToString();
            if (audioSource != null)
                audioSource.PlayOneShot(countDownSFX);

            yield return new WaitForSeconds(1);
            count -= 1;
        }
        countDown.gameObject.SetActive(false);

        isInGame = true;

        if(audioSource != null)
            audioSource.Play();
    }

    [PunRPC]
    void FadeOnEnterGame(bool fadeIn)
    {
        FadeManager.Instance.Fade(fadeIn);
    }

    [SerializeField] Transform playerPositions;
    [SerializeField] CinemachineVirtualCamera topDownCamera;
    [SerializeField] FollowingPlayerCamera minimapCamera;
    void SetLocalPlayer()
    {
        int index = PhotonNetwork.LocalPlayer.ActorNumber - 1;
        Transform currentPosition = playerPositions.GetChild(index);

        Transform playerCharacter = PhotonNetwork.Instantiate(playerPrefab.name, currentPosition.position, currentPosition.rotation).transform;
        topDownCamera.Follow = playerCharacter;
        topDownCamera.LookAt = playerCharacter;
        minimapCamera.LocalPlayer = playerCharacter;
        playerCharacter.GetComponent<PlayerHealth>().SetHealthSlider();
        CameraModeManager.Instance.FPS = playerCharacter.GetComponent<PlayerSetup>().fpsCamera;
    }

    public void AddScore(int newScore)
    {
        if (!isGameover)
        {
            score += newScore;
            UIManager.instance.UpdateScoreText(score);
        }
    }

    public void GameOverSetting()
    {
        isInGame = false;
        isGameover = true;

        photonView.RPC("SetActiveGameoverUI", RpcTarget.All);
        photonView.RPC("SetBasicCursur", RpcTarget.All);
    }

    [PunRPC]
    void SetActiveGameoverUI()
    {
        UIManager.instance.SetActiveGameoverUI();
    }

    [PunRPC]
    void SetBasicCursur()
    {
        CursurManager.Instance.SetBasicCursur();
    }
}
