using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class CustomLineView : LineView {

    [SerializeField] private CustomDialogueRunner dialogueRunner;

    public override void UserRequestedViewAdvancement() {
        base.UserRequestedViewAdvancement();
    }
}
