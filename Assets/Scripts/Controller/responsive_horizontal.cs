using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class responsive_horizontal: MonoBehaviour
{
    public float widthDesign;
    public float heightDesign;
    public GameObject generalCanvas;
    public GameObject containerCanvas;
    public GameObject header;
    public bool hasMiddle;
    public GameObject middle;
    public GameObject scrolled;
    public bool hasFooter;
    public GameObject footer;
    // Start is called before the first frame update

    void Start()
    {
        Recise();
    }

    private void Recise()
    {
        float canvasHeight = generalCanvas.GetComponent<RectTransform>().rect.height;
        
        float headerHeight = header.GetComponent<RectTransform>().rect.height;
        float scrolledHeight = scrolled.GetComponent<RectTransform>().rect.height;

        float scaleHorizontal = generalCanvas.GetComponent<RectTransform>().rect.width / widthDesign;
        float scaleVertical = heightDesign / generalCanvas.GetComponent<RectTransform>().rect.height;

        if(scaleHorizontal > 1)
        {
            containerCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(containerCanvas.GetComponent<RectTransform>().rect.width, canvasHeight - (canvasHeight * (1 / scaleHorizontal)));
        }
        else
        {
            containerCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(containerCanvas.GetComponent<RectTransform>().rect.width, (canvasHeight * (1 / scaleHorizontal)));
        }
        
        this.gameObject.transform.localScale = new Vector3(scaleHorizontal, scaleHorizontal, 1);

        if (hasMiddle == false && hasFooter == false)
        {
            scrolledHeight = ((canvasHeight * (1 / scaleHorizontal)) - headerHeight);
            RectTransform rt = scrolled.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(widthDesign, scrolledHeight);
        }

        if (hasMiddle == true && hasFooter == false)
        {
            float middlerHeight = middle.GetComponent<RectTransform>().rect.height;
            
            scrolledHeight = ((canvasHeight * (1/ scaleHorizontal)) - headerHeight - middlerHeight);
            RectTransform rt = scrolled.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(widthDesign, scrolledHeight);
        }

        if (hasMiddle == false && hasFooter == true)
        {
            float footerHeight = footer.GetComponent<RectTransform>().rect.height;

            scrolledHeight = ((canvasHeight * (1 / scaleHorizontal)) - headerHeight - footerHeight);
            RectTransform rt = scrolled.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(widthDesign, scrolledHeight);

            footer.transform.localPosition = new Vector3(0, -(scrolledHeight + headerHeight), 0);
        }

        if (hasMiddle == true && hasFooter == true)
        {
            float middlerHeight = middle.GetComponent<RectTransform>().rect.height;
            float footerHeight = footer.GetComponent<RectTransform>().rect.height;

            scrolledHeight = ((canvasHeight * (1 / scaleHorizontal)) - headerHeight - middlerHeight - footerHeight);
            RectTransform rt = scrolled.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(widthDesign, scrolledHeight);

            footer.transform.localPosition = new Vector3(0, -(scrolledHeight + headerHeight + middlerHeight), 0);
        }
    }
}
