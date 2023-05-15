using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class DialogueOptionList : DialogueViewBase {
    private DialogueOption[] options;
    private Action<int> _onOptionSelected;
    private bool isWaiting = false;

    [SerializeField] private DialogueOptionView optionViewPrefab;
    [SerializeField] private List<DialogueOptionView> dialogueOptionViews = new List<DialogueOptionView>();

    [SerializeField] private GameObject numpadMask;
    [SerializeField] private GameObject continueButton;

    public override void RunOptions(DialogueOption[] dialogueOptions, Action<int> onOptionSelected) {
        options = dialogueOptions;
        _onOptionSelected = onOptionSelected;
        isWaiting = true;

        numpadMask.SetActive(false);
        continueButton.SetActive(false);

        CreateExtraViews();

        for (int i = 0; i < dialogueOptions.Length; i++) {
            var curOption = dialogueOptions[i];
            var curOptionView = dialogueOptionViews[i];

            curOptionView.gameObject.SetActive(true);
            curOptionView.ShowAnimation(i * 0.5f);
            curOptionView.SetText(curOption.Line.Text.Text);
        }
    }

    public bool TrySelectOptionByCombination(string combination) {
        foreach (var option in options) {
            if (option.Line.Metadata.Length == 0)
                continue;

            if (option.Line.Metadata[0] == combination) {
                SelectOption(option);
                return true;
            }
        }

        return false;
    }

    private void SelectOption(DialogueOption selectedOption) {
        if (!isWaiting)
            return;

        StartCoroutine(Co());

        IEnumerator Co() {
            isWaiting = false;
            numpadMask.SetActive(true);

            HideAllOptionViews(selectedOption.DialogueOptionID);

            yield return new WaitForSeconds(2);

            _onOptionSelected(selectedOption.DialogueOptionID);
            continueButton.SetActive(true);
        }
    }

    private void SelectOptionByID(int optionID) {
        if (!isWaiting)
            return;

        if (optionID > options.Length - 1)
            return;

        _onOptionSelected(optionID);
        isWaiting = false;

        numpadMask.SetActive(true);
        continueButton.SetActive(true);

        HideAllOptionViews(optionID);
    }

    private void HideAllOptionViews(int selectedOptionID) {
        for (int i = 0; i < dialogueOptionViews.Count; i++) {
            var curOptionView = dialogueOptionViews[i];
            if (i == selectedOptionID) {
                curOptionView.Highlight();
            } else {
                curOptionView.FadeOut();
            }
        }
    }

    private void CreateExtraViews() {
        var surplus = options.Length - dialogueOptionViews.Count;
        if (surplus <= 0)
            return;

        for (int i = 0; i < surplus; i++) {
            var newOptionView = Instantiate(optionViewPrefab, transform);
            dialogueOptionViews.Add(newOptionView);
        }
    }
}
