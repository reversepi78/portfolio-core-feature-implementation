using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 행동 상태를 관리하는 FSM.
/// 상태 전환 및 상태 제한 규칙을 담당함.
/// </summary>
[RequireComponent(typeof(AnimatorController))]
[RequireComponent(typeof(PlayerStatus))]
public class PlayerFSM : MonoBehaviour
{
    private PlayerStatus playerStatus;

    public StateMachine StateMachine { get; private set; }
    public PlayerStateType CurrentStateType { get; private set; }

    private Dictionary<PlayerStateType, PlayerState> states;

    public AnimatorController AnimatorController { get; set; }

    private HitReceiver hitReceiver;

    [SerializeField] UIManager uiManager;

    private void Awake()
    {
        if(playerStatus == null) playerStatus = GetComponent<PlayerStatus>();

        StateMachine = new StateMachine();

        AnimatorController = GetComponent<AnimatorController>();
        hitReceiver = GetComponent<HitReceiver>();

        // PlayerController는 생성될 때 반드시 PlayerFSM, PlayerMotor, PlayerCombat, PlayerGuard가 존재하도록 구현했음
        states = new Dictionary<PlayerStateType, PlayerState>
        {
            { PlayerStateType.Move, new PlayerMoveState(AnimatorController, GetComponent<PlayerMotor>(), GetComponent<PlayerController>()) },
            { PlayerStateType.Attack, new PlayerAttackState(AnimatorController, GetComponent<PlayerCombat>()) },
            { PlayerStateType.Dodge, new PlayerDodgeState(AnimatorController, GetComponent<PlayerDodge>()) },
            { PlayerStateType.Hit, new PlayerHitState(AnimatorController, GetComponent<PlayerHit>()) },
            { PlayerStateType.Dead, new PlayerDeadState(AnimatorController, () => uiManager.LoadScene(SceneType.Main.ToString())) },
        };
    }

    private void Update()
    {
        StateMachine.Update();
    }

    public void ChangeState(PlayerStateType stateType)
    {
        if (!CanChangeState(stateType))
            return;

        CurrentStateType = stateType;

        StateMachine.ChangeState(states[stateType]);
    }

    protected bool CanChangeState(PlayerStateType nextState)
    {
        if (CurrentStateType == PlayerStateType.Dead)
            return false;

        if (nextState == PlayerStateType.Attack && !playerStatus.CanConsumeStamina(playerStatus.AttackStaminaCost))
        {
            return false;
        }

        if (nextState == PlayerStateType.Dodge && !playerStatus.CanConsumeStamina(playerStatus.DodgeStaminaCost))
        {
            return false;
        }

        if (CurrentStateType == PlayerStateType.Attack)
        {
            if (nextState == PlayerStateType.Dodge)
            {
                return false;
            }
        }

        if (CurrentStateType == PlayerStateType.Dodge)
        {
            if (nextState == PlayerStateType.Attack)
            {
                return false;
            }
        }

        if (CurrentStateType == PlayerStateType.Hit)
        {
            if (nextState == PlayerStateType.Attack
                || nextState == PlayerStateType.Dodge) // hit 애니메이션 실행 중에는 다시 hit 상태로 전환하지 않음
                return false;
        }

        return true;
    }

    // PlayerCombat에서 EndAttack()가 호출될 때 등록해놨음
    // 애니메이션에 등록하지는 않지만 형식을 맞추기 위해 PlayerFSM에 구현
    public void OnAttackEnd()
    {
        if (CurrentStateType == PlayerStateType.Attack)
            ChangeState(PlayerStateType.Move);
    }

    // Dodge 애니메이션이 끝날 때 등록해놨음
    // Hit 애니메이션이 시작할 때 등록해놨음
    public void OnDodgeEnd()
    {
        if (CurrentStateType == PlayerStateType.Dodge)
            ChangeState(PlayerStateType.Move);
    }

    // Hit 애니메이션이 끝날 때 등록해놨음
    public void OnHitEnd()
    {
        if (CurrentStateType == PlayerStateType.Hit)
            ChangeState(PlayerStateType.Move);

        hitReceiver.SetInvincible(false);
    }
}