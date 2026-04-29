using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.UI;
using EnhancedUI.EnhancedScroller;

public class UIRadioEditor
{
    [MenuItem("GameObject/UI/ZHelper/Radio/Vertical",false,98)]
    static void CreateVerUIList()
    {
        GameObject selectObj = Selection.activeGameObject;
        GameObject scroll = new GameObject("scroll¡¾name is script¡¿", new Type[] {typeof(RectTransform), typeof(CanvasRenderer) , typeof(Image) , typeof(ScrollRect), typeof(EnhancedScroller), typeof(UINodeScoller) });
        GameObject content = new GameObject("content", new Type[] { typeof(RectTransform)});
        GameObject cell = new GameObject("cell¡¾name is script¡¿", new Type[] { typeof(RectTransform)});
        GameObject scrollBar = new GameObject("scrollBar", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar) });
        GameObject slidingArea = new GameObject("slidingArea", new Type[] { typeof(RectTransform) });
        GameObject handle = new GameObject("handle", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) });
        GameObject prefabs = new GameObject("prefabs", new Type[] { typeof(RectTransform) });
        
        scroll.transform.SetParent(selectObj.transform);
        prefabs.transform.SetParent(scroll.transform);
        content.transform.SetParent(scroll.transform);
        cell.transform.SetParent(content.transform);
        scrollBar.transform.SetParent(scroll.transform);
        slidingArea.transform.SetParent(scrollBar.transform);
        handle.transform.SetParent(slidingArea.transform);

        prefabs.SetActive(false);
        prefabs.transform.SetAsLastSibling();

        cell.AddComponent<UINodeScollerCell>();

        scroll.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
        scroll.GetComponent<ScrollRect>().vertical = true;
        scroll.GetComponent<ScrollRect>().horizontal = false;
        scroll.GetComponent<ScrollRect>().content = content.GetComponent<RectTransform>();
        scroll.GetComponent<ScrollRect>().verticalScrollbar = scrollBar.GetComponent<Scrollbar>();
        scroll.GetComponent<EnhancedScroller>().scrollDirection = EnhancedScroller.ScrollDirectionEnum.Vertical;
        scroll.GetComponent<UINodeScoller>().cellView = cell;
        scrollBar.GetComponent<Scrollbar>().handleRect = handle.GetComponent<RectTransform>();
        scrollBar.GetComponent<Scrollbar>().SetDirection(Scrollbar.Direction.BottomToTop,true);
        scrollBar.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        RectTransformUtils.SetStretch(content);
        RectTransformUtils.SetStretchRight(scrollBar);
        RectTransformUtils.SetStretch(slidingArea);
        RectTransformUtils.SetStretch(handle);
        RectTransformUtils.SetStretch(prefabs);

        scrollBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(20, 0);
        //MPScrollerV1
        //MPCellV1
    }

    [MenuItem("GameObject/UI/ZHelper/Radio/Horizontal", false, 98)]
    static void CreateHorUIList()
    {
        GameObject selectObj = Selection.activeGameObject;
        GameObject horRadio = new GameObject("horRadio", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),typeof(HorizontalLayoutGroup),typeof(UIContainer),typeof(UIRadio)});
        GameObject radioBtn = new GameObject("btn", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),typeof(Button)});
        GameObject normal = new GameObject("normal", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) });
        GameObject selected = new GameObject("selected", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) });
        GameObject nameObj1 = new GameObject("name", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Text) });
        GameObject nameObj2 = new GameObject("name", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Text) });
        var hlg = horRadio.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 10;

        horRadio.transform.SetParent(selectObj.transform);
        radioBtn.transform.SetParent(horRadio.transform);
        normal.transform.SetParent(radioBtn.transform);
        selected.transform.SetParent(radioBtn.transform);
        nameObj1.transform.SetParent(normal.transform);
        nameObj2.transform.SetParent(selected.transform);
        radioBtn.GetComponent<Image>().color = new Color(1, 1, 1, 0);
        radioBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 100);
        normal.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 100);
        selected.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 100);
        nameObj1.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 100);
        nameObj2.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 100);
        normal.GetComponent<Image>().color = Color.grey;
        selected.GetComponent<Image>().color = Color.green;
        horRadio.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 100);
        horRadio.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);


        horRadio.GetComponent<UIContainer>().parent = horRadio;
        horRadio.GetComponent<UIContainer>().prefab = radioBtn;

        var text1 = nameObj1.GetComponent<Text>();
        var text2 = nameObj2.GetComponent<Text>();
        text1.text = "name";
        text1.color = Color.white;
        text1.alignment = TextAnchor.MiddleCenter;
        text1.fontSize = 42;
        text1.raycastTarget = false;
        text2.color = Color.white;
        text2.alignment = TextAnchor.MiddleCenter;
        text2.fontSize = 42;
        text2.raycastTarget = false;
        text2.text = "name";


    }
}
