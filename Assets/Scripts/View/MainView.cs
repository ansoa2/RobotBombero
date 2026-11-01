using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class MainView : MonoBehaviour
{
    public bool refresh = false;
    public bool auto = false;

    public bool fuego_encendido = true;
    public int contador_fuego_encendido;

    public Text steps;
    public Text fuegos;
    public Text bateria_restante;


    public StatsController stats;

    public GameObject content;
    public GameObject filaPrefab;
    public GameObject celdaPrefab;
    public GameObject bombero;
    public GameObject obstaculo1;
    public GameObject obstaculo2;
    public GameObject obstaculo3;
    public GameObject fire_prefab;

    public List<List<GameObject>> celdas_matriz;
    public List<GameObject> celdas_list;
    public List<GameObject> fire_list;
    public RuleModelList rules_list;
    // Start is called before the first frame update
    void Start()
    {
        MostrarContadores();
        celdas_matriz = new List<List<GameObject>>();
        rules_list.rulemodel_list = new List<RuleModel>();
        for (int i = 0; i < 8; i++)
        {
            GameObject fila = Instantiate(filaPrefab) as GameObject;
            fila.transform.SetParent(content.transform);
            fila.transform.localScale = Vector3.one;
            celdas_list = new List<GameObject>();
            for (int j = 0; j < 8; j++)
            {
                GameObject celda = Instantiate(celdaPrefab) as GameObject;
                celda.transform.SetParent(fila.transform);
                celda.transform.localScale = Vector3.one;
                celda.GetComponent<CeldaModel>().pos_x = celda.GetComponent<RectTransform>().anchoredPosition.x;
                celda.GetComponent<CeldaModel>().pos_y = celda.GetComponent<RectTransform>().anchoredPosition.y;
                celda.GetComponent<CeldaModel>().estado = State.free;

                if(i == 4 || j == 3)
                {
                    celda.GetComponent<Image>().color = new Color32(144,144,144,255);
                    celda.GetComponent<CeldaModel>().estado = State.dark;
                }

                if(i == 4 && j == 3)
                {
                    celda.GetComponent<Image>().color = new Color32(255, 64, 64, 255);
                    celda.GetComponent<CeldaModel>().estado = State.battery;
                }
                celdas_list.Add(celda);

            }
            celdas_matriz.Add(celdas_list);
        }
       
    }

    public void Empezar()
    {
        StopAllCoroutines();
        stats.ReiniciarContadores();

        bombero.SetActive(true);
        obstaculo1.SetActive(true);
        obstaculo2.SetActive(true);
        obstaculo3.SetActive(true);

        auto = false;

        bombero.GetComponent<Robot>().ReiniciarValores();
        bombero.GetComponent<Robot>().ControlSensores();
        contador_fuego_encendido = 0;

        bombero.transform.position = new Vector3(celdas_matriz[3][3].GetComponent<RectTransform>().position.x, celdas_matriz[3][3].GetComponent<RectTransform>().position.y, 0);
        bombero.GetComponent<Robot>().pos_x = 3;
        bombero.GetComponent<Robot>().pos_y = 3;

        obstaculo1.transform.position = new Vector3(celdas_matriz[1][5].GetComponent<RectTransform>().position.x, celdas_matriz[1][5].GetComponent<RectTransform>().position.y, 0);
        celdas_matriz[1][5].GetComponent<CeldaModel>().estado = State.obstacle;
        obstaculo2.transform.position = new Vector3(celdas_matriz[5][1].GetComponent<RectTransform>().position.x, celdas_matriz[5][1].GetComponent<RectTransform>().position.y, 0);
        celdas_matriz[5][1].GetComponent<CeldaModel>().estado = State.obstacle;
        obstaculo3.transform.position = new Vector3(celdas_matriz[6][6].GetComponent<RectTransform>().position.x, celdas_matriz[6][6].GetComponent<RectTransform>().position.y, 0);
        celdas_matriz[6][6].GetComponent<CeldaModel>().estado = State.obstacle;

        foreach (GameObject o in fire_list)
        {
            Destroy(o);
        }
        fire_list = new List<GameObject>();

        GameObject fire = Instantiate(fire_prefab) as GameObject;
        fire.SetActive(true);
        fire.transform.position = new Vector3(celdas_matriz[1][2].GetComponent<RectTransform>().position.x, celdas_matriz[1][2].GetComponent<RectTransform>().position.y, 0);
        celdas_matriz[1][2].GetComponent<CeldaModel>().estado = State.fire;
        celdas_matriz[1][2].GetComponent<CeldaModel>().fire = fire;
        fire.GetComponent<FireModel>().pos_y = 1;
        fire.GetComponent<FireModel>().pos_x = 2;
        fire_list.Add(fire);

        MostrarContadores();
        RefreshScreen();
    }

    // Update is called once per frame
    void Update()
    {
        if (refresh)
        {
            RefreshScreen();
            refresh = false;
        }
        
    }

    public void NextStep()
    {
        int i = 0;
        bool regla_encontrada = false;
        bool battery_state;
        bool sensor_temp;
        bool sensor_light;
        bool sensor_prox;
        bool sensor_cell;

        if(fuego_encendido)
            contador_fuego_encendido++;

        if(contador_fuego_encendido >= 100)
        {
            bombero.GetComponent<Robot>().muerto = true;
        }

        if (!bombero.GetComponent<Robot>().muerto)
        {
            while (i < rules_list.rulemodel_list.Count && !regla_encontrada)
            {
                battery_state = false;
                sensor_temp = false;
                sensor_light = false;
                sensor_prox = false;
                sensor_cell = false;

                switch (rules_list.rulemodel_list[i].battery_state)
                {
                    case "Anyone":
                        battery_state = true;
                        break;

                    case "High":
                        if (bombero.GetComponent<Robot>().batteryState == BatteryState.high)
                        {
                            battery_state = true;
                        }
                        break;
                    case "Medium":
                        if (bombero.GetComponent<Robot>().batteryState == BatteryState.medium)
                        {
                            battery_state = true;
                        }
                        break;
                    case "Low":
                        if (bombero.GetComponent<Robot>().batteryState == BatteryState.low)
                        {
                            battery_state = true;
                        }
                        break;
                }

                switch (rules_list.rulemodel_list[i].sensor_temp)
                {
                    case "Anyone":
                        sensor_temp = true;
                        break;
                    case "Up":
                        if (bombero.GetComponent<Robot>().sensorTemp == SensorTemp.up)
                        {
                            sensor_temp = true;
                        }
                        break;
                    case "Down":
                        if (bombero.GetComponent<Robot>().sensorTemp == SensorTemp.down)
                        {
                            sensor_temp = true;
                        }
                        break;
                    case "Equal":
                        if (bombero.GetComponent<Robot>().sensorTemp == SensorTemp.equal)
                        {
                            sensor_temp = true;
                        }
                        break;
                }

                switch (rules_list.rulemodel_list[i].sensor_light)
                {
                    case "Anyone":
                        sensor_light = true;

                        break;
                    case "Normal":
                        if (bombero.GetComponent<Robot>().sensorLight == SensorLight.normal)
                        {
                            sensor_light = true;
                        }
                        break;
                    case "Bright":
                        if (bombero.GetComponent<Robot>().sensorLight == SensorLight.bright)
                        {
                            sensor_light = true;
                        }
                        break;
                    case "Dark":
                        if (bombero.GetComponent<Robot>().sensorLight == SensorLight.dark)
                        {
                            sensor_light = true;
                        }
                        break;
                }

                switch (rules_list.rulemodel_list[i].sensor_prox)
                {
                    case "Anyone":
                        sensor_prox = true;
                        break;
                    case "Obstacle Yes":
                        if (bombero.GetComponent<Robot>().sensorProx == SensorProx.obstacle_true)
                        {
                            sensor_prox = true;
                        }
                        break;
                    case "Obstacle No":
                        if (bombero.GetComponent<Robot>().sensorProx == SensorProx.obstacle_false)
                        {
                            sensor_prox = true;
                        }
                        break;

                }

                switch (rules_list.rulemodel_list[i].sensor_cell)
                {
                    case "Anyone":
                        sensor_cell = true;
                        break;
                    case "Base":
                        if (bombero.GetComponent<Robot>().sensorCelda == SensorCelda.battery)
                        {
                            sensor_cell = true;
                        }
                        break;
                    case "Normal":
                        if (bombero.GetComponent<Robot>().sensorCelda == SensorCelda.normal)
                        {
                            sensor_cell = true;
                        }
                        break;
                    case "Black":
                        if (bombero.GetComponent<Robot>().sensorCelda == SensorCelda.black)
                        {
                            sensor_cell = true;
                        }
                        break;

                }

                if (battery_state && sensor_temp && sensor_light && sensor_prox && sensor_cell)
                {
                    //Debug.Log(rules_list.rulemodel_list[i].name);
                    for(int j = 0; j < rules_list.rulemodel_list[i].action.Count; j++)
                    {
                        switch (rules_list.rulemodel_list[i].action[j])
                        {
                            case "Fwd":
                                bombero.GetComponent<Robot>().fwd();
                                break;
                            case "Put Out":
                                bombero.GetComponent<Robot>().Put_Out();
                                break;
                            case "TurnL":
                                bombero.GetComponent<Robot>().turnL();
                                break;
                            case "TurnR":
                                bombero.GetComponent<Robot>().turnR();
                                break;
                        }
                    }

                    regla_encontrada = true;
                }
                i++;
            }
            if (!regla_encontrada)
            {
                bombero.GetComponent<Robot>().battery -= 1;
            }

            stats.IncrementarContadorCiclos();
        }
        else
        {
            StopAllCoroutines();
            PopUp popUp = new PopUp("Robot muerto", "El robot ha sobrevivido "+ stats.ciclos_vivo+ " ciclos \n Y ha apagado "+ stats.fuegos_apagados+" fuegos");
            popUp.AddAction("Aceptar", () => { Empezar();popUp.Close(); });
            popUp.Show();
        }

        MostrarContadores();
        bombero.GetComponent<Robot>().ControlSensores();
    }

    public void InsertarFuego()
    {
        int i = 0;
        int j = 0;
        bool encontrado = false;

        ReiniciarContadorFuego();

        while (!encontrado)
        {
            i = Random.Range(0, 8);
            j = Random.Range(0, 8);

            if(celdas_matriz[i][j].GetComponent<CeldaModel>().estado == State.free && i != bombero.GetComponent<Robot>().pos_y && j != bombero.GetComponent<Robot>().pos_x)
            {
                GameObject fire = Instantiate(fire_prefab) as GameObject;
                fire.SetActive(true);
                fire.transform.position = new Vector3(celdas_matriz[i][j].GetComponent<RectTransform>().position.x, celdas_matriz[i][j].GetComponent<RectTransform>().position.y, 0);
                celdas_matriz[i][j].GetComponent<CeldaModel>().estado = State.fire;
                celdas_matriz[i][j].GetComponent<CeldaModel>().fire = fire;
                fire.GetComponent<FireModel>().pos_y = i;
                fire.GetComponent<FireModel>().pos_x = j;
                fire_list.Add(fire);
                encontrado = true;
            }
        }
    }



    public void AddRule(RuleModel rule)
    {
        //rules_list.Add(rule);
        rules_list.rulemodel_list.Insert(0, rule);
    }

    public void RefreshScreen()
    {
        bombero.transform.position = new Vector3(celdas_matriz[3][3].GetComponent<RectTransform>().position.x, celdas_matriz[3][3].GetComponent<RectTransform>().position.y, 0);
    }

    public void ConfirmarGuardadoReglas()
    {
        PopUp popUp = new PopUp("Confirmar guardado de reglas", "¿Seguro que desea sobrescribir las reglas?");
        popUp.AddAction("Confirmar", () => { CreateJsonRules(); popUp.Close(); });
        popUp.AddAction("Cancelar", () => {popUp.Close(); });
        popUp.Show();
    }

    public void CreateJsonRules()
    {
        string name = "rules";
        string path = Application.persistentDataPath + "/" + name;
        string json = JsonUtility.ToJson(rules_list);

        using (FileStream fs = new FileStream (path, FileMode.Create))
        {
            using (StreamWriter writer = new StreamWriter(fs))
            {
                writer.Write(json);
            }
        }
    }

    public void ReiniciarContadorFuego()
    {
        contador_fuego_encendido = 0;
    }

    public void MostrarContadores()
    {
        steps.text = "Pasos realizados: " + stats.ciclos_vivo;
        fuegos.text = "Fuegos apagados: " + stats.fuegos_apagados;
        bateria_restante.text = "BateriaRestante: " + bombero.GetComponent<Robot>().battery;
    }

    public void LoadRules()
    {
        string name = "rules";
        string path = Application.persistentDataPath + "/" + name;
        string file;
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(fs))
                {
                    file = reader.ReadToEnd();

                    rules_list = JsonUtility.FromJson<RuleModelList>(file);
                }
            }
        }
        catch
        {

        }
    }

    public void AutoSteps()
    {
        StopAllCoroutines();
        StartCoroutine("AutoStepsCorrutine");
    }

    IEnumerator AutoStepsCorrutine()
    {
        NextStep();
        yield return new WaitForSeconds(0.05f);
        StartCoroutine("AutoStepsCorrutine");
    }

    public void ActivarAuto()
    {
        auto = true;
    }
}
