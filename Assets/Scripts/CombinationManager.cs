using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class CombinationManager : MonoBehaviour {

    public static CombinationManager Instance;

    [SerializeField] [ReadOnly] private string currentCombination;
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private OptionsListViewCustom optionsListView;

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
        if (!optionsListView.IsWaitingForOption) {
            Debug.LogWarning("Not ready for option!");
            return;
        }

        var options = optionsListView.Options;
        foreach (var option in options) {
            if (option.Line.Metadata[0] == currentCombination) {
                optionsListView.OptionViewWasSelected(option);
                return;
            }
        }

        Debug.LogWarning("No match!");
    }

    public void ClearCombination() {
        currentCombination = "";
    }
}
