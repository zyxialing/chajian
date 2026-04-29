using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIRadio : MonoBehaviour
{
    private UIContainer container;
    private Action<int> callBack;
    public int curSelected;
    private string[] names;
    public void init(int num, Action<int> callBack, string[] names = null,int defaultSelected = 0)
    {
        this.callBack = callBack;
        this.names = names;
        curSelected = defaultSelected;
        container = GetComponent<UIContainer>();
        container.bind(this.OnCreate, this.OnRefresh);
        container.clear();
        for (int i = 0; i < num; i++)
        {
            container.add(i);
        }
        container.refresh();
    }

    private void OnRefresh(UIContainer.Element obj)
    {
        GameObject normal = obj.go.transform.GetChild(0).gameObject;
        GameObject selected = obj.go.transform.GetChild(1).gameObject;
        int index = (int)obj.arg;
        normal.SetActive(index!=curSelected);
        selected.SetActive(index == curSelected);
    }

    private void OnCreate(UIContainer.Element obj)
    {
        int index = (int)obj.arg;
        obj.go.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (curSelected == index)
            {
                return;
            }
            curSelected = index;
            container.refresh();
            this.callBack?.Invoke(curSelected);
        });
        var text1 = obj.go.transform.GetChild(0).GetChild(0).GetComponent<Text>();
        var text2 = obj.go.transform.GetChild(1).GetChild(0).GetComponent<Text>();
        if (names != null)
        {
            text1.gameObject.SetActive(true);
            text2.gameObject.SetActive(true);
            text1.text = names[index];
            text2.text = names[index];
        }
        else
        {
            text1.gameObject.SetActive(false);
            text2.gameObject.SetActive(false);
        }

    }


}
