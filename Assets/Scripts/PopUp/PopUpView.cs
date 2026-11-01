using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;


/// <summary>
/// Vista del PopUp
/// Este script se encuentra enlazado directamente 
/// al objeto del PopUp. Cada variable hace referencia
/// a cada componente.
/// </summary>
public class PopUpView : MonoBehaviour
{
    // ------------------------------
    //          Variables
    // ------------------------------
    // Las que se necesitan crear desde otros script
    public Text Title;
    public Text Message;
    public Image Image;
    protected Dictionary<string, PopUp.PopUpAction> Actions = new Dictionary<string, PopUp.PopUpAction>();

    // Las que se necesitan internamente 
    public RectTransform WindowPopUp; // Para controlar el tamaño del panel en función del message
    public RectTransform CanvasInfo;

    private GameObject objectPopUp;
    private PopUpView pop;

    // Control de los botonoes
    // Cuando se activan varios botones se necesita reposicionar 
    // el canvas en el centro para que aparezcan bien colocados
    public RectTransform canvasButton;

    // Para desactivar el icono
    public RectTransform canvasImage;

    public Animator animator;


    /// <summary>
    /// Inicializacion
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="image"></param>
    public void CreatePopUp(string title, string message, Sprite image, Dictionary<string, PopUp.PopUpAction> actions)
    {
        // 1. Instanciamos el objeto
        objectPopUp = Instantiate(Resources.Load("PopUp/PopUp") as GameObject);

        objectPopUp.SetActive(false);

        // 2. Llamamos al controlador
        pop = objectPopUp.GetComponent<PopUpView>();

        // 3. Inicializamos sus componentes
        SetTitle(title);
        SetMessage(message);
        SetIcon(image);
        SetButton(actions);

        objectPopUp.SetActive(true);
        AnimationShow();
    }


    /// <summary>
    /// Crea el titulo del PopUp
    /// </summary>
    public void SetTitle(string title)
    {
        if (title == "" || title == null)
        {
            pop.Title.text = "";
        }
        else
        {
            pop.Title.text = title;
        }
    }


    /// <summary>
    /// Crea el mensaje del PopUp
    /// </summary>
    public void SetMessage(string message)
    {
        if (message == "")
        {
            pop.Message.text = " ";
        }
        else
        {
            pop.Message.text = message;
        }
    }


    /// <summary>
    /// Crea la imagen del PopUp
    /// </summary>
    public void SetIcon(Sprite image)
    {
        if (image == null)
        {
            pop.Image.gameObject.SetActive(false);
            pop.canvasImage.gameObject.SetActive(false);
        }
        else
        {
            pop.Image.sprite = image; 
        }
    }

    /// <summary>
    /// Crea los botones del PopUp
    /// </summary>
    public void SetButton(Dictionary<string, PopUp.PopUpAction> actions)
    {
        pop.Actions = actions;
        //CreateButtons();
        CreateButtonsPanel();
    }



    /// <summary>
    /// Crea tantos botones como acciones
    /// existan. 
    /// </summary>
    public void CreateButtons()
    {
        //if (isActive) { return; }

        int index = 0;
        Button[] listButtons = objectPopUp.GetComponentsInChildren<Button>();

        //this.dismiss = dismissAction;
        foreach (KeyValuePair<string, PopUp.PopUpAction> actionPair in pop.Actions)
        {
            listButtons[index].GetComponentInChildren<Text>().text = actionPair.Key;

            PopUp.PopUpAction a = actionPair.Value.Clone() as PopUp.PopUpAction;

            listButtons[index].onClick.AddListener(() =>
            {
                a.Invoke();
                for (int i = 0; i < listButtons.Length; i++)
                {
                    listButtons[i].onClick.RemoveAllListeners();
                }
                // Hide();
            });
            listButtons[index].gameObject.SetActive(true);

            index++;
        }

        // index >= 2 ... Significa que hay que reposicionar el canvas de los botones
        if (index >= 2) 
        {
            canvasButton.localPosition = new Vector2(225, 0);
        }

    }


    /// <summary>
    /// 
    /// </summary>
    public void CreateButtonsPanel() 
    {
        int index = 0;
        Button[] listButtons = objectPopUp.GetComponentsInChildren<Button>();

        //this.dismiss = dismissAction;
        foreach (KeyValuePair<string, PopUp.PopUpAction> actionPair in pop.Actions)
        {
            listButtons[index].GetComponentInChildren<Text>().text = actionPair.Key;

            PopUp.PopUpAction a = actionPair.Value.Clone() as PopUp.PopUpAction;

            listButtons[index].onClick.AddListener(() =>
            {
                a.Invoke();
                for (int i = 0; i < listButtons.Length; i++)
                {
                    listButtons[i].onClick.RemoveAllListeners();
                }
                // Hide();
            });
            listButtons[index].gameObject.SetActive(true);

            index++;
        }

        int buttonsActive = index;

        while (buttonsActive != listButtons.Length) 
        {
            listButtons[buttonsActive].gameObject.SetActive(false);
            buttonsActive++;
        }
    }


    public void AnimationShow()
    {
        pop.animator.SetBool("PopUP", true);
    }

    public void AnimationClose()
    {
        DestroyPopUp();
    }


    public void DestroyPopUp()
    {
        Destroy(objectPopUp);
    }

    public void NotShowIcon() 
    {
        canvasImage.gameObject.SetActive(false);
    }

}
