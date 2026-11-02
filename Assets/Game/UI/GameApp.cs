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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            dataRoot = DevFlags.Get("-rb-data") ?? dataRoot;
            apiUrl = DevFlags.Get("-rb-api") ?? apiUrl;
#endif
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            Context = new GameContext(new LocalStore(dataRoot), new ApiClient(apiUrl), routine => StartCoroutine(routine));
            Router = AppScreens.Compose(root, assets, Context);
            Router.ShowStart();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            StartCoroutine(DevFlags.Apply(root, Context, Router));
#endif
        }
    }
}
