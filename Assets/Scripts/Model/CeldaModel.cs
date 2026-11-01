using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum State 
{
    free,
    fire,
    dark,
    obstacle,
    battery
}

public class CeldaModel : MonoBehaviour
{
    public float pos_x;
    public float pos_y;

    public State estado;

    public GameObject fire;

}
