using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RulesAdministratorController : MonoBehaviour
{
    public GameObject prefab_itemRule;

    public GameObject content;
    public MainView main;
    public GameObject main_View;
    RuleModel rule;
    public int next_id;

    public Vector3 initialSizeDelta;

    private void Awake()
    {
        initialSizeDelta = content.GetComponent<RectTransform>().sizeDelta;
        next_id = 0;
    }

    void OnEnable()
    {
        LoadRules();
    }

    public void LoadRules()
    {
        if(main.rules_list.rulemodel_list.Count > 0)
            next_id = main.rules_list.rulemodel_list[0].id_rule + 1;

        for (int i = 0; i < content.transform.childCount; i++)
        {
            Destroy(content.transform.GetChild(i).gameObject);
        }

        for (int i = 0; i < main.rules_list.rulemodel_list.Count; i++)
        {
            GameObject rule = Instantiate(prefab_itemRule) as GameObject;
            rule.transform.SetParent(content.transform);
            rule.GetComponentInChildren<Text>().text = main.rules_list.rulemodel_list[i].name;
            rule.transform.localScale = Vector3.one;

            rule.GetComponent<EditRuleController>().id = main.rules_list.rulemodel_list[i].id_rule;
            rule.GetComponent<EditRuleController>().rule_name.text = main.rules_list.rulemodel_list[i].name;

            for (int j = 0; j < rule.GetComponent<EditRuleController>().battery.options.Count; j++)
            {
                if (rule.GetComponent<EditRuleController>().battery.options[j].text == main.rules_list.rulemodel_list[i].battery_state)
                {
                    rule.GetComponent<EditRuleController>().battery.value = j;
                }
            }

            for (int j = 0; j < rule.GetComponent<EditRuleController>().sensor_temp.options.Count; j++)
            {
                if (rule.GetComponent<EditRuleController>().sensor_temp.options[j].text == main.rules_list.rulemodel_list[i].sensor_temp)
                {
                    rule.GetComponent<EditRuleController>().sensor_temp.value = j;
                }
            }


            for (int j = 0; j < rule.GetComponent<EditRuleController>().sensor_light.options.Count; j++)
            {
                if (rule.GetComponent<EditRuleController>().sensor_light.options[j].text == main.rules_list.rulemodel_list[i].sensor_light)
                {
                    rule.GetComponent<EditRuleController>().sensor_light.value = j;
                }
            }

            for (int j = 0; j < rule.GetComponent<EditRuleController>().sensor_cell.options.Count; j++)
            {
                if (rule.GetComponent<EditRuleController>().sensor_cell.options[j].text == main.rules_list.rulemodel_list[i].sensor_cell)
                {
                    rule.GetComponent<EditRuleController>().sensor_cell.value = j;
                }
            }

            for (int j = 0; j < rule.GetComponent<EditRuleController>().sensor_prox.options.Count; j++)
            {
                if (rule.GetComponent<EditRuleController>().sensor_prox.options[j].text == main.rules_list.rulemodel_list[i].sensor_prox)
                {
                    rule.GetComponent<EditRuleController>().sensor_prox.value = j;
                }
            }
            /*
            for (int j = 0; j < rule.GetComponent<EditRuleController>().action.options.Count; j++)
            {
                if (rule.GetComponent<EditRuleController>().action.options[j].text == main.rules_list.rulemodel_list[i].action[0])
                {
                    rule.GetComponent<EditRuleController>().action.value = j;
                }
            }*/

            rule.GetComponent<EditRuleController>().action_list = main.rules_list.rulemodel_list[i].action;

        }
        content.GetComponent<RectTransform>().sizeDelta = new Vector2(initialSizeDelta.x, initialSizeDelta.y + 700 * main.rules_list.rulemodel_list.Count);
    }

    public void SaveEditRules()
    {
        main_View.GetComponent<MainView>().rules_list.rulemodel_list = new List<RuleModel>();

        for (int i = 0; i < content.transform.childCount; i++)
        {
            rule = new RuleModel();
            rule.id_rule = content.transform.GetChild(i).GetComponent<EditRuleController>().id;
            rule.name = content.transform.GetChild(i).GetComponent<EditRuleController>().rule_name.text;
            rule.battery_state = content.transform.GetChild(i).GetComponent<EditRuleController>().battery.captionText.text;
            rule.sensor_temp = content.transform.GetChild(i).GetComponent<EditRuleController>().sensor_temp.captionText.text;
            rule.sensor_light = content.transform.GetChild(i).GetComponent<EditRuleController>().sensor_light.captionText.text;
            rule.sensor_cell = content.transform.GetChild(i).GetComponent<EditRuleController>().sensor_cell.captionText.text;
            rule.sensor_prox = content.transform.GetChild(i).GetComponent<EditRuleController>().sensor_prox.captionText.text;
            rule.action = content.transform.GetChild(i).GetComponent<EditRuleController>().action_list;
            main_View.GetComponent<MainView>().rules_list.rulemodel_list.Add(rule);
        }

        LoadRules();
    }
}
