using System;
using RobotBombero.Simulation;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Lista de packs: crear, renombrar, eliminar con confirmación, exportar, importar y abrir el editor.</summary>
    public sealed class RulesController
    {
        private readonly VisualElement _view;
        private readonly GameContext _context;
        private readonly Action<ScreenId> _go;
        private readonly VisualElement _list;
        private readonly VisualElement _empty;
        private readonly Label _message;

        public RulesController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _view = view;
            _context = context;
            _go = go;
            _list = UiKit.Require<VisualElement>(view, "rules-list");
            _empty = UiKit.Require<VisualElement>(view, "rules-empty");
            _message = UiKit.Require<Label>(view, "rules-message");

            UiKit.Require<Button>(view, "rules-back").clicked += () => _go(ScreenId.Menu);
            UiKit.Require<Button>(view, "rules-new").clicked += NewPack;
            // El selector de archivos de WebGL solo se abre dentro de un gesto del usuario: se arma al pulsar,
            // no al soltar (ver FileBridge.PickJson).
            UiKit.Require<Button>(view, "rules-import").RegisterCallback<PointerDownEvent>(e => BeginImport());
        }

        public void OnShow()
        {
            UiKit.Show(_message, false);
            Refresh();
        }

        private void Refresh()
        {
            _list.Clear();
            var packs = _context.Packs.All;
            UiKit.Show(_empty, packs.Count == 0);
            for (int i = 0; i < packs.Count; i++) _list.Add(Card(packs[i], i));
        }

        private VisualElement Card(LocalPack pack, int i)
        {
            VisualElement card = UiKit.Box("card");
            card.Add(UiKit.Label(null, pack.content.name, "card__title"));
            int games = pack.stats == null ? 0 : pack.stats.games;
            string rules = pack.content.rules.Length == 1 ? "1 regla" : pack.content.rules.Length + " reglas";
            card.Add(UiKit.Label(null, rules + " · " + (games == 1 ? "1 partida" : games + " partidas"), "text-muted"));

            VisualElement buttons = UiKit.Box("card__buttons");
            buttons.Add(UiKit.Button("pack-edit-" + i, "Editar", () => { _context.EditingPack = pack; _go(ScreenId.RuleEditor); }, "btn--small", "btn--main"));
            buttons.Add(UiKit.Button("pack-rename-" + i, "Renombrar", () => Rename(pack), "btn--small"));
            buttons.Add(UiKit.Button("pack-export-" + i, "Exportar", () => Export(pack), "btn--small"));
            buttons.Add(UiKit.Button("pack-delete-" + i, "Eliminar", () => Delete(pack), "btn--small", "btn--danger"));
            card.Add(buttons);
            return card;
        }

        private void NewPack()
        {
            Dialog.Prompt(_view, "Nuevo pack", "", PackLimits.MaxNameLength, "Crear", text =>
            {
                LocalPack created;
                string error = _context.Packs.Create(text, out created);
                if (error == null) { Info("Pack «" + created.content.name + "» creado."); Refresh(); }
                return error;
            });
        }

        private void Rename(LocalPack pack)
        {
            Dialog.Prompt(_view, "Renombrar pack", pack.content.name, PackLimits.MaxNameLength, "Guardar", text =>
            {
                string error = _context.Packs.Rename(pack, text);
                if (error == null)
                {
                    Refresh();
                    _context.Run(_context.Packs.Sync(pack));
                }
                return error;
            });
        }

        private void Delete(LocalPack pack)
        {
            string message = "¿Eliminar «" + pack.content.name + "»? Se perderán sus reglas y estadísticas de este dispositivo. No se puede deshacer.";
            Dialog.Confirm(_view, "Eliminar pack", message, "Eliminar", () =>
            {
                long serverId = pack.serverPackId;
                _context.Packs.Delete(pack);
                Refresh();
                if (serverId > 0) _context.Run(_context.Packs.DeleteRemote(serverId));
            });
        }

        private void Export(LocalPack pack)
        {
            string fileName = FileName(pack.content.name) + ".json";
            if (_context.Files.Download(fileName, PackFiles.Export(pack))) Info("Pack exportado como " + fileName + ".");
            else Problem("No se pudo exportar el pack en este dispositivo.");
        }

        /// <summary>Se llama al pulsar (no al soltar) el botón Importar.</summary>
        public void BeginImport()
        {
            _context.Files.PickJson(OnImported, OnImportError);
        }

        private void OnImported(string json)
        {
            ImportResult result = _context.Packs.Import(json);
            if (!result.Success) { Problem(result.Error); return; }
            Info("Pack «" + result.Pack.content.name + "» importado.");
            Refresh();
        }

        private void OnImportError(string code)
        {
            if (code == FileBridge.ErrorCancelled) return;
            if (code == FileBridge.ErrorTooLarge) Problem("El archivo es demasiado grande: el máximo son " + PackLimits.MaxFileBytes / 1024 + " KB.");
            else if (code == FileBridge.ErrorUnavailable) Problem("Importar archivos no está disponible en este dispositivo.");
            else Problem("No se pudo leer el archivo.");
        }

        private void Info(string text)
        {
            _message.text = text;
            _message.RemoveFromClassList("is-error");
            UiKit.Show(_message, true);
        }

        private void Problem(string text)
        {
            _message.text = text;
            _message.AddToClassList("is-error");
            UiKit.Show(_message, true);
        }

        private static string FileName(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                bool ok = char.IsLetterOrDigit(chars[i]) || chars[i] == '-' || chars[i] == '_' || chars[i] == ' ';
                if (!ok) chars[i] = '_';
            }
            return new string(chars).Trim();
        }
    }
}
