using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BatteryState
{
    high,
    medium,
    low
}

public enum SensorTemp
{
    up,
    down, 
    equal
}

public enum SensorLight 
{ 
    normal,
    bright,
    dark
}

public enum SensorProx
{
    obstacle_true,
    obstacle_false
}

public enum SensorCelda
{
    battery,
    normal,
    black
}
    

public class Robot : MonoBehaviour
{
    public MainView main;
    public int battery;
    public bool muerto;
    public BatteryState batteryState;
    public SensorTemp sensorTemp;
    public SensorLight sensorLight;
    public SensorProx sensorProx;
    public SensorCelda sensorCelda;

    public int pos_x;
    public int pos_y;
    public int dir_x;
    public int dir_y;



    public bool cerca = false;

    private void Start()
    {
        battery = 50;
        this.transform.rotation = Quaternion.Euler(0, 0, 180);
        batteryState = BatteryState.high;
        sensorTemp = SensorTemp.equal;
        sensorLight = SensorLight.normal;
        sensorProx = SensorProx.obstacle_false;
        sensorCelda = SensorCelda.black;
        dir_x = 0;
        dir_y = -1;
        muerto = false;
        ControlSensores();
    }

    public void ReiniciarValores()
    {
        battery = 50;
        this.transform.rotation = Quaternion.Euler(0, 0, 180);
        batteryState = BatteryState.high;
        sensorTemp = SensorTemp.equal;
        sensorLight = SensorLight.normal;
        sensorProx = SensorProx.obstacle_false;
        sensorCelda = SensorCelda.black;
        dir_x = 0;
        dir_y = -1;
        muerto = false;
        ControlSensores();
    }

    //Giro derecha
    public void turnR()
    {
        if (sensorCelda == SensorCelda.battery)
        {
            battery = 50;
        }
        ControlSensores();
        if (sensorCelda == SensorCelda.battery)
        {
            battery = 50;
        }

        if (dir_x == 1 && dir_y == 0)
        {
            dir_x = 0;
            dir_y = 1;
        }else if(dir_x == 0 && dir_y == -1)
        {
            dir_x = 1;
            dir_y = 0;
        }
        else if (dir_x == -1 && dir_y == 0)
        {
            dir_x = 0;
            dir_y = -1;
        }
        else if (dir_x == 0 && dir_y == 1)
        {
            dir_x = -1;
            dir_y = 0;
        }

        this.gameObject.transform.Rotate(0, 0, -90);
        battery -= 1;
        ControlSensores();
    }
    //Giro izquierda
    public void turnL()
    {
        if (sensorCelda == SensorCelda.battery)
        {
            battery = 50;
        }
        ControlSensores();

        if (dir_x == 1 && dir_y == 0)
        {
            dir_x = 0;
            dir_y = -1;
        }
        else if (dir_x == 0 && dir_y == -1)
        {
            dir_x = -1;
            dir_y = 0;
        }
        else if (dir_x == -1 && dir_y == 0)
        {
            dir_x = 0;
            dir_y = 1;
        }
        else if (dir_x == 0 && dir_y == 1)
        {
            dir_x = 1;
            dir_y = 0;
        }

        this.gameObject.transform.Rotate(0, 0, 90);
        battery -= 1;
        ControlSensores();
    }
    //Avanza
    public void fwd()
    {
        ControlSensores();
        if (sensorLight == SensorLight.bright)
        {
            muerto = true;
        }
        else
        {

            if(sensorProx == SensorProx.obstacle_true)
            {
                battery -= 1;
            }
            else
            {
                battery -= 1;

                if (sensorCelda == SensorCelda.battery)
                {
                    battery = 50;
                }

                if(pos_y + dir_y < 8 && pos_y + dir_y >= 0 && pos_x + dir_x < 8 && pos_x + dir_x >= 0)
                {
                    this.gameObject.transform.position = new Vector3(main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<RectTransform>().position.x, main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<RectTransform>().position.y, 0);
                    pos_x += dir_x;
                    pos_y += dir_y;

                    if (sensorLight == SensorLight.bright)
                    {
                        muerto = true;
                    }
                }
            }
        }

        ControlSensores();

    }

    public void comprobarBateria()
    {
        if(battery > 30)
        {
            batteryState = BatteryState.high;
        }
        else if(battery < 30 && battery > 10)
        {
            batteryState = BatteryState.medium;
        }
        else if(battery < 10)
        {
            batteryState = BatteryState.low;
        }

        if(battery <= 0)
        {
            //Robot muerto
            muerto = true;
        }

    }

    public void Put_Out()
    {
        ControlSensores();
        battery -= 5;
        if (pos_y + dir_y < 8 && pos_y + dir_y >= 0 && pos_x + dir_x < 8 && pos_x + dir_x >= 0)
        {
            if (main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<CeldaModel>().estado == State.fire)
            {
                for(int i = 0; i <  main.fire_list.Count; i++)
                {
                    if(main.fire_list[i].GetComponent<FireModel>().pos_x == pos_x + dir_x && main.fire_list[i].GetComponent<FireModel>().pos_y == pos_y + dir_y)
                    {
                        main.stats.IncrementarContadorFuegos();
                        main.fuego_encendido = true;
                        main.fire_list.RemoveAt(i);
                        main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<CeldaModel>().estado = State.free;
                        Destroy(main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<CeldaModel>().fire);
                        
                        main.InsertarFuego();

                    }
                }
                
            }
                   
        }
    }

    public void ControlSensores()
    {
        ControlSensorTemp();
        ControlSensorLight();
        ControlSensorProx();
        ControlSensorCelda();
        comprobarBateria();
    }

    public void ControlSensorTemp()
    {
        cerca = false;

        sensorTemp = SensorTemp.equal;
        for (int i = 0; i <  main.fire_list.Count; i++)
        {
            if(dir_x != 0)
            {
                if((Mathf.Abs(main.fire_list[i].GetComponent<FireModel>().pos_x - pos_x) > Mathf.Abs(main.fire_list[i].GetComponent<FireModel>().pos_x - (pos_x + dir_x))))
                {
                    cerca = true;
                }

            }else if(dir_y != 0)
            {
                if ((Mathf.Abs(main.fire_list[i].GetComponent<FireModel>().pos_y - pos_y) > Mathf.Abs(main.fire_list[i].GetComponent<FireModel>().pos_y - (pos_y + dir_y))))
                {
                    cerca = true;
                }
            }

            if (cerca)
            {
                sensorTemp = SensorTemp.up;
            }
            else
            {
                sensorTemp = SensorTemp.down;
            }
        }
        
    }

    public void ControlSensorLight()
    {
        if (pos_y + dir_y < 8 && pos_y + dir_y >= 0 && pos_x + dir_x < 8 && pos_x + dir_x >= 0)
        {
            if(main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<CeldaModel>().estado == State.fire)
            {
                sensorLight = SensorLight.bright;
            }
            else if (main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<CeldaModel>().estado == State.dark || main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<CeldaModel>().estado == State.battery)
            {
                sensorLight = SensorLight.dark;
            }
            else
            {
                sensorLight = SensorLight.normal;
            }
        }
        else
        {
            sensorLight = SensorLight.normal;
        }
            
    }

    public void ControlSensorProx()
    {
        if (pos_y + dir_y < 8 && pos_y + dir_y >= 0 && pos_x + dir_x < 8 && pos_x + dir_x >= 0)
        {
            if (main.celdas_matriz[pos_y + dir_y][pos_x + dir_x].GetComponent<CeldaModel>().estado == State.obstacle)
            {
                sensorProx = SensorProx.obstacle_true;
            }
            else
            {
                sensorProx = SensorProx.obstacle_false;
            }
        }
        else
        {
            sensorProx = SensorProx.obstacle_true;
        }
    }

    public void ControlSensorCelda()
    {
        if (main.celdas_matriz[pos_y][pos_x].GetComponent<CeldaModel>().estado == State.dark)
        {
            sensorCelda = SensorCelda.black;
        }
        else if (main.celdas_matriz[pos_y][pos_x].GetComponent<CeldaModel>().estado == State.battery)
        {
            sensorCelda = SensorCelda.battery;
        }
        else
        {
            sensorCelda = SensorCelda.normal;
        }

    }



}
