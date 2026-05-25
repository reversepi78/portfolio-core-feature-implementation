using System;
using UnityEngine;
using UnityEngine.InputSystem.HID;

/// <summary>
/// 모든 플레이어 State의 부모 클래스.
/// PlayerFSM 참조를 공통으로 관리함.
/// </summary>
public abstract class PlayerState : IState
{
    protected AnimatorController animatorController;

    protected PlayerState(AnimatorController _animatorController)
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

public class PlayerMoveState : PlayerState
{
    protected PlayerMotor motor;
    protected PlayerController playerController;

    public PlayerMoveState(AnimatorController animatorController, PlayerMotor _motor, PlayerController _playerController) : base(animatorController)
    {
        if (_motor == null)
            Debug.LogError("PlayerMoveState 생성자 : _motor에 null이 할당됨");

        motor = _motor;
        playerController = _playerController;
    }

    public override void Enter()
    {
        animatorController.Play("Move");
    }

    public override void Update()
    {
        Vector2 moveInput = motor.MoveInput;
        if (moveInput != Vector2.zero && !playerController.IsLockOn)
            moveInput = new Vector2(0, 1);

        animatorController.SetAnimation("Move", moveInput);

        motor.Move();
    }
}

public class PlayerAttackState : PlayerState
{
    private PlayerCombat combat;
    private int currentComboIndex;

    public PlayerAttackState(AnimatorController animatorController, PlayerCombat combat) : base(animatorController)
    {
        this.combat = combat;
    }

    public override void Enter()
    {
        combat.Attack();
        if (!combat.HeavyAttack)
        {
            currentComboIndex = combat.ComboIndex;

            PlayAttackAnimation();
        }
        else
        {
            animatorController.Play($"Attack_Heavy"); // 클립명으로 직접 호출
        }
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
        combat.EndAttack();
        currentComboIndex = 0;
    }

    private void PlayAttackAnimation()
    {
        animatorController.Play($"Attack_Basic_{currentComboIndex}"); // 클립명으로 직접 호출
    }
}

public class PlayerDodgeState : PlayerState
{
    protected PlayerDodge dodge;

    public PlayerDodgeState(AnimatorController animatorController, PlayerDodge _dodge) : base(animatorController)
    {
        if (_dodge == null)
            Debug.LogError("PlayerDodgeState 생성자 : _dodge에 null이 할당됨");

        dodge = _dodge;
    }

    public override void Enter()
    {
        animatorController.SetAnimation("Dodge");

        dodge.StartDodge();
    }

    public override void Update()
    {
        dodge.UpdateDodge();
    }

    public override void Exit()
    {
        dodge.StopDodge();
    }
}

public class PlayerHitState : PlayerState
{
    protected PlayerHit hit;
    private bool knockdowned;

    public PlayerHitState(AnimatorController animatorController, PlayerHit _hit) : base(animatorController)
    {
        if (_hit == null)
            Debug.LogError("PlayerGuardState 생성자 : _hit에 null이 할당됨");

        hit = _hit;
    }

    public override void Enter()
    {
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

public class PlayerDeadState : PlayerState
{
    Action loadMainScene;

    private float delayTime = 3.5f;
    private float timer = 0f;
    private bool isCalled = false;

    public PlayerDeadState(AnimatorController animatorController, Action _loadMainScene) : base(animatorController) {
        loadMainScene = _loadMainScene;
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
            loadMainScene();
        }
    }
}