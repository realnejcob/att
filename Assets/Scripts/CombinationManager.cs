using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombinationManager : MonoBehaviour {

    public static CombinationManager Instance;

    [SerializeField] [ReadOnly] private string currentCombination;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        } else {
            Destroy(gameObject);
        }
    }

    public void AppendToCombination(string toAppend) {
        if (toAppend == "#") {
            CommitCombination();
            ClearCombination();
            return;
        }
        currentCombination += toAppend;
    }

    public void CommitCombination() {

    }

    public void ClearCombination() {
        currentCombination = "";
    }
}
