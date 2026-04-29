using EnhancedUI.EnhancedScroller;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExampleCell : EnhancedScrollerCellView
{
    private Text _text;
    private Button _button;
    public void UpdateDate(string name)
    {
        if (_text == null)
        {
            _text =transform.GetChild(0).GetComponent<Text>();
        }
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => {
            ResLoader.Instance.GetScene(name, null);
        });
        _text.text = name;
    }

    
}
