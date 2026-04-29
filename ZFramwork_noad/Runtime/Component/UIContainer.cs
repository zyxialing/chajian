using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// UIContainer V1.0
/// 基于UNITY自动布局的容器
/// @Liukeming 2021-7-13
/// </summary>
public class UIContainer : MonoBehaviour
{
    public class Element
    {
        public GameObject go;//fetch from  pool
        public Rect rect;
        public object arg;//send by lua caller
        public bool dirty;//visibility changed
    }

    public GameObject parent;
    public GameObject prefab;

    private List<Element> elements = new List<Element>();
    private Action<Element> onCreate;
    private Action<Element> onRefresh;

    private void Awake()
    {
        if (parent == null)
            parent = gameObject;

        if (prefab == null)
        {
            if (parent.transform.childCount > 0)
                prefab = parent.transform.GetChild(0).gameObject;
            else
                Debug.LogError("UIContainer need target for prefab");
        }

        if (prefab != null)
            prefab.SetActive(false);
    }

    private void call(Action<Element> function, Element element)
    {
        function?.Invoke(element);
    }

    public void clear()
    {
        foreach (Element element in elements)
            Destroy(element.go);

        elements.Clear();
    }

    public void refresh()
    {
        if (onRefresh != null)
            foreach (Element element in elements)
                call(onRefresh, element);
    }

    public Element add(object arg = null)
    {
        if (prefab != null)
        {
            Element element = new Element();
            element.go = Instantiate(prefab);
            element.go.transform.SetParent(parent.transform, false);
            element.go.SetActive(true);
            element.go.name = "element";
            element.arg = arg;
            elements.Add(element);
            if (onCreate != null)
                call(onCreate, element);
            return element;
        }
        else
        {
            Debug.LogError("can not add element to UIContainer without prefab target");
            return null;
        }
    }

    public Element remove(int index)
    {
        Element element = elements[index];
        Destroy(element.go);
        elements.RemoveAt(index);
        return element;
    }

    public Element remove(Element element)
    {
        Destroy(element.go);
        elements.RemoveAt(elements.IndexOf(element));
        return element;
    }

    public List<Element> GetElements()
    {
        return elements;
    }

    public void bind(Action<Element> onCreate, Action<Element> onRefresh=null)
    {
        this.onCreate = onCreate;
        this.onRefresh = onRefresh;
    }
}