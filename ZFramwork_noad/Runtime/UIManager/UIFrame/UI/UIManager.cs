using UnityEngine.Rendering.Universal;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum PanelLayer
{
    Panel,
    PanelUp,
    Pop,
    Tips,
    Overlay
}

public struct PanelData
{
    public BasePanel basePanel;
    public string name;

    public PanelData(string name,BasePanel basePanel)
    {
        this.basePanel = basePanel;
        this.name = name;
    }
}

/// <summary>
/// 面板管理器只有打开和关闭的功能！！！！！！！
/// </summary>
public class UIManager : Singleton<UIManager>
{
    //画布
    private Canvas canvas;
    public Camera uiCamera;
    private Dictionary<PanelLayer, Stack<BasePanel>> _panelStacks;
    private Dictionary<PanelLayer, Transform> layerDict;
    public void Init()
    {
        ////not need write anyting
    }
    public Camera camera_scene;

    void CreateCameras(Transform cameraRoot)
    {
        var cameraObj = new GameObject("sceneCamera");
        cameraObj.transform.SetParent(cameraRoot);
        camera_scene = cameraObj.AddComponent<Camera>();
        camera_scene.clearFlags = CameraClearFlags.SolidColor;
        camera_scene.cullingMask = (1 << 6) + (1 << 7) + (1 << 0);
        camera_scene.orthographic = true;
        camera_scene.farClipPlane = 2000;
        camera_scene.orthographicSize = ZDefine.sceneCameraSize;
        camera_scene.depth = 0;


    }

    public void SetUISceneCameraType(bool uiScene)
    {
        if (uiScene)
        {
            uiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
            camera_scene.gameObject.SetActive(false);
        }
        else
        {
            uiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            camera_scene.gameObject.SetActive(true);
            var cameraData = camera_scene.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.cameraStack.Add(uiCamera);
        }

    }


    private UIManager()
    {
        canvas = new GameObject("UICanvas", new Type[] { typeof(Canvas), typeof(CanvasScaler) }).GetComponent<Canvas>();
        GameObject.DontDestroyOnLoad(canvas.gameObject);
        //uiCamera = GameObject.Find("UICamera").GetComponent<Camera>();
        uiCamera = CameraUtils.CreateCamer("UICamera");
        Transform eventTrans = new GameObject("EventSystem", new Type[] { typeof(EventSystem),typeof(StandaloneInputModule), typeof(BaseInput) }).transform;
        eventTrans.tag = "EventSystem";
        TransformUtils.TransformWorldNormalize(canvas.gameObject);
        TransformUtils.TransformLocalNormalize(uiCamera.gameObject, canvas.gameObject);
        TransformUtils.TransformLocalNormalize(eventTrans.gameObject, canvas.gameObject);

        CameraUtils.SetUICameraParma(uiCamera);
        var cameraRootObj = new GameObject("CameraRoot");
        GameObject.DontDestroyOnLoad(cameraRootObj);
        CreateCameras(cameraRootObj.transform);
        cameraRootObj.transform.position = new Vector3(0, 0, -1000);
        InitUICanvas();

        layerDict = new Dictionary<PanelLayer, Transform>();
        _panelStacks = new Dictionary<PanelLayer, Stack<BasePanel>>();
        int sort = 0;
        foreach (PanelLayer pl in Enum.GetValues(typeof(PanelLayer)))
        {
            string name = pl.ToString();
            GameObject obj = new GameObject(name, new Type[] { typeof(Canvas),typeof(GraphicRaycaster) });
            TransformUtils.TransformLocalNormalize(obj, canvas.gameObject);
            RectTransformUtils.SetStretch(obj);
            LayerUtils.SetUILayer(obj);
            layerDict.Add(pl, obj.transform);
            obj.GetComponent<Canvas>().overrideSorting = true;
            obj.GetComponent<Canvas>().sortingOrder = sort;
            sort++;
        }
    }

    public void OpenPanel<T>(params object[] args) where T : BasePanel
    {
        string className = typeof(T).FullName;

        BasePanel basePanel = canvas.gameObject.AddComponent(typeof(T)) as BasePanel;
        basePanel.className = className;
        basePanel.Init(args);
        if (_panelStacks.ContainsKey(basePanel.panelLayer))
        {
            if (_panelStacks[basePanel.panelLayer].Count > 0)
            {
                BasePanel lastPanel = _panelStacks[basePanel.panelLayer].Peek();
                lastPanel.OnHide();
                lastPanel.panel.SetActive(false);
            }
            _panelStacks[basePanel.panelLayer].Push(basePanel);
        }
        else
        {
            Stack<BasePanel> panels = new Stack<BasePanel>();
            panels.Push(basePanel);
            _panelStacks.Add(basePanel.panelLayer, panels);
        }

        string adressPath = basePanel.adressPath;
        GameObject panelObj = PrefabUtils.Instance(adressPath,true);
        InitUIPrefab(basePanel, panelObj);
    }

    public bool IsContainUI(PanelLayer panelLayer,string className)
    {
        if (_panelStacks.ContainsKey(panelLayer))
        {
            var stackUI = _panelStacks[panelLayer];
            foreach (var item in stackUI)
            {
                if (item.className == className)
                {
                    return true;
                }
            }
 
        }
        return false;
    }
    public string ClosePanel(PanelLayer panelLayer)
    {
        if (_panelStacks.ContainsKey(panelLayer))
        {
            if (_panelStacks[panelLayer].Count > 0)
            {
                BasePanel panel = _panelStacks[panelLayer].Pop();
                panel.OnHide();
                panel.OnClosing();
                panel.panel.SetActive(false);
                if (_panelStacks[panelLayer].Count > 0)
                {
                    BasePanel basePanel =  _panelStacks[panelLayer].Peek();
                    basePanel.panel.SetActive(true);

                    basePanel.OnOpen();
                    GameObject.Destroy(panel.panel);
                    GameObject.Destroy(panel);
                    return basePanel.className;
                }
                else
                {
                    GameObject.Destroy(panel.panel);
                    GameObject.Destroy(panel);
                    return "";
                }
 
            }
        }
        ZLogUtil.LogError("没有面板关闭了");
        return "";
    }

    public void CloseAll()
    {
        foreach (PanelLayer item in Enum.GetValues(typeof(PanelLayer)))
        {
            if (_panelStacks.ContainsKey(item))
            {
                int count = _panelStacks[item].Count;
                for (int i = 0; i < count; i++)
                {
                    BasePanel panel = _panelStacks[item].Pop();
                    panel.OnHide();
                    panel.OnClosing();
                    panel.panel.SetActive(false);
                    GameObject.Destroy(panel.panel);
                    GameObject.Destroy(panel);
                }
            }
        }
    }

    public int GetCount(PanelLayer panelLayer)
    {
        if (_panelStacks.ContainsKey(panelLayer))
        {
            return _panelStacks[panelLayer].Count;
        }
        return 0;
    }

    private void InitUIPrefab(BasePanel basePanel, GameObject obj)
    {
        basePanel.panel = obj;
        Transform panelTrans = basePanel.panel.transform;
        PanelLayer layer = basePanel.panelLayer;
        Transform parent = layerDict[layer];
        panelTrans.SetParent(parent, false);
        basePanel.panel.SetActive(true);
        basePanel.AutoInit();
        basePanel.CoroInit();

    }

    private void InitUICanvas()
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
        var scaler = canvas.gameObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =ZDefine.Portrait ? new Vector2(ZDefine.StandardScreen.y, ZDefine.StandardScreen.x) : ZDefine.StandardScreen; ;
        scaler.matchWidthOrHeight = ZDefine.Portrait ? 1 : 0;
    }

    public void SetMatchWidthOrHeight(float value)
    {
        var scaler = canvas.gameObject.GetComponent<CanvasScaler>();
        scaler.matchWidthOrHeight = value;
    }

    public void AutoMatchWidthOrHeightByPortrait()
    {
        var scaler = canvas.gameObject.GetComponent<CanvasScaler>();
        var hBili = ZDefine.StandardScreen.x / ZDefine.StandardScreen.y;
        var curBili = Screen.height / Screen.width;
        if (curBili >= hBili)
        {
            scaler.matchWidthOrHeight = 0;
        }
        else
        {
            scaler.matchWidthOrHeight = 1;
        }
        
    }

}

