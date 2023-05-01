using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

public class YarnCommandManager : MonoBehaviour {

    public DialogueRunner dialogueRunner;
    public void GetOptions() {
        var nodeName = dialogueRunner.CurrentNodeName;
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            GetOptions();
        }
    }

}
