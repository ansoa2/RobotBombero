using UnityEngine;

namespace RobotBombero.Game
{
    /// <summary>Un cliente de red cuya dirección base se puede cambiar mientras la aplicación está abierta.</summary>
    public interface IServerTarget
    {
        string BaseUrl { get; set; }
    }

    /// <summary>
    /// La dirección del servidor de esta app: la fijada al compilar con el parámetro del script. El jugador no la ve ni la cambia.
    /// </summary>
    public sealed class ServerConfig
    {
        /// <summary>Recurso de texto que escribe el script de compilación con la dirección por defecto. Puede no existir.</summary>
        public const string DefaultResource = "rb-server-default";

        public ServerConfig(string defaultAddress)
        {
            DefaultAddress = defaultAddress;
        }

        public string DefaultAddress { get; private set; }

        /// <summary>La configuración de esta app.</summary>
        public static ServerConfig ForPlatform()
        {
            var asset = Resources.Load<TextAsset>(DefaultResource);
            string text = asset == null ? null : asset.text.Trim();
            return new ServerConfig(string.IsNullOrEmpty(text) ? null : text);
        }
    }
}
