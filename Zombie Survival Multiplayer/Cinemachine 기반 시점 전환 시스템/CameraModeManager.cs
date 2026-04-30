using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraModeManager : MonoBehaviour
{
    static CameraModeManager instance;
    public static CameraModeManager Instance { get => instance; }
    public static event Action<CameraMode> SetCameraMode;

    CameraMode currentCameraMode;
    public CameraMode CurrentCameraMode => currentCameraMode;

    [SerializeField] CinemachineVirtualCamera topDown;
    CinemachineVirtualCamera fps;
    public CinemachineVirtualCamera FPS { get => fps; set
        {
            fps = value;
            cvcDic[CameraMode.FPS] = FPS;
        }
    }

    Dictionary<CameraMode, CinemachineVirtualCamera> cvcDic;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;


        cvcDic = new()
        {
            {CameraMode.TopDown,  topDown}
        };
    }

    private void Start()
    {
        SetCameraMode(CameraMode.TopDown);

        SetCameraMode += SetCurrentCameraMode;

        void SetCurrentCameraMode(CameraMode cm)
        {
            currentCameraMode = cm;

            topDown.Priority = FPS.Priority = 1;

            cvcDic[cm].Priority = 2;
        }
    }

    private void Update()
    {
        if (!GameManager.instance.isGameover && GameManager.instance.isInGame && Input.GetKeyDown(KeyCode.V)) // v로 시점 변환
        {
            if (currentCameraMode == CameraMode.TopDown)
                currentCameraMode = CameraMode.FPS;
            else
                currentCameraMode = CameraMode.TopDown;

            SetCameraMode(currentCameraMode);
        }
    }

    private void OnDestroy()
    {
        SetCameraMode = null;
    }
}

public enum CameraMode
{
    TopDown,
    FPS
}
