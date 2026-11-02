using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Punto de entrada de Main.unity: crea los servicios y muestra la primera pantalla.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameApp : MonoBehaviour
    {
        [SerializeField] private UiAssets assets = new UiAssets();

        public GameContext Context { get; private set; }
        public ScreenRouter Router { get; private set; }

        private void Start()
        {
            string dataRoot = Application.persistentDataPath;
            string apiUrl = "";
#if DEVELOPMENT_BUILD || UNITY_EDITOR || RB_CAPTURE
            dataRoot = DevFlags.Get("-rb-data") ?? dataRoot;
            apiUrl = DevFlags.Get("-rb-api") ?? apiUrl;
#endif
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            IApiClient api = new ApiClient(apiUrl);
#if DEVELOPMENT_BUILD || UNITY_EDITOR || RB_CAPTURE
            api = DevSamples.DemoApi(apiUrl) ?? api;
#endif
            Context = new GameContext(new LocalStore(dataRoot), api, routine => StartCoroutine(routine));
            Router = AppScreens.Compose(root, assets, Context);
            Router.ShowStart();
            // Con una identidad válida, se ponen al día los borrados y cambios que no llegaron al servidor.
            Context.Run(Context.Packs.SyncPending());
#if DEVELOPMENT_BUILD || UNITY_EDITOR || RB_CAPTURE
            StartCoroutine(DevFlags.Apply(root, Context, Router));
#endif
        }
    }
}
