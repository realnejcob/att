using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class CombinationManager : MonoBehaviour {
    public static CombinationManager Instance;

    [SerializeField] [ReadOnly] private string currentCombination;
    [SerializeField] private DialogueOptionList dialogueOptionList;

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
        dialogueOptionList.TrySelectOptionByCombination(currentCombination);
    }

    public void ClearCombination() {
        currentCombination = "";
    }
}
