using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RuleModel
{
    public int id_rule;
    public string name;
    public string battery_state;
    public string sensor_temp;
    public string sensor_light;
    public string sensor_prox;
    public string sensor_cell;
    public List<string> action;


    public List<string> actions;
}
