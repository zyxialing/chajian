using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButton : Button
{
    public UnityEvent onPress = new UnityEvent();

    public UnityEvent onPressDown = new UnityEvent();

    public UnityEvent onPressUp = new UnityEvent();

    public ButtonClickedEvent onDisableClick = new ButtonClickedEvent();

    bool isPress = false;

    float pressTime = 0;

    public override void OnPointerClick(PointerEventData e)
    {
        if (this.interactable)
        {
            base.OnPointerClick(e);
        }
        else
        {
            onDisableClick.Invoke();
        }
    }

    public override void OnPointerDown(PointerEventData e)
    {
        base.OnPointerDown(e);

        isPress = true;

        if (this.interactable)
            onPressDown.Invoke();
    }

    public override void OnPointerUp(PointerEventData e)
    {
        base.OnPointerUp(e);

        isPress = false;

        pressTime = 0;

        if (this.interactable)
            onPressUp.Invoke();
    }

    public override void OnPointerExit(PointerEventData e)
    {
        base.OnPointerExit(e);

        isPress = false;

        pressTime = 0;

        if (this.interactable)
            onPressUp.Invoke();
    }

    void LateUpdate()
    {
        if (isPress && pressTime < 0.1f)
        {
            pressTime += Time.deltaTime;
            if (pressTime >= 0.1f)
            {
                pressTime = 0;

                if (this.interactable)
                    onPress.Invoke();
            }
        }
    }
}