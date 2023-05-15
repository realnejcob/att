using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StateMachineBase : MonoBehaviour {
    public State CurrentState { get; private set; }
    private void Update() {
        CurrentState?.Tick(Time.deltaTime);
    }

    public void SetState(State state) {
        CurrentState?.Exit();
        CurrentState = state;
        CurrentState.Enter();
    }
}
