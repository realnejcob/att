using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DialogueOptionView : MonoBehaviour {
    private TextMeshProUGUI textMesh;

    private Color initColor;
    [SerializeField] private Color hiddenColor = new Color(0, 0, 0.5f, 0);
    [SerializeField] private Color selectColor = new Color(1, 1, 1, 1);

    private void Awake() {
        textMesh = GetComponent<TextMeshProUGUI>();
        initColor = textMesh.color;
        InitializeText();
    }

    private void InitializeText() {
        LeanTween.cancel(gameObject);

        textMesh.color = hiddenColor;
        textMesh.text = string.Empty;
    }

    public void SetText(string newString) {
        textMesh.SetText(newString);
        textMesh.ForceMeshUpdate();
    }

    public void ShowAnimation(float delay) {
        StartCoroutine(Co());
        IEnumerator Co() {
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            FadeIn();
        }
    }

    private LTDescr LerpTextColor(Color startCol, Color targetCol, float speed) {
        return LeanTween.value(gameObject, 0, 1, speed).setOnUpdate((float t) => {
            var lerpedColor = Color.Lerp(startCol, targetCol, t);
            textMesh.color = lerpedColor;
        }).setEaseOutSine();
    }

    public void FadeOut() {
        LerpTextColor(initColor, hiddenColor, 0.75f).setOnComplete(InitializeText);
    }

    public void FadeIn() {
        LerpTextColor(hiddenColor, initColor, 0.75f);
    }

    public void Highlight() {
        StartCoroutine(Co());
        IEnumerator Co() {
            LerpTextColor(initColor, selectColor, 0.25f);
            yield return new WaitForSeconds(1f);
            LerpTextColor(selectColor, hiddenColor, 1f).setOnComplete(InitializeText);
        }
    }
}
