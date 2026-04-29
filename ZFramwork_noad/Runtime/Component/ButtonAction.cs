using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonAction : Button
{

    public UnityEvent onPressDown = new UnityEvent();

    public UnityEvent onTimeClick = new UnityEvent();

    public UnityEvent onPressUp = new UnityEvent();

    public ButtonClickedEvent onDisableClick = new ButtonClickedEvent();

    bool isPress = false;

    private float longTimeClick = 0.3f;

    private float pressTime = 0;


    public override void OnPointerDown(PointerEventData e)
    {
        base.OnPointerDown(e);

        isPress = true;
        pressTime = 0;
        if (this.interactable)
            onPressDown.Invoke();
    }

    public override void OnPointerUp(PointerEventData e)
    {

        if (isPress)
        {
            base.OnPointerUp(e);
            isPress = false;
            if (this.interactable)
                onPressUp.Invoke();
        }

    }

    void LateUpdate()
    {
        if (isPress)
        {
            if(pressTime < longTimeClick)
            {
                pressTime += Time.deltaTime;
            }
            else
            {
                if (this.interactable)
                    onTimeClick.Invoke();

                isPress = false;
            }
        }
    }
}