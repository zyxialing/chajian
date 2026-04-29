using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using UnityEngine.Events;

public class BasePanel : MonoBehaviour {


    public static BasePanel basePanel;

    public string className;
    public string adressPath;
    //
    public GameObject panel;
    //层级
    public PanelLayer panelLayer;
    //
    public Dictionary<string, Transform> uiTransforms;
    //面板参数
    public object[] args;

    //面板的生命周期
    #region 
    //若面板退出时，不销毁则该初始化，只会运行一次
    public virtual void Init(params object[] args)
    {
        this.args = args;
        basePanel = this;
        uiTransforms = new Dictionary<string, Transform>();
    }

    public virtual void AutoInit()
    {

    }

    public void CoroInit()
    {
        StartCoroutine(InitPanel());
    }

    private IEnumerator InitPanel()
    {
        OnShowing();
        yield return null;
        OnShowed();
        yield return null;
        OnOpen();
        yield return null;
    }

    /// <summary>
    /// 组件初始化之后，但是面板销毁之前只初始化一次
    /// </summary>
    public virtual void OnShowing()
    {
      
    }
    /// <summary>
    /// 组件初始化之后，但是面板销毁之前只初始化一次
    /// </summary>
    public virtual void OnShowed()
    {
      
    }
    /// <summary>
    /// 每次打开都会调用
    /// </summary>
    public virtual void OnOpen()
    {

    }
    /// <summary>
    /// 每次关闭都会顶用
    /// </summary>
    public virtual void OnHide()
    {

    }
    /// <summary>
    /// 销毁后调用
    /// </summary>
    public virtual void OnClosing()
    {

    }

    #endregion

    protected virtual void Close()
    {
        UIManager.Instance.ClosePanel(panelLayer);
    }


    protected void AddClick(Button button,UnityAction callBack)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callBack);
    }

}
