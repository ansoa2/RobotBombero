using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AddRuleController : MonoBehaviour
{
    public GameObject main_View;
    RuleModel rule;
    public InputField ruleName;
    public GameObject battery;
    public GameObject temp;
    public GameObject light;
    public GameObject prox;
    public GameObject cell;
    public GameObject action;

    public List<string> action_list;
    public RulesAdministratorController administrator_controller;
    private void Start()
    {

    }

    private void OnEnable()
    {
        ruleName.text = "Rule " + administrator_controller.next_id;
    }

    public void AddRule()
    {
        rule = new RuleModel();
        rule.id_rule = main_View.GetComponent<MainView>().rules_list.rulemodel_list.Count;
        rule.name = ruleName.text;
        rule.battery_state = battery.GetComponent<Dropdown>().captionText.text;
        rule.sensor_temp = temp.GetComponent<Dropdown>().captionText.text;
        rule.sensor_light = light.GetComponent<Dropdown>().captionText.text;
        rule.sensor_cell = cell.GetComponent<Dropdown>().captionText.text;
        rule.sensor_prox = prox.GetComponent<Dropdown>().captionText.text;
        rule.action = action_list;
        main_View.GetComponent<MainView>().AddRule(rule);
        ruleName.text = "Rule " + main_View.GetComponent<MainView>().rules_list.rulemodel_list.Count;

        administrator_controller.LoadRules();
        action_list = new List<string>();
    }

    public void AddAction(Dropdown action)
    {
        action_list.Add(action.options[action.value].text);
    }
}
