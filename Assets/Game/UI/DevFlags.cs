#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>
    /// Solo en compilaciones de desarrollo o en el editor: permiten capturar las pantallas sin ratón.
    /// -rb-data dir        carpeta del almacén local
    /// -rb-api url         dirección del servidor
    /// -rb-screen id       identidad de demostración en memoria (sin guardar) y abre esa pantalla
    /// -rb-name texto      escribe ese nombre en el acceso
    /// -rb-press a,b       pulsa esos botones, en orden, con eventos reales del panel
    /// </summary>
    internal static class DevFlags
    {
        public static string Get(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }

        public static IEnumerator Apply(VisualElement root, GameContext context, ScreenRouter router)
        {
            string screen = Get("-rb-screen");
            if (screen != null)
            {
                context.Profile = new Profile { playerName = "Bombera_7", playerKey = "demostracion", playerId = 1 };
                router.Show((ScreenId)Enum.Parse(typeof(ScreenId), screen));
            }

            string name = Get("-rb-name");
            if (name != null) root.Q<TextField>("login-name").value = name;

            string press = Get("-rb-press");
            if (press == null) yield break;

            // El panel tiene que haber hecho el primer cálculo de distribución.
            yield return new WaitForSeconds(0.5f);
            foreach (string button in press.Split(','))
            {
                Button target = root.Q<Button>(button);
                if (target == null)
                {
                    Debug.LogWarning("RobotBombero: no existe el botón " + button);
                    continue;
                }
                using (NavigationSubmitEvent e = NavigationSubmitEvent.GetPooled())
                {
                    e.target = target;
                    target.SendEvent(e);
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
#endif
