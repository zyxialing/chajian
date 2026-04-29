using EnhancedUI.EnhancedScroller;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class cellTest : EnhancedScrollerCellView
{

    public void UpdateDate(int index,int dainx)
    {
        UIUtils.SetSprite(GetComponent<Image>(), dainx.ToString());
    }
}
