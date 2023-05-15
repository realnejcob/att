using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Yarn.Unity;

public class DialogueTextView : DialogueViewBase {
    [SerializeField] private TextMeshProUGUI textMesh;
    private Action advanceHandler;
    private LocalizedLine currentLocalizedLine;

    public override void RunLine(LocalizedLine dialogueLine, Action onDialogueLineFinished) {
        currentLocalizedLine = dialogueLine;
        textMesh.text = currentLocalizedLine.Text.Text;
        advanceHandler = requestInterrupt;
    }

    public override void DismissLine(Action onDismissalComplete) {
        onDismissalComplete();
    }

    public override void UserRequestedViewAdvancement() {
        advanceHandler?.Invoke();
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            UserRequestedViewAdvancement();
        }
    }
}
