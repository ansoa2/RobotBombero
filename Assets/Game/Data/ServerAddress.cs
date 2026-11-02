using System;

namespace RobotBombero.Game
{
    /// <summary>Resultado de normalizar una dirección: la URL base o el motivo del rechazo (en español).</summary>
    public sealed class ServerAddressResult
    {
        public bool IsValid { get; private set; }
        public string Url { get; private set; }
        public string Error { get; private set; }

        public static ServerAddressResult Ok(string url) { return new ServerAddressResult { IsValid = true, Url = url }; }
        public static ServerAddressResult Fail(string error) { return new ServerAddressResult { IsValid = false, Error = error }; }
    }

    /// <summary>
    /// La dirección del servidor fijada al compilar (host:puerto, http://mipc, ...) convertida en una URL base sin barra final.
    /// Sin esquema se usa http; sin puerto, el 5080. El jugador nunca la escribe ni la ve.
    /// </summary>
    public static class ServerAddress
    {
        public const int DefaultPort = 5080;

        private const string Http = "http://";
        private const string Https = "https://";

        private static string Invalid(string why)
        {
            return why;
        }

        public static ServerAddressResult Normalize(string input)
        {
            string text = (input ?? "").Trim();
            if (text.Length == 0) return ServerAddressResult.Fail(Invalid("Falta la dirección del servidor."));

            string scheme = Http;
            if (text.StartsWith(Http, StringComparison.OrdinalIgnoreCase)) text = text.Substring(Http.Length);
            else if (text.StartsWith(Https, StringComparison.OrdinalIgnoreCase)) { scheme = Https; text = text.Substring(Https.Length); }
            else if (text.Contains("://")) return ServerAddressResult.Fail(Invalid("Solo se admiten direcciones http o https."));

            text = text.TrimEnd('/');
            if (text.Length == 0) return ServerAddressResult.Fail(Invalid("Falta la dirección del servidor."));
            foreach (char c in text)
                if (c == '/' || c == '?' || c == '#' || c == '@' || char.IsWhiteSpace(c))
                    return ServerAddressResult.Fail(Invalid("La dirección no es válida: no puede llevar ruta, espacios ni usuario."));

            string host = text;
            int port = DefaultPort;
            int colon = text.LastIndexOf(':');
            if (colon >= 0)
            {
                host = text.Substring(0, colon);
                string portText = text.Substring(colon + 1);
                if (!TryPort(portText, out port)) return ServerAddressResult.Fail(Invalid("El puerto debe ser un número entre 1 y 65535."));
            }

            if (!IsValidHost(host)) return ServerAddressResult.Fail(Invalid("La dirección del servidor no es válida."));
            return ServerAddressResult.Ok(scheme + host.ToLowerInvariant() + ":" + port);
        }

        private static bool TryPort(string text, out int port)
        {
            port = 0;
            if (text.Length == 0 || text.Length > 5) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            port = int.Parse(text);
            return port >= 1 && port <= 65535;
        }

        private static bool IsValidHost(string host)
        {
            if (host.Length == 0 || host.Length > 253) return false;
            bool numeric = true;
            foreach (char c in host) if (!(c >= '0' && c <= '9') && c != '.') { numeric = false; break; }
            if (numeric) return IsIPv4(host);

            foreach (string label in host.Split('.'))
            {
                if (label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-') return false;
                foreach (char c in label)
                {
                    bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-';
                    if (!ok) return false;
                }
            }
            return true;
        }

        private static bool IsIPv4(string host)
        {
            string[] parts = host.Split('.');
            if (parts.Length != 4) return false;
            foreach (string part in parts)
            {
                if (part.Length == 0 || part.Length > 3) return false;
                if (int.Parse(part) > 255) return false;
            }
            return true;
        }
    }
}
