#if DEVELOPMENT_BUILD || UNITY_EDITOR || RB_CAPTURE
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>
    /// Solo en compilaciones de desarrollo, en el editor o con RB_CAPTURE: permiten capturar las pantallas sin ratón.
    /// -rb-data dir        carpeta del almacén local
    /// -rb-api url         dirección del servidor
    /// -rb-screen id       identidad de demostración en memoria (sin guardar) y abre esa pantalla
    /// -rb-name texto      escribe ese nombre en el acceso
    /// -rb-server texto    escribe esa dirección en el campo del servidor del acceso
    /// -rb-sample          con -rb-screen: packs de ejemplo, resultado y pack de partida (ver DevSamples)
    /// -rb-press a,b       pulsa esos botones, en orden, con eventos reales del panel
    ///                     (también acepta @pack, @wait=segundos, @shot=nombre y @dump=a+b; ver Apply)
    /// -rb-shotdir dir     carpeta de las capturas @shot (por defecto, la del almacén local)
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

        public static bool Has(string flag)
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;
        }

        public static IEnumerator Apply(VisualElement root, GameContext context, ScreenRouter router)
        {
            string screen = Get("-rb-screen");
            if (screen != null)
            {
                context.Profile = new Profile { playerName = "Bombera_7", playerKey = "demostracion", playerId = 1 };
                if (Has("-rb-sample")) DevSamples.Seed(context);
                router.Show((ScreenId)Enum.Parse(typeof(ScreenId), screen));
            }

            string name = Get("-rb-name");
            if (name != null) root.Q<TextField>("login-name").value = name;

            string server = Get("-rb-server");
            if (server != null) root.Q<TextField>("login-server").value = server;

            string press = Get("-rb-press");
            if (press == null) yield break;

            // El panel tiene que haber hecho el primer cálculo de distribución.
            yield return new WaitForSeconds(0.5f);
            foreach (string button in press.Split(','))
            {
                if (button.StartsWith("@"))
                {
                    yield return Special(button.Substring(1), root, context);
                    continue;
                }

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

        /// <summary>
        /// Pasos especiales de -rb-press: pack (crea un pack propio con reglas y lo sincroniza con el servidor),
        /// wait=segundos, shot=nombre (captura PNG de la ventana) y dump=a+b (escribe en el log el texto de esas etiquetas).
        /// </summary>
        private static IEnumerator Special(string step, VisualElement root, GameContext context)
        {
            string arg = null;
            int eq = step.IndexOf('=');
            if (eq >= 0) { arg = step.Substring(eq + 1); step = step.Substring(0, eq); }

            if (step == "pack")
            {
                DevSamples.AddPlainPack(context);
                context.Run(context.Packs.SyncPending());
                yield return new WaitForSeconds(1f);
            }
            else if (step == "wait")
            {
                yield return new WaitForSeconds(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (step == "shot")
            {
                string dir = Get("-rb-shotdir") ?? Get("-rb-data") ?? ".";
                Directory.CreateDirectory(dir);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, arg + ".png"));
                yield return new WaitForSeconds(1f);
            }
            else if (step == "dump")
            {
                foreach (string label in arg.Split('+'))
                {
                    Label l = root.Q<Label>(label);
                    Debug.Log("RB_DUMP " + label + " = " + (l == null ? "(no existe)" : l.text));
                }
            }
            else Debug.LogWarning("RobotBombero: paso desconocido @" + step);
        }
    }
}
#endif
