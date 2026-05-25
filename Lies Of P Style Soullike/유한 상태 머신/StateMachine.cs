/// <summary>
/// 현재 State를 관리하고 상태 전환을 담당하는 FSM 핵심 클래스.
/// Enter → Update → Exit 흐름을 관리함.
/// </summary>

public class StateMachine
{
    public IState CurrentState { get; private set; }

    public void Initialize(IState startState)
    {
        CurrentState = startState;
        CurrentState.Enter();
    }

    public void ChangeState(IState newState)
    {
        if (newState == null)
            return;

        if (CurrentState == newState)
            return;

        CurrentState?.Exit();

        CurrentState = newState;

        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}