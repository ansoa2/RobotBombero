using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Clase a la que se accede desde otros Scripts
/// 
///  PopUp popup = new PopUp("Ejemplo del PopUp", "Esto es unicamente una prueba");
///  popup.AddAction("OK", () => { popup.Close(); });
///  popup.AddAction("OK 1", () => { popup.Close(); });
///  popup.AddAction("OK 2", () => { popup.Close(); });
///  popup.Show();   NO OLVIDAR!!!
///
/// PopUp popup = new PopUp("Titulo", "Contenido");
/// popup.AddAction("OK", () => { popup.Close(); });
/// popup.Show(); 
/// </summary>
public class PopUp 
{
    // --------------------------------------------
    //                  Variables 
    // image: Imagen que se muestra en el panel
    // title: Título de la ventana
    // message: Texto informativo
    // PopUpAction: Delegado de la acción
    // 
    // ---------------------------------------------

    protected Sprite image; 
    protected string title = string.Empty;
    protected string message = string.Empty;
    public delegate void PopUpAction();
    protected Dictionary<string, PopUpAction> actions = new Dictionary<string, PopUpAction>();


    //
    protected PopUpView objectPopUp;

    /// <summary>
    /// Constructor con imagen
    /// </summary>
    /// <param name="image"> Imagen que se muestra en el panel </param>
    /// <param name="title"> Título del panel </param>
    /// <param name="message"> Mensaje del panel </param>
    public PopUp(Sprite image, string title, string message) 
    {
        actions = new Dictionary<string, PopUpAction>();

        this.image = image;
        this.title = title;
        this.message = message;

    }


    /// <summary>
    /// Constructor con imagen por defecto
    /// (logo)
    /// </summary>
    /// <param name="title"> Título del panel </param>
    /// <param name="message"> Mensaje del panel </param>
    public PopUp(string title, string message)
    {
        actions = new Dictionary<string, PopUpAction>();

        //this.image = Resources.Load("/logo.png");//Logo
        this.title = title;
        this.message = message;
        this.image = null;
    }


    /// <summary>
    /// Añade un botón con una acción 
    /// asociada a la ventana
    /// </summary>
    /// <param name="titleAction"> Título del botón </param>
    /// <param name="callback"> Acción al pulsar botón </param>
    public void AddAction(string titleAction, PopUpAction callback) 
    {
        // Si la accion que se manda es () ... que se cierre el
        // PopUp
        actions.Add(titleAction, callback);
        // objectPopUp.CreateButtonsPanel();
    }


    /// <summary>
    /// Funcion que crea y visualiza el objeto del PopUp
    /// </summary>
    public void Show() 
    {
        // Se crea el objeto
        objectPopUp = new PopUpView();
        objectPopUp.CreatePopUp(this.title, this.message, this.image, this.actions);

    }

    /// <summary>
    /// Destruye el objeto en la escena
    /// </summary>
    public void Close()
    {

        // Se crea el objeto
        objectPopUp.AnimationClose();

    }


    public void NotShowIcon() 
    {
        objectPopUp.NotShowIcon();
    }

}
