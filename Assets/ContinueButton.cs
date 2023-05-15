using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContinueButton : MonoBehaviour {
    [SerializeField] private float animationScale = 0.75f;
    [SerializeField] private float lerpTime = 0.75f;
    private LTDescr tween;

    public void Click() {
        if (tween != null)
            LeanTween.cancel(gameObject);

        transform.localScale = Vector2.one * animationScale;
        tween = LeanTween.scale(gameObject, Vector2.one, lerpTime)
            .setEaseOutQuint();
    }
}
