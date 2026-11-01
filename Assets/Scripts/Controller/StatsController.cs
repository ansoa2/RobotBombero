using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatsController : MonoBehaviour
{
    public int fuegos_apagados = 0;
    public int ciclos_vivo = 0;

    public void ReiniciarContadores()
    {
        ReiniciarContadorFuegos();
        ReiniciarContadorCiclos();
    }
    public void ReiniciarContadorFuegos()
    {
        fuegos_apagados = 0;
    }

    public void ReiniciarContadorCiclos()
    {
        ciclos_vivo = 0;
    }

    public void IncrementarContadorFuegos()
    {
        fuegos_apagados++;
    }

    public void IncrementarContadorCiclos()
    {
        ciclos_vivo++;
    }
}
