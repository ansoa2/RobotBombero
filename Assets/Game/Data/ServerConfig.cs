using UnityEngine;

namespace RobotBombero.Game
{
    /// <summary>Un cliente de red cuya dirección base se puede cambiar mientras la aplicación está abierta.</summary>
    public interface IServerTarget
    {
        string BaseUrl { get; set; }
    }

    /// <summary>
    /// Si la plataforma necesita que el jugador elija el servidor (todas menos WebGL, que usa rutas relativas)
    /// y cuál es la dirección por defecto fijada al compilar.
    /// </summary>
    public sealed class ServerConfig
    {
        /// <summary>Recurso de texto que escribe el script de compilación con la dirección por defecto. Puede no existir.</summary>
        public const string DefaultResource = "rb-server-default";

        public ServerConfig(bool enabled, string defaultAddress)
        {
            Enabled = enabled;
            DefaultAddress = defaultAddress;
        }

        public bool Enabled { get; private set; }
        public string DefaultAddress { get; private set; }

        /// <summary>La configuración de esta plataforma: WebGL (fuera del editor) no usa dirección.</summary>
        public static ServerConfig ForPlatform()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new ServerConfig(false, null);
#else
            var asset = Resources.Load<TextAsset>(DefaultResource);
            string text = asset == null ? null : asset.text.Trim();
            return new ServerConfig(true, string.IsNullOrEmpty(text) ? null : text);
#endif
        }
    }
}
