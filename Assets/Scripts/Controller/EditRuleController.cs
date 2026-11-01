using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class EditRuleController : MonoBehaviour
{
    public int id;
    public Text rule_name;
    public Dropdown battery;
    public Dropdown sensor_temp;
    public Dropdown sensor_light;
    public Dropdown sensor_prox;
    public Dropdown sensor_cell;
    public AddActionsController addAction;
    public DeleteActionController deleteAction;
    //public Dropdown action;

    public List<string> action_list;

    private void Awake()
    {
        action_list = new List<string>();
    }

    public void DeleteRule()
    {
        PopUp popUp = new PopUp("Eliminar Regla", "¿Seguro que deséa eliminar esta regla? ");
        popUp.AddAction("Eliminar", () => { Eliminar(); popUp.Close(); });
        popUp.AddAction("Cancelar", () => {  popUp.Close(); });
        popUp.Show();
    }

    public void Eliminar()
    {
        Destroy(this.gameObject);
    }

    public void AddAction(Dropdown action)
    {
        action_list.Add(action.options[action.value].text);
    }

    public void DeleteAction(Dropdown action)
    {
        action_list.RemoveAt(action_list.Count-1);
    }
    public void EditAction(Dropdown action, int pos)
    {
        action_list[pos] = action.options[action.value].text;
    }


}
