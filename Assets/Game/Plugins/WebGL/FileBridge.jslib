// Puente de archivos para WebGL. Estrategia de retorno a C#: SendMessage hacia un GameObject con nombre
// fijo (RobotBomberoFileBridge), con los métodos OnFilePicked / OnFileError. SendMessage ya reserva,
// copia (UTF-8) y libera la memoria del string; aquí no se hace ningún _malloc propio.
var RobotBomberoFileBridge = {
  // fileNamePtr y contentPtr son char* UTF-8; UTF8ToString los copia a strings de JS al instante.
  RB_DownloadFile: function (fileNamePtr, contentPtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var content = UTF8ToString(contentPtr);
    var blob = new Blob([content], { type: 'application/json;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  },

  // "Arma" el selector: NO lo abre. Unity procesa los eventos de puntero en un frame posterior, fuera del
  // gesto del usuario, y navegadores como Safari bloquearían input.click() desde aquí. Se registra un
  // listener de un solo uso (pointerup, con touchend de respaldo) en document que llama a input.click()
  // dentro del evento real del navegador. C# debe llamar a PickJson en el pointer-down del botón, para
  // que el pointer-up del mismo toque abra el selector.
  RB_PickJson: function (maxBytes, receiverNamePtr) {
    var receiver = UTF8ToString(receiverNamePtr);

    // Iniciar una selección nueva sustituye a la pendiente: se quitan sus listeners y su input.
    if (window.__rbPendingPick) {
      window.__rbPendingPick.dispose();
      window.__rbPendingPick = null;
    }

    var input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json,application/json';
    input.style.display = 'none';

    function fail(message) {
      SendMessage(receiver, 'OnFileError', message);
    }
    function removeInput() {
      if (input.parentNode) input.parentNode.removeChild(input);
    }
    function removeListeners() {
      document.removeEventListener('pointerup', onRelease, true);
      document.removeEventListener('touchend', onRelease, true);
    }
    function onRelease() {
      removeListeners();           // un solo uso: el primero de pointerup / touchend gana
      document.body.appendChild(input);
      input.click();               // dentro del evento real del usuario
    }

    input.addEventListener('change', function () {
      var file = input.files && input.files[0];
      removeInput();
      window.__rbPendingPick = null;
      if (!file) { fail('cancelled'); return; }
      // Se comprueba el tamaño ANTES de leer el archivo a memoria.
      if (file.size > maxBytes) { fail('too_large'); return; }
      var reader = new FileReader();
      reader.onload = function () { SendMessage(receiver, 'OnFilePicked', String(reader.result)); };
      reader.onerror = function () { fail('read_error'); };
      reader.readAsText(file, 'utf-8');
    });
    // Si el usuario cierra el diálogo sin elegir (navegadores recientes lo avisan con 'cancel').
    input.addEventListener('cancel', function () { removeInput(); window.__rbPendingPick = null; fail('cancelled'); });

    window.__rbPendingPick = {
      dispose: function () { removeListeners(); removeInput(); }
    };
    document.addEventListener('pointerup', onRelease, true);
    document.addEventListener('touchend', onRelease, true);
  }
};

mergeInto(LibraryManager.library, RobotBomberoFileBridge);
