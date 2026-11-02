using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>
    /// Pantalla de acceso: comprueba el nombre con las mismas reglas que el servidor, lo registra y guarda la clave.
    /// La clave solo va a LocalStore; nunca se escribe en el log.
    /// </summary>
    public sealed class LoginController
    {
        public const int MinNameLength = 3;
        public const int MaxNameLength = 20;

        private const string MsgLength = "El nombre debe tener entre 3 y 20 caracteres.";
        private const string MsgCharacters = "Usa solo letras sin acentos, números, _ o -. La ñ tampoco vale.";
        private const string MsgTaken = "Ese nombre ya está en uso. Prueba con otro.";
        public const string MsgOffline = "No se puede conectar con el servidor.";
        private const string MsgServer = "El servidor no ha podido atender la petición. Inténtalo de nuevo en un momento.";
        private const string MsgCannotSave = "No se ha podido guardar tu perfil en este dispositivo. Revisa el espacio del navegador y reintenta.";

        private const string TextEnter = "Entrar";
        private const string TextBusy = "Entrando…";
        public const string TextOffline = "Jugar sin conexión";
        private readonly Button _offline;

        private readonly GameContext _context;
        private readonly Action _loggedIn;
        private readonly TextField _name;
        private readonly Action _playOffline;
        private readonly Button _enter;
        private readonly Button _retry;
        private readonly Label _message;

        private bool _busy;
        // Nombre ya concedido por el servidor que falta guardar. Volver a pedirlo daría 409.
        private Profile _granted;

        public LoginController(VisualElement view, GameContext context, Action loggedIn, Action playOffline = null)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (context == null) throw new ArgumentNullException("context");
            _context = context;
            _loggedIn = loggedIn;
            _playOffline = playOffline;
            _name = Require<TextField>(view, "login-name");
            _enter = Require<Button>(view, "login-enter");
            _retry = Require<Button>(view, "login-retry");
            _offline = Require<Button>(view, "login-offline");
            _message = Require<Label>(view, "login-message");

            _name.maxLength = MaxNameLength;
            Label note = view.Q<Label>("login-note");
            if (note != null) note.text = "Tu progreso se guarda en este dispositivo y se pierde si borras los datos de la aplicación.";
            _enter.text = TextEnter;
            _enter.clicked += Submit;
            _retry.clicked += Submit;
            _offline.text = TextOffline;
            _offline.clicked += PlayOffline;
            _name.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _name.RegisterValueChangedCallback(e => NameChanged());
            ClearMessage();
        }

        /// <summary>Se muestra la pantalla (al arrancar, tras una sesión caducada o desde "Registrar nombre"): sin avisos viejos.</summary>
        public void OnShow()
        {
            if (_busy || _granted != null) return;
            ClearMessage();
        }

        /// <summary>Sigue sin conexión: se recuerda para el próximo arranque y se entra al menú sin tocar la red.</summary>
        public void PlayOffline()
        {
            if (_busy) return;
            _context.StartOffline();
            if (_playOffline != null) _playOffline();
        }

        /// <summary>Un aviso del resto de la aplicación (por ejemplo, sesión caducada); desaparece al cambiar el nombre.</summary>
        public void ShowNotice(string text)
        {
            _granted = null;
            ShowMessage(text, false);
        }

        /// <summary>El jugador está corrigiendo el nombre: el mensaje anterior ya no vale. No toca el reintento de guardado.</summary>
        public void NameChanged()
        {
            if (_busy || _granted != null) return;
            ClearMessage();
        }

        /// <summary>Devuelve el mensaje para el jugador, o nulo si el nombre es válido. Misma regla que PlayersApi: ^[A-Za-z0-9_-]{3,20}$.</summary>
        public static string Validate(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < MinNameLength || name.Length > MaxNameLength) return MsgLength;
            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok) return MsgCharacters;
            }
            return null;
        }

        public void Submit()
        {
            if (_busy) return;

            if (_granted != null)
            {
                SaveGranted();
                return;
            }

            string name = (_name.value ?? "").Trim();
            string invalid = Validate(name);
            if (invalid != null)
            {
                ShowMessage(invalid, false);
                return;
            }

            ClearMessage();
            SetBusy(true);
            _context.Run(Register(name));
        }

        private IEnumerator Register(string name)
        {
            ApiResult<PlayerCreated> result = null;
            yield return _context.Api.CreatePlayer(name, r => result = r);
            SetBusy(false);

            if (result == null || result.Status == ApiStatus.ConnectionFailed)
            {
                // Nada se borra por no poder conectar: solo se avisa, y "Jugar sin conexión" sigue disponible.
                ShowMessage(MsgOffline, true);
            }
            else if (result.Status == ApiStatus.Rejected)
            {
                if (result.HttpStatus == 409) ShowMessage(MsgTaken, false);
                else if (result.HttpStatus == 400) ShowMessage(MsgCharacters, false);
                else ShowMessage(MsgServer, true);
            }
            else if (result.Value == null || string.IsNullOrEmpty(result.Value.key))
            {
                // Sin clave no hay identidad, aunque la respuesta parezca correcta.
                ShowMessage(MsgServer, true);
            }
            else
            {
                PlayerCreated created = result.Value;
                _granted = new Profile
                {
                    playerName = string.IsNullOrEmpty(created.name) ? name : created.name,
                    playerKey = created.key,
                    playerId = created.playerId
                };
                SaveGranted();
            }
        }

        private void SaveGranted()
        {
            try
            {
                _context.Store.SaveProfile(_granted);
            }
            catch (IOException) { ShowMessage(MsgCannotSave, true); return; }
            catch (UnauthorizedAccessException) { ShowMessage(MsgCannotSave, true); return; }

            // Sin identidad previa, los ids de servidor que queden en los packs son de otro servidor o de otra base: se olvidan.
            if (!_context.HasIdentity) _context.Packs.ForgetPackServerIds();
            _context.Profile = _granted;
            _granted = null;
            ClearMessage();
            if (_loggedIn != null) _loggedIn();
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Submit();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _enter.SetEnabled(!busy);
            _retry.SetEnabled(!busy);
            _name.SetEnabled(!busy);
            _offline.SetEnabled(!busy);
            _enter.text = busy ? TextBusy : TextEnter;
        }

        private void ShowMessage(string text, bool canRetry)
        {
            _message.text = text;
            _message.style.display = DisplayStyle.Flex;
            _retry.style.display = canRetry ? DisplayStyle.Flex : DisplayStyle.None;
            _enter.style.display = canRetry ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ClearMessage()
        {
            _message.text = "";
            _message.style.display = DisplayStyle.None;
            _retry.style.display = DisplayStyle.None;
            _enter.style.display = DisplayStyle.Flex;
        }

        private static T Require<T>(VisualElement view, string name) where T : VisualElement
        {
            T element = view.Q<T>(name);
            if (element == null) throw new InvalidOperationException("Falta el elemento '" + name + "' en la pantalla de acceso.");
            return element;
        }
    }
}
