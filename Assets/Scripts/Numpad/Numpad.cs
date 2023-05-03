using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Numpad : MonoBehaviour {
    public GameObject mask;
    [SerializeField] private TextMeshProUGUI displayText;

    public void AppendToDisplay(string toAppend) {
        var originalText = displayText.text;
        var newText = originalText + toAppend;
        displayText.SetText(newText);
        displayText.ForceMeshUpdate();

        if (displayText.isTextOverflowing) {
            newText = newText.Substring(1);
            displayText.SetText(newText);
            displayText.ForceMeshUpdate();
        }
    }
}
