using UnityEngine;
using UnityEngine.EventSystems;
public class UIEventTrigger : EventTrigger
    {
        public event PointerEventDelegate onPointerEnter;
        public event PointerEventDelegate onPointerExit;
        public event PointerEventDelegate onPointerDown;
        public event PointerEventDelegate onPointerUp;
        public event PointerEventDelegate onPointerClick;
        public event PointerEventDelegate onInitializePotentialDrag;
        public event PointerEventDelegate onBeginDrag;
        public event PointerEventDelegate onDrag;
        public event PointerEventDelegate onEndDrag;
        public event PointerEventDelegate onDrop;
        public event PointerEventDelegate onScroll;
        public delegate void PointerEventDelegate(GameObject obj, PointerEventData eventData);

        public static UIEventTrigger RegistUIEvent(GameObject obj)
        {
            UIEventTrigger listener = obj.GetComponent<UIEventTrigger>();
            if (listener == null) listener = obj.AddComponent<UIEventTrigger>();
            return listener;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            if (onPointerEnter != null) onPointerEnter(gameObject, eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            if (onPointerExit != null) onPointerExit(gameObject, eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (onPointerDown != null) onPointerDown(gameObject, eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (onPointerUp != null) onPointerUp(gameObject, eventData);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (onPointerClick != null) onPointerClick(gameObject, eventData);
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (onInitializePotentialDrag != null) onInitializePotentialDrag(gameObject, eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (onBeginDrag != null) onBeginDrag(gameObject, eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (onDrag != null) onDrag(gameObject, eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (onEndDrag != null) onEndDrag(gameObject, eventData);
        }

        public override void OnDrop(PointerEventData eventData)
        {
            if (onDrop != null) onDrop(gameObject, eventData);
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (onScroll != null) onScroll(gameObject, eventData);
        }
    }
