using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.UI;
using EnhancedUI.EnhancedScroller;

public class UIListEditor
{
    [MenuItem("GameObject/UI/ZHelper/UIList/Vertical", false,99)]
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

    [MenuItem("GameObject/UI/ZHelper/UIList/Horizontal", false, 99)]
    static void CreateHorUIList()
    {
        GameObject selectObj = Selection.activeGameObject;
        GameObject scroll = new GameObject("scroll¡¾name is script¡¿", new Type[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(EnhancedScroller), typeof(UINodeScoller) });
        GameObject content = new GameObject("content", new Type[] { typeof(RectTransform) });
        GameObject cell = new GameObject("cell¡¾name is script¡¿", new Type[] { typeof(RectTransform) });
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
        scroll.GetComponent<ScrollRect>().vertical = false;
        scroll.GetComponent<ScrollRect>().horizontal = true;
        scroll.GetComponent<ScrollRect>().content = content.GetComponent<RectTransform>();
        scroll.GetComponent<ScrollRect>().horizontalScrollbar = scrollBar.GetComponent<Scrollbar>();
        scroll.GetComponent<EnhancedScroller>().scrollDirection = EnhancedScroller.ScrollDirectionEnum.Horizontal;
        scroll.GetComponent<UINodeScoller>().cellView = cell;
        scrollBar.GetComponent<Scrollbar>().handleRect = handle.GetComponent<RectTransform>();
        scrollBar.GetComponent<Scrollbar>().SetDirection(Scrollbar.Direction.RightToLeft, true);
        scrollBar.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        RectTransformUtils.SetStretch(content);
        RectTransformUtils.SetStretchButtom(scrollBar);
        RectTransformUtils.SetStretch(slidingArea);
        RectTransformUtils.SetStretch(handle);
        RectTransformUtils.SetStretch(prefabs);


        //MPScrollerV1
        //MPCellV1
    }
}
