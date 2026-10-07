using iiMenu.CanvasMenuUI.Main;
using UnityEngine;

namespace iiMenu.CanvasMenuUI.Settings
{
    public sealed class MenuButton
    {
        public GameObject go;
        public int tab = -1;
        public bool isTab;
        public int tabIdx;
        public System.Action onPress;
        public System.Func<bool> isOn;
        public Renderer pillRend;
        public Renderer rowBgRend;
        public TextMesh statusTM;
        public int navTargetCategory = int.MinValue;
        public GameObject stripGO;
        public TextMesh iconTM, nameTM;
        public bool neutralToggleRow;
        public bool suppressToggleStatus;
        public bool isSearchUi;
    }

    public sealed class MenuButtonCollider : MonoBehaviour
    {
        MenuButton _b;
        iiMenu.CanvasMenuUI.Main.Menu _m;

        public void Set(MenuButton b, iiMenu.CanvasMenuUI.Main.Menu m)
        {
            _b = b;
            _m = m;
        }

        void OnTriggerEnter(Collider o) { }
    }

    public sealed class MenuTabVisibility : MonoBehaviour
    {
        iiMenu.CanvasMenuUI.Main.Menu _m;
        int _t;

        public void Init(iiMenu.CanvasMenuUI.Main.Menu m, int t)
        {
            _m = m;
            _t = t;
        }

        void Update()
        {
            if (_m != null)
                gameObject.SetActive(_m.ActiveTab == _t);
        }
    }

    public sealed class MenuThemeLabel : MonoBehaviour
    {
        iiMenu.CanvasMenuUI.Main.Menu _m;
        TextMesh _tm;
        int _last = -1;

        public void Set(iiMenu.CanvasMenuUI.Main.Menu m) => _m = m;

        void Awake()
        {
            _tm = gameObject.AddComponent<TextMesh>();
            _tm.fontSize = 200;
            _tm.characterSize = 0.00055f;
            _tm.anchor = TextAnchor.MiddleCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.color = new Color(0.50f, 0.50f, 0.50f, 1f);
        }

        void Start()
        {
            if (_m?._font != null)
            {
                _tm.font = _m._font;
                _tm.GetComponent<MeshRenderer>().material = _m._font.material;
            }
            _tm.GetComponent<MeshRenderer>().sortingOrder = 30;
        }

        void Update()
        {
            if (_m?.plugin == null) return;
            int i = _m.plugin.selectedThemeIndex;
            if (i == _last) return;
            _last = i;
            _tm.text = "Theme: " + (i >= 0 && i < Plugin.colourPresets.Length
                ? Plugin.colourPresets[i].Name : "?");
        }
    }

    public sealed class MenuAnalyticsLabel : MonoBehaviour
    {
        iiMenu.CanvasMenuUI.Main.Menu _m;
        TextMesh _tm;
        float _t;

        public void Set(iiMenu.CanvasMenuUI.Main.Menu m) => _m = m;

        void Awake()
        {
            _tm = gameObject.AddComponent<TextMesh>();
            _tm.fontSize = 200;
            _tm.characterSize = 0.00055f;
            _tm.anchor = TextAnchor.UpperCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.color = new Color(0.70f, 0.70f, 0.70f, 1f);
        }

        void Start()
        {
            if (_m?._font != null)
            {
                _tm.font = _m._font;
                _tm.GetComponent<MeshRenderer>().material = _m._font.material;
            }
            _tm.GetComponent<MeshRenderer>().sortingOrder = 30;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t < 0.5f) return;
            _t = 0f;
            if (_m?.plugin == null) return;
            _tm.text = _m.plugin.GetAnalyticsSummary_WM();
        }
    }

    public sealed class MenuStatusLabel : MonoBehaviour
    {
        iiMenu.CanvasMenuUI.Main.Menu _m;
        TextMesh _tm;

        public void Set(iiMenu.CanvasMenuUI.Main.Menu m) => _m = m;

        void Awake()
        {
            _tm = gameObject.AddComponent<TextMesh>();
            _tm.fontSize = 200;
            _tm.characterSize = 0.00060f;
            _tm.anchor = TextAnchor.MiddleCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.color = new Color(0.70f, 0.70f, 0.70f, 1f);
        }

        void Start()
        {
            if (_m?._font != null)
            {
                _tm.font = _m._font;
                _tm.GetComponent<MeshRenderer>().material = _m._font.material;
            }
            _tm.GetComponent<MeshRenderer>().sortingOrder = 30;
        }

        void Update()
        {
            if (_tm == null || _m?.plugin == null) return;
            _tm.text = _m.plugin.statusMessage;
            _tm.color = _m.plugin.statusColor;
        }
    }
}
