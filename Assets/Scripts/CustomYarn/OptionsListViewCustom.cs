using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Yarn.Unity;

public class OptionsListViewCustom : OptionsListView {
    public override void RunOptions(DialogueOption[] dialogueOptions, Action<int> onOptionSelected) {
        base.RunOptions(dialogueOptions, onOptionSelected);

        foreach (var option in dialogueOptions) {
            if (option.Line.Metadata != null) {
                print($"Has key: { option.Line.Metadata[0]}");
            }
        }
    }
}