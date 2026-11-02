using UnityEngine;

namespace RobotBombero.Game
{
    /// <summary>Mantiene la pantalla encendida mientras una partida corre en automático; el resto del tiempo, el valor del sistema.</summary>
    public static class ScreenAwake
    {
        public static void Set(bool awake)
        {
            int wanted = awake ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            if (Screen.sleepTimeout != wanted) Screen.sleepTimeout = wanted;
        }
    }
}
