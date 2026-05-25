using System;
using UnityEngine;

/// <summary>
/// 모든 적 State의 부모 클래스.
/// EnemyFSM 참조를 공통으로 관리함.
/// </summary>
public abstract class EnemyState : IState
{
    protected AnimatorController animatorController;

    protected EnemyState(AnimatorController _animatorController)
    {
        animatorController = _animatorController;
    }

    // 상태 진입 시 실행되는 로직
    public virtual void Enter()
    {
        // 진입할 때 애니메이션 재생
    }
    // 상태 유지 시 매 프레임마다 실행되는 로직
    public virtual void Update() { }
    // 상태 종료 시 실행되는 로직
    public virtual void Exit() { }
}

public class EnemyIdleState : EnemyState
{
    protected EnemyMotor motor;

    public EnemyIdleState(AnimatorController animatorController, EnemyMotor _motor) : base(animatorController)
    {
        if (_motor == null)
            Debug.LogError("EnemyIdleState 생성자 : _motor에 null이 할당됨");

        motor = _motor;
    }

    public override void Enter()
    {
        animatorController.Play("Walk");
    }

    public override void Update()
    {
        animatorController.SetAnimation("Walk", motor.moveInput);
        motor.Walk();
    }
}

public class EnemyReadyAttackState : EnemyState
{
    private EnemyMotor motor;

    public EnemyReadyAttackState(AnimatorController animatorController, EnemyMotor _motor) : base(animatorController)
    {
        if (_motor == null)
            Debug.LogError("EnemyReadyAttackState 생성자 : _motor에 null이 할당됨");

        motor = _motor;
    }

    public override void Enter()
    {
        animatorController.SetAnimation("StartChase");
    }

    public override void Update()
    {
        motor.Chase();
    }
}

public class EnemyAttackState : EnemyState
{
    protected EnemyCombat combat;
    private int currentComboIndex;

    public EnemyAttackState(AnimatorController animatorController, EnemyCombat _combat) : base(animatorController)
    {
        if (_combat == null)
            Debug.LogError("EnemyAttackState 생성자 : _combat에 null이 할당됨");

        combat = _combat;
    }

    public override void Enter()
    {
        combat.StartAttack();
        PlayAttackAnimation();
    }

    public override void Update()
    {
        if (currentComboIndex != combat.ComboIndex) // 콤보 인덱스가 변경되었다면 다음 공격 애니메이션 재생
        {
            currentComboIndex = combat.ComboIndex;
            PlayAttackAnimation();
        }
    }

    public override void Exit()
    {
        currentComboIndex = 0;
    }

    private void PlayAttackAnimation()
    {
        string clipName = $"Attack_{combat.CurrentPatternNum}_{currentComboIndex}";
        animatorController.Play(clipName); // 클립명으로 직접 호출
    }
}

public class EnemyHitState : EnemyState
{
    protected EnemyHit hit;
    EnemyController enemyController;
    private bool knockdowned;

    public EnemyHitState(AnimatorController animatorController, EnemyHit _hit, EnemyController _enemyController) : base(animatorController)
    {
        if (_hit == null)
            Debug.LogError("EnemyHitState 생성자 : _hit에 null이 할당됨");

        hit = _hit;
        enemyController = _enemyController;
    }

    public override void Enter()
    {
        if (enemyController.CanEnterReadyAttack)
        {
            return;
        }

        knockdowned = false;

        if (!hit.HitReceiver.IsKnockdown)
            animatorController.Play("Hit");
        else
        {
            knockdowned = true;
            animatorController.Play("Knockdown");
        }
    }

    public override void Update()
    {
        if (knockdowned && !hit.HitReceiver.IsKnockdown) // 넉다운 상태에서 벗어난 경우
        {
            knockdowned = false;
            animatorController.Play("GettingUp");
        }
    }

    public override void Exit()
    {
        knockdowned = false;
    }
}

public class EnemyGroggyState : EnemyState
{
    GameObject heavyAttackZone;

    public EnemyGroggyState(AnimatorController animatorController, GameObject _heavyAttackZone) : base(animatorController) {
        if(_heavyAttackZone == null)
            Debug.LogError("EnemyHitState 생성자 : _heavyAttackZone에 null이 할당됨");
        heavyAttackZone = _heavyAttackZone;
    }

    public override void Enter()
    {
        animatorController.Play("Groggy");
        heavyAttackZone.SetActive(true);
    }

    public override void Exit()
    {
        heavyAttackZone.SetActive(false);
    }
}

public class EnemyDeadState : EnemyState
{
    Action activeTFP;

    private float delayTime = 5.5f;
    private float timer = 0f;
    private bool isCalled = false;

    public EnemyDeadState(AnimatorController animatorController, Action _activeTFP) : base(animatorController)
    {
        activeTFP = _activeTFP;
    }

    public override void Enter()
    {
        animatorController.SetAnimation("Die");
    }

    public override void Update()
    {
        if (isCalled)
            return;

        timer += Time.deltaTime;
        Debug.Log(timer);

        if (timer >= delayTime)
        {
            isCalled = true;
            activeTFP();
        }
    }
}