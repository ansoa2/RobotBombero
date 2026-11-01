using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeleteActionController : MonoBehaviour
{
    public GameObject content;
    public GameObject action_prefab;

    public void DeleteAccion()
    {
        if (content.transform.childCount == 0)
        {
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(content.GetComponent<RectTransform>().rect.width, action_prefab.GetComponent<RectTransform>().rect.height);
        }
        else if (content.transform.childCount > 1)
        {
            Destroy(content.transform.GetChild(content.transform.childCount - 1).gameObject);
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(content.GetComponent<RectTransform>().rect.width, content.GetComponent<RectTransform>().rect.height - action_prefab.GetComponent<RectTransform>().rect.height);
        }
    }
}