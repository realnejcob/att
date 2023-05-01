using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NumpadButton : Button {
    private string Key { get; set; }

    protected override void Awake() {
        base.Awake();
        Key = GetComponentInChildren<TextMeshProUGUI>().text;
    }

    protected override void Start() {
        base.Start();
        onClick.AddListener(()=> {
            CombinationManager.Instance.AppendToCombination(Key);
            GetComponentInParent<Numpad>().AppendToDisplay(Key);
        });
    }
}
