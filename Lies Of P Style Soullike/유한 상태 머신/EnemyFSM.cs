using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AnimatorController))]
public class EnemyFSM : MonoBehaviour
{
    public StateMachine StateMachine { get; private set; }
    public EnemyStateType CurrentStateType { get; private set; }

    private Dictionary<EnemyStateType, EnemyState> states;

    public AnimatorController AnimatorController { get; private set; }

    private HitReceiver hitReceiver;
    [SerializeField] GameObject heavyAttackZone;
    [SerializeField] UIManager uiManager;

    private void Awake()
    {
        StateMachine = new StateMachine();

        AnimatorController = GetComponent<AnimatorController>();
        hitReceiver = GetComponent<HitReceiver>();

        states = new Dictionary<EnemyStateType, EnemyState>
        {
            { EnemyStateType.WaitForPlayer, null },
            { EnemyStateType.Idle, new EnemyIdleState(AnimatorController, GetComponent<EnemyMotor>()) },
            { EnemyStateType.ReadyAttack, new EnemyReadyAttackState(AnimatorController, GetComponent<EnemyMotor>()) },
            { EnemyStateType.Attack, new EnemyAttackState(AnimatorController, GetComponent<EnemyCombat>()) },
            { EnemyStateType.Hit, new EnemyHitState(AnimatorController, GetComponent<EnemyHit>(), GetComponent<EnemyController>()) },
            { EnemyStateType.Groggy, new EnemyGroggyState(AnimatorController, heavyAttackZone) },
            { EnemyStateType.Dead, new EnemyDeadState(AnimatorController, () => uiManager.SetThankYouForPlaying(true)) },
        };
    }

    private void Start()
    {
        heavyAttackZone.gameObject.SetActive(false);
    }

    private void Update()
    {
        StateMachine.Update();
    }

    public void ChangeState(EnemyStateType stateType, bool force = false)
    {
        if (!force && !CanChangeState(stateType))
            return;
        if (states[stateType] == null)
            return;

        CurrentStateType = stateType;

        StateMachine.ChangeState(states[stateType]);
    }

    private bool CanChangeState(EnemyStateType nextState)
    {
        if (CurrentStateType == EnemyStateType.Dead)
            return false;

        if (nextState == EnemyStateType.Dead)
            return true;

        if (nextState == EnemyStateType.Groggy)
            return true;

        if (CurrentStateType == EnemyStateType.Groggy)
        {
            if (nextState == EnemyStateType.Hit ||
                nextState == EnemyStateType.ReadyAttack ||
                nextState == EnemyStateType.Attack)
                return false;
        }

        if (CurrentStateType == EnemyStateType.ReadyAttack ||
            CurrentStateType == EnemyStateType.Attack)
        {
            if (nextState == EnemyStateType.Hit)
                return false;
        }

        return true;
    }

    // EnemyCombat에서 EndAttack()가 호출될 때 등록해놨음
    // 애니메이션에 등록하지는 않지만 형식을 맞추기 위해 EnemyFSM에 구현
    public void OnAttackEnd()
    {
        if (CurrentStateType == EnemyStateType.Attack)
            //ChangeState(EnemyStateType.Idle);
            GetComponent<EnemyController>().Idle();
    }

    // Hit 애니메이션이 끝날 때 등록해놨음
    public void OnHitAnimationEnd()
    {
        if (CurrentStateType == EnemyStateType.Hit)
            ChangeState(EnemyStateType.Idle);

        hitReceiver.SetInvincible(false);
    }
}