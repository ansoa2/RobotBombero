using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AddActionsController : MonoBehaviour
{
    public GameObject content;
    public GameObject action_prefab;

    public void Start()
    {
        if (content.transform.childCount == 0)
        {
            AddAccion();
        }
    }

    public void AddAccion()
    {
        if(content.transform.childCount == 0)
        {
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(content.GetComponent<RectTransform>().rect.width, action_prefab.GetComponent<RectTransform>().rect.height);
        }

        GameObject action = Instantiate(action_prefab) as GameObject;
        action.GetComponentInChildren<Text>().text = "Action " + content.transform.childCount;
        action.transform.parent = content.transform;
        content.GetComponent<RectTransform>().sizeDelta = new Vector2(content.GetComponent<RectTransform>().rect.width, content.GetComponent<RectTransform>().rect.height + action_prefab.GetComponent<RectTransform>().rect.height);
        
    }
}
