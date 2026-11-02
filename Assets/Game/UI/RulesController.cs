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
        private readonly Button _import;
        private readonly Label _filesNote;

        public RulesController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _view = view;
            _context = context;
            _go = go;
            _list = UiKit.Require<VisualElement>(view, "rules-list");
            _empty = UiKit.Require<VisualElement>(view, "rules-empty");
            _message = UiKit.Plain(UiKit.Require<Label>(view, "rules-message"));

            UiKit.Require<Button>(view, "rules-back").clicked += () => _go(ScreenId.Menu);
            UiKit.Require<Button>(view, "rules-new").clicked += NewPack;
            _import = UiKit.Require<Button>(view, "rules-import");
            _filesNote = UiKit.Require<Label>(view, "rules-files-note");
            // Sin selector de archivos no se deja ningún botón que no haga nada.
            UiKit.Show(_import, context.Files.IsAvailable);
            UiKit.Show(_filesNote, !context.Files.IsAvailable);
            _filesNote.text = context.Files.IsAvailable ? "" : "Importar y exportar packs: Próximamente en esta versión.";
            var emptyLabel = _empty as Label;
            if (emptyLabel != null && !context.Files.IsAvailable) emptyLabel.text = "Todavía no tienes ningún pack. Crea uno nuevo.";
            if (context.Files.PickNeedsPointerDown)
            {
                // Solo WebGL: el selector del navegador solo se abre dentro de un gesto del usuario, así que se arma al pulsar,
                // no al soltar (ver FileBridge.PickJson).
                // Con TrickleDown: Clickable detiene la propagación del PointerDown del propio botón, y una devolución en la fase normal no llegaría a ejecutarse.
                _import.RegisterCallback<PointerDownEvent>(OnImportPointerDown, TrickleDown.TrickleDown);
            }
            else
            {
                // Apps nativas: un clic normal.
                _import.clicked += BeginImport;
            }
        }

        public void OnShow()
        {
            UiKit.Show(_message, false);
            Refresh();
            _context.Run(_context.Packs.SyncPending());
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
            if (_context.Files.IsAvailable) buttons.Add(UiKit.Button("pack-export-" + i, "Exportar", () => Export(pack), "btn--small"));
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
                    _context.Run(_context.Packs.SyncPending());
                }
                return error;
            });
        }

        private void Delete(LocalPack pack)
        {
            string message = "¿Eliminar «" + pack.content.name + "»? Se perderán sus reglas y estadísticas de este dispositivo. No se puede deshacer.";
            Dialog.Confirm(_view, "Eliminar pack", message, "Eliminar", () =>
            {
                _context.Packs.Delete(pack);
                Refresh();
                _context.Run(_context.Packs.SyncPending());
            });
        }

        private void Export(LocalPack pack)
        {
            string fileName = PackFiles.ExportFileName(pack.content.name);
            _context.Files.Download(fileName, PackFiles.Export(pack),
                () => Info("Pack exportado como " + fileName + "."),
                code =>
                {
                    // Cancelar (o denegar) el guardado no es un error.
                    if (code == FileBridge.ErrorCancelled) return;
                    Problem("No se pudo exportar el pack en este dispositivo.");
                });
        }

        private void OnImportPointerDown(PointerDownEvent e)
        {
            if (e.button == 0) BeginImport();
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
    }
}
