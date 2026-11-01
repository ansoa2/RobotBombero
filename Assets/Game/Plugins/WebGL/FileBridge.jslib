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

  // Debe llamarse desde un gesto del usuario (clic) para que el navegador permita abrir el selector.
  RB_PickJson: function (maxBytes, receiverNamePtr) {
    var receiver = UTF8ToString(receiverNamePtr);
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json,application/json';
    input.style.display = 'none';

    function fail(message) {
      SendMessage(receiver, 'OnFileError', message);
    }
    function cleanup() {
      if (input.parentNode) input.parentNode.removeChild(input);
    }

    input.addEventListener('change', function () {
      var file = input.files && input.files[0];
      cleanup();
      if (!file) { fail('cancelled'); return; }
      // Se comprueba el tamaño ANTES de leer el archivo a memoria.
      if (file.size > maxBytes) { fail('too_large'); return; }
      var reader = new FileReader();
      reader.onload = function () { SendMessage(receiver, 'OnFilePicked', String(reader.result)); };
      reader.onerror = function () { fail('read_error'); };
      reader.readAsText(file, 'utf-8');
    });
    // Si el usuario cierra el diálogo sin elegir (navegadores recientes lo avisan con 'cancel').
    input.addEventListener('cancel', function () { cleanup(); fail('cancelled'); });

    document.body.appendChild(input);
    input.click();
  }
};

mergeInto(LibraryManager.library, RobotBomberoFileBridge);
