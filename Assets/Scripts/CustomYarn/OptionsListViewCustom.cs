using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Yarn.Unity;

public class OptionsListViewCustom : OptionsListView {
    public DialogueOption[] Options { get; private set; }
    private Action<int> _onOptionSelected;
    public bool IsWaitingForOption { get; private set; } = false;

    public override void RunOptions(DialogueOption[] dialogueOptions, Action<int> onOptionSelected) {
        base.RunOptions(dialogueOptions, onOptionSelected);

        _onOptionSelected = onOptionSelected;
        Options = dialogueOptions;
        IsWaitingForOption = true;

        foreach (var option in dialogueOptions) {
            if (option.Line.Metadata != null) {
                print($"Option '{option.Line.Text.Text}' has key '{option.Line.Metadata[0]}'");
            } else {
                Debug.LogWarning($"Option '{option.Line.Text.Text}' is missing a key!");
            }
        }
    }

    public void OptionViewWasSelected(DialogueOption option) {
        StartCoroutine(OptionViewWasSelectedInternal(option));
    }

    IEnumerator OptionViewWasSelectedInternal(DialogueOption selectedOption) {
        yield return StartCoroutine(Effects.FadeAlpha(GetComponent<CanvasGroup>(), 1f, 0f, 0.1f));
        _onOptionSelected(selectedOption.DialogueOptionID);
        Options = new DialogueOption[] { };
        IsWaitingForOption = false;
    }
}