using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private readonly Dictionary<(GUIStyle Source,int Size,bool Wrap),GUIStyle> _inventoryFitStyles = new();

        private static (Rect Items,Rect Stats,Rect Description) InventoryPanels(float width,float height,int familiarCount,bool familiarStatus)
        {
            const float gap=10f, descriptionHeight=112f;
            float totalWidth=Mathf.Min(900f,width-32f);
            float column=(totalWidth-gap)*.5f;
            float strip=76f;
            float mainHeight=Mathf.Max(120f,Mathf.Min(300f,height-32f-descriptionHeight-strip));
            float totalHeight=mainHeight+strip+descriptionHeight;
            float x=(width-totalWidth)*.5f,y=(height-totalHeight)*.5f;
            return (new Rect(x,y,column,mainHeight),new Rect(x+column+gap,y,column,Mathf.Min(mainHeight,222f)),
                new Rect(x,y+mainHeight+strip,totalWidth,descriptionHeight));
        }

        private readonly Dictionary<GUIStyle,GUIStyle> _inventoryNumberStyles=new();
        private GUIStyle InventoryNumberStyle(GUIStyle source)
        {
            if (!_inventoryNumberStyles.TryGetValue(source,out var result))
                _inventoryNumberStyles[source]=result=new GUIStyle(source){alignment=TextAnchor.MiddleRight};
            return result;
        }

        private GUIStyle InventoryEmphasisStyle(GUIStyle source) => InventoryFitStyle(source,(source.fontSize>0 ? source.fontSize : 13)+4,false);

        private GUIStyle InventoryFitStyle(GUIStyle source,int size,bool wrap)
        {
            var key=(source,size,wrap);
            if(!_inventoryFitStyles.TryGetValue(key,out var result))
                _inventoryFitStyles[key]=result=new GUIStyle(source){fontSize=size,wordWrap=wrap,clipping=TextClipping.Clip,alignment=wrap ? TextAnchor.UpperLeft : source.alignment};
            return result;
        }

        private GUIStyle InventoryFittedStyle(Rect rect,string value,GUIStyle source)
        {
            value ??= string.Empty;
            int size=source.fontSize>0 ? source.fontSize : 13;
            GUIStyle fitted;
            do
            {
                fitted=InventoryFitStyle(source,size,false);
                if(size<=7)break;
                var measured=fitted.CalcSize(new GUIContent(value));
                if(measured.x<=rect.width && measured.y<=rect.height)break;
                size--;
            }while(true);
            return fitted;
        }

        private void DrawInventoryFittedText(Rect rect,string value,GUIStyle source) =>
            DrawHudText(rect,value,InventoryFittedStyle(rect,value,source),dialogue:true);

        private bool DrawInventoryFittedButton(Rect rect,string value,bool selected=false)
        {
            var textRect=new Rect(rect.x+5f,rect.y,rect.width-10f,rect.height);
            return DrawInventoryButton(rect,value,selected,InventoryFittedStyle(textRect,value,_bodyStyle));
        }

        private void DrawInventoryParagraph(Rect rect,string value,int maxLines=4)
        {
            int size=_messageStyle.fontSize>0 ? _messageStyle.fontSize : 13;
            GUIStyle fitted;
            do
            {
                fitted=InventoryFitStyle(_messageStyle,size,true);
                float limit=Mathf.Min(rect.height,Mathf.Max(1f,fitted.lineHeight)*maxLines+fitted.padding.vertical);
                if(size<=8 || fitted.CalcHeight(new GUIContent(value),rect.width)<=limit+.1f)break;
                size--;
            }while(true);
            rect.height=Mathf.Min(rect.height,Mathf.Max(1f,fitted.lineHeight)*maxLines+fitted.padding.vertical);
            DrawHudText(rect,value,fitted,dialogue:true);
        }
    }
}
