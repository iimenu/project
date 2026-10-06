using System;
using System.Collections.Generic;
using iiMenu.CanvasMenuUI.Classes;
using iiMenu.CanvasMenuUI.Settings;
using UnityEngine;
using UnityEngine.XR;
using iiMenu.CanvasMenuUI.Input;

namespace iiMenu.CanvasMenuUI.Main
{
    public sealed class Menu : MonoBehaviour
    {
        public static Menu Instance { get; private set; }

        public static int _currentCategory;
        public static int currentCategory
        {
            get => _currentCategory;
            set
            {
                _currentCategory = value;
                Instance?.RebuildModsCategory();
                Instance?.RebuildSidebar();
            }
        }

        public static bool rightHanded = true;
        public static bool disableNotifications;
        public static bool fpsCounter;
        public static bool disconnectButton;

        public Plugin plugin;
        public bool IsOpen { get; private set; }
        public Font _font;
        Font _sidebarIconFont;

        float _btnCd = 0f;
        bool _spawned = false;
        Vector3 _spawnPos;

        const float W = 0.93f;
        const float H = 0.54f;
        const float D = 0.010f;
        const float DIST = 0.55f;
        const float VOFF = 0.00f;
        const float TBH = 0.054f;
        const float RH = 0.062f;
        const float RG = 0.005f;

        const float TS_TITLE = 0.0018f; const float TS_SUB = 0.0010f;
        const float TS_ROW = 0.0015f; const float TS_DESC = 0.0009f;
        const float TS_BTN = 0.0011f;

        struct ThemePreset
        {
            public string name;
            public Color bg, sb, bar, row, acc, txt, sub, on_c, off_c, red, grn, dark;
            public ThemePreset(string n,
                Color bg, Color sb, Color bar, Color row, Color acc, Color txt,
                Color sub, Color on_c, Color off_c, Color red, Color grn, Color dark)
            {
                this.name = n; this.bg = bg; this.sb = sb; this.bar = bar; this.row = row;
                this.acc = acc; this.txt = txt; this.sub = sub; this.on_c = on_c;
                this.off_c = off_c; this.red = red; this.grn = grn; this.dark = dark;
            }
        }

        static readonly ThemePreset[] THEMES = new ThemePreset[]
        {
            new ThemePreset("iiMenu.CanvasMenuUI",
                new Color(0.03f,0.04f,0.09f,1), new Color(0.02f,0.03f,0.07f,1),
                new Color(0.05f,0.06f,0.12f,1), new Color(0.10f,0.11f,0.18f,1),
                new Color(0.62f,0.72f,0.96f,1), new Color(0.93f,0.94f,0.98f,1),
                new Color(0.45f,0.48f,0.60f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.12f,0.14f,0.22f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(0.20f,0.85f,0.55f,1), new Color(0.01f,0.02f,0.05f,1)),
            new ThemePreset("Dark",
                new Color(0.08f,0.08f,0.09f,1), new Color(0.05f,0.05f,0.06f,1),
                new Color(0.09f,0.09f,0.11f,1), new Color(0.12f,0.12f,0.14f,1),
                new Color(0.26f,0.52f,0.98f,1), new Color(0.95f,0.95f,0.97f,1),
                new Color(0.42f,0.44f,0.52f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.13f,0.15f,0.19f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(0.14f,0.68f,0.38f,1), new Color(0.02f,0.02f,0.02f,1)),
            new ThemePreset("Purple",
                new Color(0.08f,0.05f,0.12f,1), new Color(0.06f,0.03f,0.10f,1),
                new Color(0.12f,0.07f,0.18f,1), new Color(0.16f,0.10f,0.22f,1),
                new Color(0.72f,0.40f,1.00f,1), new Color(0.96f,0.94f,0.99f,1),
                new Color(0.52f,0.42f,0.62f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.18f,0.12f,0.26f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(0.14f,0.68f,0.38f,1), new Color(0.02f,0.01f,0.03f,1)),
            new ThemePreset("Blue",
                new Color(0.04f,0.07f,0.14f,1), new Color(0.03f,0.05f,0.11f,1),
                new Color(0.06f,0.10f,0.18f,1), new Color(0.09f,0.13f,0.22f,1),
                new Color(0.20f,0.60f,1.00f,1), new Color(0.92f,0.96f,1.00f,1),
                new Color(0.38f,0.48f,0.62f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.08f,0.12f,0.22f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(0.14f,0.68f,0.38f,1), new Color(0.01f,0.02f,0.04f,1)),
            new ThemePreset("Green",
                new Color(0.04f,0.10f,0.06f,1), new Color(0.03f,0.07f,0.04f,1),
                new Color(0.06f,0.14f,0.08f,1), new Color(0.09f,0.18f,0.11f,1),
                new Color(0.20f,0.90f,0.40f,1), new Color(0.90f,0.98f,0.92f,1),
                new Color(0.38f,0.56f,0.42f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.06f,0.16f,0.08f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(0.20f,0.90f,0.40f,1), new Color(0.01f,0.03f,0.01f,1)),
            new ThemePreset("Red",
                new Color(0.12f,0.04f,0.04f,1), new Color(0.09f,0.03f,0.03f,1),
                new Color(0.16f,0.06f,0.06f,1), new Color(0.20f,0.08f,0.08f,1),
                new Color(1.00f,0.28f,0.28f,1), new Color(1.00f,0.94f,0.94f,1),
                new Color(0.60f,0.40f,0.40f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.22f,0.08f,0.08f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(0.14f,0.68f,0.38f,1), new Color(0.03f,0.01f,0.01f,1)),
            new ThemePreset("Teal",
                new Color(0.04f,0.10f,0.10f,1), new Color(0.03f,0.07f,0.07f,1),
                new Color(0.06f,0.14f,0.14f,1), new Color(0.09f,0.18f,0.18f,1),
                new Color(0.10f,0.88f,0.82f,1), new Color(0.90f,0.98f,0.98f,1),
                new Color(0.36f,0.54f,0.54f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.06f,0.16f,0.16f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(0.10f,0.88f,0.82f,1), new Color(0.01f,0.03f,0.03f,1)),
            new ThemePreset("Orange",
                new Color(0.12f,0.07f,0.02f,1), new Color(0.09f,0.05f,0.01f,1),
                new Color(0.16f,0.10f,0.03f,1), new Color(0.20f,0.13f,0.04f,1),
                new Color(1.00f,0.55f,0.10f,1), new Color(1.00f,0.96f,0.88f,1),
                new Color(0.60f,0.48f,0.32f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.22f,0.14f,0.04f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(1.00f,0.55f,0.10f,1), new Color(0.03f,0.02f,0.01f,1)),
            new ThemePreset("Pink",
                new Color(0.12f,0.05f,0.10f,1), new Color(0.09f,0.04f,0.08f,1),
                new Color(0.16f,0.07f,0.14f,1), new Color(0.20f,0.09f,0.18f,1),
                new Color(1.00f,0.42f,0.72f,1), new Color(1.00f,0.92f,0.96f,1),
                new Color(0.62f,0.40f,0.54f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.22f,0.08f,0.18f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(1.00f,0.42f,0.72f,1), new Color(0.03f,0.01f,0.02f,1)),
            new ThemePreset("Void",
                new Color(0.04f,0.04f,0.06f,1), new Color(0.02f,0.02f,0.04f,1),
                new Color(0.06f,0.06f,0.09f,1), new Color(0.08f,0.08f,0.12f,1),
                new Color(0.55f,0.55f,0.70f,1), new Color(0.88f,0.88f,0.94f,1),
                new Color(0.34f,0.34f,0.44f,1), new Color(0.14f,0.68f,0.38f,1),
                new Color(0.10f,0.10f,0.16f,1), new Color(0.82f,0.22f,0.26f,1),
                new Color(0.14f,0.68f,0.38f,1), new Color(0.01f,0.01f,0.02f,1)),
        };

        int _themeIdx = 0;

        Material _mBG, _mSB, _mBAR, _mROW, _mACC, _mOn, _mOff, _mRed, _mGrn, _mDark, _mPAGER;

        readonly Dictionary<MenuButton, float> _rowSlideT = new Dictionary<MenuButton, float>();
        readonly Dictionary<MenuButton, Vector3> _rowBasePos = new Dictionary<MenuButton, Vector3>();
        const float RowSlideOffset = 0.10f;
        const float RowSlideSpeed = 10f;
        const float RowSlideCascade = 0.06f;
        const float MenuLaserOpenThreshold = 0.02f;
        const float XrPressThreshold = 0.75f;
        const float SidebarWidth = 0.152f;
        const float SidebarRowH = 0.054f;
        const float SidebarRowGap = 0.0035f;
        const int SidebarPageSize = 6;
        const int ModsPageSize = 6;
        const float ModsPagerHeight = 0.040f;

        float _springVel = 0f;
        float _springPos = 0f;
        const float SpringStiffness = 280f;
        const float SpringDamping = 22f;

        readonly Dictionary<MenuButton, float> _pressFlashT = new Dictionary<MenuButton, float>();

        AudioSource _audio;

        readonly List<MenuButton> _btns = new List<MenuButton>();
        readonly List<MenuButton> _modDynamicBtns = new List<MenuButton>();
        readonly Dictionary<int, bool> _modToggleState = new Dictionary<int, bool>();

        Transform _modsContentRoot;
        Transform _sidebarListRoot;
        Transform _sidebarPagerRoot;
        readonly List<MenuButton> _sidebarNavBtns = new List<MenuButton>();
        MenuButton _sidebarPrev;
        MenuButton _sidebarNext;
        int _sidebarPage;
        int _sidebarPageCount = 1;
        int _modsPage;
        static bool pagerLogged;
        readonly List<MenuButton> _modsPagerBtns = new List<MenuButton>();
        Transform _modsPagerRoot;
        int _modsPageCount = 1;
        TextMesh _modsPageLabel;
        float _sidebarNavInnerW;
        Renderer _logoRend;

        GameObject _root;
        Collider _lH, _rH;

        LineRenderer _laserL;
        LineRenderer _laserR;
        Transform _lHandTx;
        Transform _rHandTx;
        GameObject _dotL;
        GameObject _dotR;
        Material _laserMat;
        Material _dotMat;

        Quaternion _targetRot;

        float _menuDisplayScale;

        MenuButton _laserHoveredL;
        MenuButton _laserHoveredR;
        bool _laserResolveDebugOnce;
        bool _prevTrigL;
        bool _prevTrigR;
        float _prevGorillaL;
        float _prevGorillaR;

        GameObject _homePanelRoot;

        GameObject _disconnectTopRootMenu;

        void Awake()
        {
            Instance = this;
            BuildMats();
            BuildMenu();
            if (_root) _root.SetActive(false);
            _targetRot = Quaternion.identity;
            _springPos = 0f;
            _springVel = 0f;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = 0.5f;
        }
        void Start()
        {
            TryHands();
            BuildLasers();
            EnsureSidebarIconFont();
            if (LogoCache.TryGetCached(out Texture2D warmLogo))
                ApplyLogoTexture(warmLogo);
            StartCoroutine(LogoCache.EnsureTexture(t => ApplyLogoTexture(t)));
            if (plugin != null)
                StartCoroutine(DiscordAvatarCache.FetchToCache(plugin));
        }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_root) Destroy(_root);
            if (_laserL) Destroy(_laserL.gameObject);
            if (_laserR) Destroy(_laserR.gameObject);
            if (_dotL) Destroy(_dotL);
            if (_dotR) Destroy(_dotR);
        }

        void Update()
        {

            float dt = Time.deltaTime;

            float springTarget = IsOpen ? 1f : 0f;
            float springForce = (springTarget - _springPos) * SpringStiffness
                                - _springVel * SpringDamping;
            _springVel += springForce * dt;
            _springPos = Mathf.Clamp01(_springPos + _springVel * dt);

            float displayScale = _springPos < 0.001f ? 0f : _springPos;
            _menuDisplayScale = displayScale;

            if (_root != null)
            {
                bool vis = displayScale > 0.005f;
                if (_root.activeSelf != vis) _root.SetActive(vis);
                if (vis)
                {
                    float sx = Mathf.Clamp01(displayScale * 1.05f);
                    float sy = Mathf.Clamp01(displayScale * 0.95f);
                    _root.transform.localScale = new Vector3(sx, sy, displayScale);
                }
            }

            if (displayScale < 0.005f)
            {
                HideLasers();
                if (_disconnectTopRootMenu != null) _disconnectTopRootMenu.SetActive(false);
                return;
            }

            if (_disconnectTopRootMenu != null)
                _disconnectTopRootMenu.SetActive(disconnectButton);

            var cam = iiMenu.Menu.Main.TPC != null ? iiMenu.Menu.Main.TPC : Camera.main;
            if (cam != null && _root != null && !_spawned)
            {
                Vector3 flatFwd = new Vector3(
                    cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;
                if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;

                _spawnPos = cam.transform.position + flatFwd * DIST + Vector3.up * VOFF;
                Vector3 toPlayer = cam.transform.position - _spawnPos;
                toPlayer.y = 0f;

                _root.transform.position = _spawnPos;
                if (toPlayer.sqrMagnitude > 0.001f)
                    _root.transform.rotation = Quaternion.LookRotation(
                        -toPlayer.normalized, Vector3.up);

                if (!UnityEngine.XR.XRSettings.isDeviceActive)
                    _root.transform.SetParent(cam.transform, true);
                else
                    _root.transform.SetParent(null, true);

                Physics.SyncTransforms();
                _spawned = true;
            }

            try { UpdateRowSlides(dt); }
            catch (Exception e) { Debug.LogError("[ii Reborn] UpdateRowSlides: " + e); }

            try { UpdatePressFlash(dt); }
            catch (Exception e) { Debug.LogError("[ii Reborn] UpdatePressFlash: " + e); }

            try { TryHands(); }
            catch (Exception e) { Debug.LogError("[ii Reborn] TryHands: " + e); }

            var ms = UnityEngine.InputSystem.Mouse.current;
            if (ms != null && ms.leftButton.wasPressedThisFrame) Click();

            try { UpdateLaser(_lHandTx, _laserL, _dotL, true); }
            catch (Exception e) { Debug.LogError("[ii Reborn] UpdateLaserL: " + e); }

            try { UpdateLaser(_rHandTx, _laserR, _dotR, false); }
            catch (Exception e) { Debug.LogError("[ii Reborn] UpdateLaserR: " + e); }

            try { CheckControllerTrigger(); }
            catch (Exception e) { Debug.LogError("[ii Reborn] CheckControllerTrigger: " + e); }

            try { RefreshToggles(); }
            catch (Exception e) { Debug.LogError("[ii Reborn] RefreshToggles: " + e); }
        }

        void LateUpdate()
        {
            Buttons.ExecuteEnabledMethods();
        }

        public void SetOpen(bool open)
        {

            bool wasOpen = IsOpen;
            IsOpen = open;

            if (open)
            {
                _spawned = false;
                _modsPage = 0;
                RebuildModsCategory();
                RebuildSidebar();
                PlaySound(SoundId.MenuOpen);
                KickRowSlide();
            }
            else if (wasOpen)
            {
                PlaySound(SoundId.MenuClose);
            }
        }
        public bool IsHandCollider(Collider c) => c != null && (c == _lH || c == _rH);

        public int ActiveTab => 0;

        public void Press(MenuButton btn)
        {
            if (Time.time < _btnCd) return;
            _btnCd = Time.time + 0.18f;

            _pressFlashT[btn] = 1f;

            PlaySound(btn.isOn != null ? SoundId.ToggleClick : SoundId.ButtonClick);
            btn.onPress?.Invoke();

            if (iiMenu.Menu.Main.isSearching && !btn.isSearchUi)
                iiMenu.Menu.Main.Toggle("Search", true);
        }

        public void SelectThemeByIndex(int idx)
        {
            if (idx < 0 || idx >= THEMES.Length) return;
            SelectTheme(idx);
        }

        void ApplyLogoTexture(Texture2D tex)
        {
            if (tex == null || _logoRend == null) return;
            Shader sh = Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Texture")
                ?? _logoRend.material.shader;
            Material m = new Material(sh);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            else m.mainTexture = tex;
            if (m.HasProperty("_Color")) m.color = Color.white;
            _logoRend.material = m;
        }

        void ApplySidebarLogoTexture(Renderer iconR, Texture2D tex)
        {
            if (iconR == null || tex == null) return;
            Shader sh = Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Texture")
                ?? iconR.material.shader;
            var m = new Material(sh);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            else m.mainTexture = tex;
            if (m.HasProperty("_Color")) m.color = Color.white;
            iconR.material = m;
        }

        /// <summary>Font Awesome 6 Free Solid (OS install) or Resources/iiMenu.CanvasMenuUIIconFont.ttf (user-supplied, OFL).</summary>
        Font EnsureSidebarIconFont()
        {
            if (_sidebarIconFont != null) return _sidebarIconFont;
            string[] tryNames =
            {
                "Font Awesome 6 Free Solid",
                "Font Awesome 6 Free",
                "Font Awesome 5 Free Solid",
                "Font Awesome 5 Free",
            };
            foreach (var n in tryNames)
            {
                _sidebarIconFont = Font.CreateDynamicFontFromOSFont(n, 32);
                if (_sidebarIconFont != null) return _sidebarIconFont;
            }
            _sidebarIconFont = Resources.Load<Font>("iiMenu.CanvasMenuUIIconFont");
            return _sidebarIconFont;
        }

        void ApplyModRowThemeColors(Transform anc, ThemePreset t)
        {
            if (anc == null) return;
            var bg = anc.Find("Bg")?.GetComponent<Renderer>();
            if (bg != null) bg.material.color = t.row;
            var bar = anc.Find("Bar")?.GetComponent<Renderer>();
            if (bar != null)
                bar.material.color = new Color(t.acc.r, t.acc.g, t.acc.b, 1f);
            var tTm = anc.Find("T")?.GetComponent<TextMesh>();
            if (tTm != null) tTm.color = t.txt;
            var dTm = anc.Find("D")?.GetComponent<TextMesh>();
            if (dTm != null) dTm.color = t.sub;
            var hi = anc.Find("EdgeHi")?.GetComponent<Renderer>();
            if (hi != null) hi.material.color = Color.Lerp(t.row, t.bar, 0.55f);
            var lo = anc.Find("EdgeLo")?.GetComponent<Renderer>();
            if (lo != null) lo.material.color = t.dark;
        }

        void RefreshModsContentThemeColors(int themeIndex)
        {
            if (_modsContentRoot == null || _currentCategory == 0) return;
            var t = THEMES[themeIndex];
            foreach (var b in _modDynamicBtns)
            {
                if (b.go != null) ApplyModRowThemeColors(b.go.transform, t);
            }
            var mThemeTr = _modsContentRoot.Find("mTheme");
            if (mThemeTr != null) ApplyModRowThemeColors(mThemeTr, t);
        }

        Color SidebarCategoryIconColor(SidebarCategoryIcon k)
        {
            var acc = THEMES[_themeIdx].acc;
            switch (k)
            {
                case SidebarCategoryIcon.Settings:
                    return new Color(
                        Mathf.Clamp01(acc.r * 1.05f),
                        Mathf.Clamp01(acc.g * 1.0f),
                        Mathf.Clamp01(acc.b * 1.12f), 0.9f);
                case SidebarCategoryIcon.Room:
                    return new Color(0.48f, 0.74f, 1f, 0.9f);
                case SidebarCategoryIcon.Movement:
                    return new Color(0.52f, 0.95f, 0.62f, 0.9f);
                case SidebarCategoryIcon.Safety:
                    return new Color(1f, 0.68f, 0.42f, 0.9f);
                default:
                    return new Color(acc.r, acc.g, acc.b, 0.9f);
            }
        }

        static int ModToggleKey(int cat, int i) => cat * 1000 + i;

        void BuildMats()
        {
            Shader sh = Shader.Find("Unlit/Color") ?? Shader.Find("GUI/Text Shader");
            _mPAGER = new Material(sh) { color = Color.Lerp(THEMES[0].row, THEMES[0].acc, 0.26f) };
            _mBG = new Material(sh); _mSB = new Material(sh);
            _mBAR = new Material(sh); _mROW = new Material(sh);
            _mACC = new Material(sh); _mOn = new Material(sh);
            _mOff = new Material(sh); _mRed = new Material(sh);
            _mGrn = new Material(sh); _mDark = new Material(sh);
            ApplyTheme(THEMES[_themeIdx]);

            _font = Font.CreateDynamicFontFromOSFont("Segoe UI", 28)
               ?? Font.CreateDynamicFontFromOSFont("Arial", 28)
               ?? Font.CreateDynamicFontFromOSFont("Helvetica", 28);
            if (_font?.material?.mainTexture != null)
                _font.material.mainTexture.filterMode = FilterMode.Trilinear;
        }

        void ApplyTheme(ThemePreset p)
        {
            Shader sh = Shader.Find("Unlit/Color") ?? Shader.Find("GUI/Text Shader");
            void S(Material m, Color col) { if (m != null) m.color = col; }
            S(_mBG, p.bg); S(_mSB, p.sb); S(_mBAR, p.bar); S(_mROW, p.row);
            S(_mACC, p.acc); S(_mOn, p.on_c); S(_mOff, p.off_c);
            S(_mRed, p.red); S(_mGrn, p.grn); S(_mDark, p.dark);
            S(_mPAGER, Color.Lerp(p.row, p.acc, 0.26f));
        }

        void BuildMenu()
        {
            _root = new GameObject("iiRebornMenu");
            _root.transform.SetParent(null, false);
            DontDestroyOnLoad(_root);
            float bgF = -D * 0.5f;

            float cr = 0.024f;
            Cube(_root.transform, "BG", Vector3.zero, new Vector3(W, H, D), _mBG);
            float cx = W * 0.5f - cr;
            float cy = H * 0.5f - cr;
            foreach (var cp in new[] {
                new Vector3(-cx, -cy, -0.001f), new Vector3(cx, -cy, -0.001f),
                new Vector3(-cx,  cy, -0.001f), new Vector3(cx,  cy, -0.001f) })
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(g.GetComponent<Collider>());
                g.name = "CC";
                g.transform.SetParent(_root.transform, false);
                g.transform.localPosition = cp;
                g.transform.localScale = new Vector3(cr * 2f, cr * 2f, D);
                g.GetComponent<Renderer>().material = _mBG;
            }

            float tbW = W - 0.008f;
            float tbY = H * 0.5f - TBH * 0.5f - 0.002f;
            float tbZ = bgF - 0.002f;

            _disconnectTopRootMenu = Anchor(_root.transform, "DisconnectTop",
                new Vector3(0f, H * 0.5f + 0.038f, bgF - 0.003f));
            float discW = 0.24f, discH = 0.034f;
            var disBtnGo = HBtn(_disconnectTopRootMenu.transform, "DisTop", 0f, 0f, discW, discH,
                "Disconnect", _mRed, TS_BTN * 1.08f);
            disBtnGo.onPress = () =>
            {
                try { NetworkSystem.Instance.ReturnToSinglePlayer(); }
                catch { }
            };
            _btns.Add(disBtnGo);
            _disconnectTopRootMenu.SetActive(disconnectButton);

            var tbAnc = Anchor(_root.transform, "TBanc", new Vector3(0f, tbY, tbZ));
            Cube(tbAnc.transform, "Bg", Vector3.zero, new Vector3(tbW, TBH, 0.003f), _mBAR);
            Cube(tbAnc.transform, "Sep", new Vector3(0f, -TBH * 0.5f, -0.001f),
                 new Vector3(tbW, 0.001f, 0.001f), _mDark);

            float tbHalf = TBH * 0.5f;
            float logoS = TBH * 0.72f;
            var logoGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(logoGo.GetComponent<Collider>());
            logoGo.name = "Logo";
            logoGo.transform.SetParent(tbAnc.transform, false);
            logoGo.transform.localPosition = new Vector3(-tbW * 0.46f, 0f, -0.005f);
            logoGo.transform.localRotation = Quaternion.identity;
            logoGo.transform.localScale = new Vector3(logoS, logoS, 1f);
            var logoSh = Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Unlit/Color");
            var logoMat = new Material(logoSh);
            if (logoMat.HasProperty("_Color")) logoMat.color = Color.white;
            logoGo.GetComponent<Renderer>().material = logoMat;
            _logoRend = logoGo.GetComponent<Renderer>();

            RoundDot(tbAnc.transform, "Dot", new Vector3(-tbW * 0.38f, 0f, -0.003f), 0.010f, _mACC);

            TM(tbAnc.transform, "Title", PluginInfo.Name + "   v" + PluginInfo.Version,
               new Vector3(-tbW * 0.34f, tbHalf * 0.30f, -0.004f), TS_TITLE, THEMES[_themeIdx].txt, TextAnchor.MiddleLeft);
            TM(tbAnc.transform, "Sub", "ii reborn",
               new Vector3(-tbW * 0.34f, -tbHalf * 0.36f, -0.004f), TS_SUB, THEMES[_themeIdx].sub, TextAnchor.MiddleLeft);

            float bH = TBH * 0.45f, bPad = 0.004f, bRX = tbW * 0.5f - bPad;
            float bWX = bH * 1.1f;
            var bX = HBtn(tbAnc.transform, "X", bRX - bWX * 0.5f, 0f, bWX, bH, "✕", _mPAGER, TS_BTN * 1.2f);
            bX.tab = -1; bX.onPress = () => SetOpen(false); _btns.Add(bX);

            float cpTop = tbY - TBH * 0.5f - 0.003f, cpBot = -H * 0.5f + 0.005f;
            float cpH = cpTop - cpBot, cpCY = (cpTop + cpBot) * 0.5f;
            float cpZ = bgF - 0.004f;
            var cpAnc = Anchor(_root.transform, "CP", new Vector3(0f, cpCY, cpZ));

            float colGap = 0.007f;
            float mainW = tbW - SidebarWidth - colGap;
            _sidebarNavInnerW = SidebarWidth - 0.014f;
            float sbX = -tbW * 0.5f + SidebarWidth * 0.5f + 0.004f;
            float mainX = tbW * 0.5f - mainW * 0.5f - 0.004f;

            var sbAnc = Anchor(cpAnc.transform, "Sidebar", new Vector3(sbX, 0f, 0f));
            Cube(sbAnc.transform, "SFrame", Vector3.zero, new Vector3(SidebarWidth, cpH, 0.003f), _mSB);
            TM(sbAnc.transform, "STitle", "CATEGORIES",
               new Vector3(0f, cpH * 0.5f - 0.016f, -0.002f),
               TS_ROW * 0.92f, THEMES[_themeIdx].acc, TextAnchor.MiddleCenter);

            _sidebarListRoot = Anchor(sbAnc.transform, "SBList",
                new Vector3(0f, cpH * 0.5f - 0.058f, 0f)).transform;
            _sidebarPagerRoot = Anchor(sbAnc.transform, "SBPager",
                new Vector3(0f, -cpH * 0.5f + 0.03f, 0f)).transform;

            var mainAnc = Anchor(cpAnc.transform, "MainCol", new Vector3(mainX, 0f, 0f));
            BuildModsContent(mainAnc.transform, mainW, cpH);

            RebuildSidebar();

            if (plugin != null) plugin.selectedThemeIndex = _themeIdx;
        }

        void ClearSidebarNav()
        {
            foreach (var b in _sidebarNavBtns)
            {
                _btns.Remove(b);
                _rowSlideT.Remove(b);
                _rowBasePos.Remove(b);
                _pressFlashT.Remove(b);
                Kill(b.go);
            }
            _sidebarNavBtns.Clear();
        }

        public void RebuildSidebar()
        {
            if (_sidebarListRoot == null || _sidebarPagerRoot == null) return;
            ClearSidebarNav();

            var entries = new List<CategoryHubEntry>(Category.Hub.Length + 1) { Category.Home };
            entries.AddRange(Category.Hub);

            int total = entries.Count;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)SidebarPageSize));
            _sidebarPage = Mathf.Clamp(_sidebarPage, 0, pageCount - 1);
            int start = _sidebarPage * SidebarPageSize;
            int count = Mathf.Min(SidebarPageSize, total - start);

            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                var e = entries[start + i];
                var btn = SidebarNavRow(_sidebarListRoot, "sn" + i, y, _sidebarNavInnerW,
                    e.title, e.buttonsCategoryIndex, e.Icon);
                _sidebarNavBtns.Add(btn);
                y -= SidebarRowH + SidebarRowGap;
            }

            EnsureSidebarPager(pageCount);

            RefreshSidebarHighlights();
        }

        Dictionary<SidebarCategoryIcon, Texture2D> _sidebarIconTextures;

        Texture2D LoadSidebarIcon(SidebarCategoryIcon kind)
        {
            if (_sidebarIconTextures == null)
                _sidebarIconTextures = new Dictionary<SidebarCategoryIcon, Texture2D>();

            if (_sidebarIconTextures.TryGetValue(kind, out Texture2D cached))
                return cached;

            string file;

            switch (kind)
            {
                case SidebarCategoryIcon.Settings: file = "settings2.png"; break;
                case SidebarCategoryIcon.Room: file = "room2.png"; break;
                case SidebarCategoryIcon.Movement: file = "movement2.png"; break;
                case SidebarCategoryIcon.Safety: file = "safety2.png"; break;
                case SidebarCategoryIcon.Detected: file = "detected2.png"; break;
                case SidebarCategoryIcon.Visuals: file = "visuals2.png"; break;
                default: file = "home2.png"; break;
            }

            Texture2D texture = null;

            try
            {
                texture = iiMenu.Utilities.AssetUtilities.LoadTextureFromResource(
                    $"{iiMenu.PluginInfo.ClientResourcePath}.{file}");
            }
            catch { }

            if (texture != null && texture.width <= 2)
                texture = null;

            _sidebarIconTextures[kind] = texture;
            return texture;
        }

        void GoToSidebarPage(int target)
        {
            int clamped = Mathf.Clamp(target, 0, Mathf.Max(0, _sidebarPageCount - 1));

            if (clamped == _sidebarPage)
                return;

            _sidebarPage = clamped;
            RebuildSidebar();
            PlaySound(SoundId.TabSwitch);
        }

        void EnsureSidebarPager(int pageCount)
        {
            _sidebarPageCount = pageCount;
            bool show = pageCount > 1;

            if (_sidebarPagerRoot == null)
                return;

            if (_sidebarPrev == null)
            {
                float bw = SidebarWidth * 0.37f;
                float bh = 0.032f;
                float pg = 0.005f;

                _sidebarPrev = HBtn(_sidebarPagerRoot, "SBPrev",
                    -bw * 0.5f - pg * 0.5f, 0f, bw, bh, "◀", _mPAGER, TS_BTN * 1.15f);
                _sidebarPrev.onPress = () => GoToSidebarPage(_sidebarPage - 1);
                _btns.Add(_sidebarPrev);

                _sidebarNext = HBtn(_sidebarPagerRoot, "SBNext",
                    bw * 0.5f + pg * 0.5f, 0f, bw, bh, "▶", _mPAGER, TS_BTN * 1.15f);
                _sidebarNext.onPress = () => GoToSidebarPage(_sidebarPage + 1);
                _btns.Add(_sidebarNext);
            }

            _sidebarPagerRoot.gameObject.SetActive(show);
        }

        void RefreshSidebarHighlights()
        {
            foreach (var b in _sidebarNavBtns)
            {
                if (b == null || b.go == null || b.navTargetCategory == int.MinValue) continue;
                bool sel = _currentCategory == b.navTargetCategory;
                if (b.rowBgRend != null)
                {
                    Color baseC = THEMES[_themeIdx].sb;
                    b.rowBgRend.material.color = sel
                        ? Color.Lerp(baseC, THEMES[_themeIdx].acc, 0.38f)
                        : baseC;
                }
                var bar = b.go.transform.Find("Bar")?.GetComponent<Renderer>();
                if (bar != null)
                {
                    bar.material.color = sel
                        ? THEMES[_themeIdx].acc
                        : new Color(THEMES[_themeIdx].acc.r, THEMES[_themeIdx].acc.g,
                            THEMES[_themeIdx].acc.b, 0.48f);
                }
            }
        }

        MenuButton SidebarNavRow(Transform p, string n, float y, float w, string title, int targetCat,
            SidebarCategoryIcon iconKind)
        {
            float sh = SidebarRowH;
            float rW = w;
            var anc = Anchor(p, n, new Vector3(0f, y, 0f));
            var bgGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(bgGo.GetComponent<Collider>());
            bgGo.name = "Bg";
            bgGo.transform.SetParent(anc.transform, false);
            bgGo.transform.localPosition = Vector3.zero;
            bgGo.transform.localScale = new Vector3(rW, sh, 0.006f);
            var bgR = bgGo.GetComponent<Renderer>();
            bgR.material = new Material(_mROW.shader) { color = THEMES[_themeIdx].sb };

            var barGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(barGo.GetComponent<Collider>());
            barGo.name = "Bar";
            barGo.transform.SetParent(anc.transform, false);
            barGo.transform.localPosition = new Vector3(-rW * 0.5f + 0.0026f, 0f, -0.003f);
            barGo.transform.localScale = new Vector3(0.003f, sh * 0.55f, 0.002f);
            barGo.GetComponent<Renderer>().material = new Material(_mACC.shader)
            {
                color = new Color(THEMES[_themeIdx].acc.r, THEMES[_themeIdx].acc.g,
                    THEMES[_themeIdx].acc.b, 0.52f)
            };

            float shH = sh * 0.5f;
            float iconS = 0.028f;

            if (iconKind == SidebarCategoryIcon.Home)
            {
                var iconGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(iconGo.GetComponent<Collider>());
                iconGo.name = "SBIcon";
                iconGo.transform.SetParent(anc.transform, false);
                iconGo.transform.localPosition = new Vector3(0f, shH * 0.26f, -0.0048f);
                iconGo.transform.localScale = new Vector3(iconS, iconS, 1f);
                var iconR = iconGo.GetComponent<Renderer>();
                Shader ish = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Unlit/Transparent")
                    ?? Shader.Find("Unlit/Texture")
                    ?? Shader.Find("Unlit/Color");
                iconR.material = new Material(ish) { color = Color.white };
                if (LogoCache.TryGetCached(out Texture2D logoTex))
                    ApplySidebarLogoTexture(iconR, logoTex);
                else
                    StartCoroutine(LogoCache.EnsureTexture(tex => ApplySidebarLogoTexture(iconR, tex)));
            }
            else
            {
                Texture2D icon = LoadSidebarIcon(iconKind);

                if (icon != null)
                {
                    var iconGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(iconGo.GetComponent<Collider>());
                    iconGo.name = "SBIcon";
                    iconGo.transform.SetParent(anc.transform, false);
                    iconGo.transform.localPosition = new Vector3(0f, shH * 0.24f, -0.0052f);
                    iconGo.transform.localScale = new Vector3(iconS * 1.12f, iconS * 1.12f, 1f);

                    Shader ish = Shader.Find("Sprites/Default")
                        ?? Shader.Find("Unlit/Transparent")
                        ?? Shader.Find("Universal Render Pipeline/Unlit");

                    var iconR = iconGo.GetComponent<Renderer>();
                    iconR.material = new Material(ish) { color = Color.black };
                    iconR.material.mainTexture = icon;
                }
                else
                {
                    SidebarIcons.Build(anc.transform, iconKind, iconS * 1.35f, -0.0052f,
                        SidebarCategoryIconColor(iconKind), Cube, PillCap);
                }
            }

            TM(anc.transform, "T", title,
               new Vector3(0f, -shH * 0.32f, -0.004f),
               TS_ROW * 0.72f, THEMES[_themeIdx].txt, TextAnchor.MiddleCenter);

            var col = anc.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(rW, sh, 0.060f);
            var rb = anc.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var btn = new MenuButton
            {
                go = anc,
                tab = -1,
                navTargetCategory = targetCat,
                rowBgRend = bgR,
                onPress = () =>
                {
                    currentCategory = targetCat;
                    PlaySound(SoundId.TabSwitch);
                }
            };
            anc.AddComponent<MenuButtonCollider>().Set(btn, this);
            _btns.Add(btn);
            return btn;
        }

        void SelectTheme(int idx)
        {
            int prevIdx = _themeIdx;
            _themeIdx = idx;
            ApplyTheme(THEMES[idx]);

            if (_root == null) return;

            foreach (var tm in _root.GetComponentsInChildren<TextMesh>())
            {
                if (tm.gameObject.name == "St") continue;
                if (tm.gameObject.name == "SBIconTxt")
                {
                    tm.color = THEMES[idx].acc;
                    continue;
                }
                Color c = tm.color;
                if (c.r > 0.88f && c.g > 0.88f) tm.color = THEMES[idx].txt;
                else if (IsSubColor(c)) tm.color = THEMES[idx].sub;
                else if (IsAccentColor(c, prevIdx)) tm.color = THEMES[idx].acc;
            }

            RefreshModsContentThemeColors(idx);

            if (_laserMat != null)
            {
                Color la = THEMES[idx].acc;
                _laserMat.color = new Color(la.r, la.g, la.b, 0.85f);
            }

            if (plugin != null) plugin.selectedThemeIndex = idx;

            RebuildSidebar();

            plugin?.SendNotification_WM("Theme", THEMES[idx].name + " applied");
        }

        bool IsSubColor(Color c)
        {
            return c.r < 0.65f && c.g < 0.65f && c.b < 0.75f && c.a > 0.9f;
        }

        bool IsAccentColor(Color c, int prevIdx)
        {
            Color prev = THEMES[prevIdx].acc;
            return Mathf.Abs(c.r - prev.r) < 0.12f
                && Mathf.Abs(c.g - prev.g) < 0.12f
                && Mathf.Abs(c.b - prev.b) < 0.12f;
        }

        enum SoundId { MenuOpen, MenuClose, ButtonClick, ToggleClick, TabSwitch }

        void PlaySound(SoundId id)
        {
            if (_audio == null) return;
            AudioClip clip = GenerateClip(id);
            if (clip != null) _audio.PlayOneShot(clip, id == SoundId.MenuOpen ? 0.5f : 0.35f);
        }

        AudioClip GenerateClip(SoundId id)
        {
            const int rate = 44100;

            float dur = 0.06f;
            float freq1 = 540f;
            float freq2 = 640f;
            float decay = 10f;

            switch (id)
            {
                case SoundId.MenuOpen:
                    dur = 0.18f; freq1 = 420f; freq2 = 680f; decay = 4f; break;
                case SoundId.MenuClose:
                    dur = 0.14f; freq1 = 620f; freq2 = 320f; decay = 5f; break;
                case SoundId.TabSwitch:
                    dur = 0.08f; freq1 = 520f; freq2 = 620f; decay = 8f; break;
                case SoundId.ToggleClick:
                    dur = 0.07f; freq1 = 780f; freq2 = 780f; decay = 10f; break;
                case SoundId.ButtonClick:
                default:
                    dur = 0.06f; freq1 = 540f; freq2 = 640f; decay = 10f; break;
            }

            int samples = Mathf.Max(1, (int)(rate * dur));
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float progress = (float)i / samples;
                float time = (float)i / rate;
                float env = Mathf.Exp(-decay * progress);
                float freq = Mathf.Lerp(freq1, freq2, progress);
                data[i] = env * Mathf.Sin(2f * Mathf.PI * freq * time) * 0.5f;
            }

            float[] dataCopy = new float[data.Length];
            System.Array.Copy(data, dataCopy, data.Length);

            AudioClip clip = AudioClip.Create("UISound_" + id.ToString(), samples, 1, rate, false,
                (float[] outData) =>
                {
                    int copyLen = Mathf.Min(outData.Length, dataCopy.Length);
                    System.Array.Copy(dataCopy, 0, outData, 0, copyLen);
                    if (copyLen < outData.Length)
                    {
                        for (int i = copyLen; i < outData.Length; i++) outData[i] = 0f;
                    }
                }
            );

            return clip;
        }

        void RefreshToggles()
        {
            float bodyW = 0.050f - 0.023f;
            foreach (var b in _btns)
            {
                if (b == null || b.go == null) continue;
                if (b.isOn == null) continue;
                bool on = b.isOn();
                Color grn = THEMES[_themeIdx].grn;
                Color offC = THEMES[_themeIdx].off_c;
                Color trackOff = new Color(offC.r * 0.92f, offC.g * 0.92f, offC.b * 0.92f, 1f);
                Color trackOn = new Color(grn.r * 0.36f, grn.g * 0.36f, grn.b * 0.36f, 1f);
                Color trackCurrent = on ? trackOn : trackOff;
                if (b.pillRend != null)
                {
                    var pm = b.pillRend.material;
                    pm.color = trackCurrent;
                }
                var pAnc = b.go.transform.Find("PillAnc"); if (pAnc == null) continue;
                foreach (Transform ch in pAnc)
                {
                    if ((ch.name == "CapL" || ch.name == "CapR") && ch.GetComponent<Renderer>() != null)
                    {
                        var cm = ch.GetComponent<Renderer>().material;
                        cm.color = trackCurrent;
                    }
                }
                var dot = pAnc.Find("Dot"); if (dot == null) continue;
                float tx = on ? bodyW * 0.5f : -bodyW * 0.5f;
                var lp = dot.localPosition; lp.x = Mathf.Lerp(lp.x, tx, Time.deltaTime * 16f); dot.localPosition = lp;
                var dr = dot.GetComponent<Renderer>();
                if (dr != null)
                {
                    var dm = dr.material;
                    dm.color = on
                        ? new Color(Mathf.Min(1f, grn.r * 1.05f), Mathf.Min(1f, grn.g * 1.05f),
                            Mathf.Min(1f, grn.b * 1.03f), 1f)
                        : new Color(Mathf.Min(1f, offC.r * 1.30f), Mathf.Min(1f, offC.g * 1.30f), Mathf.Min(1f, offC.b * 1.30f), 1f);
                }

                if (b.statusTM != null && !b.suppressToggleStatus)
                {
                    b.statusTM.text = on ? "ON" : "OFF";
                    if (b.neutralToggleRow)
                    {
                        b.statusTM.color = new Color(THEMES[_themeIdx].txt.r * 0.92f, THEMES[_themeIdx].txt.g * 0.92f,
                            THEMES[_themeIdx].txt.b * 0.95f, 1f);
                    }
                    else
                    {
                        b.statusTM.color = on
                            ? new Color(THEMES[_themeIdx].grn.r, THEMES[_themeIdx].grn.g, THEMES[_themeIdx].grn.b, 1f)
                            : new Color(THEMES[_themeIdx].sub.r * 0.88f, THEMES[_themeIdx].sub.g * 0.88f,
                                THEMES[_themeIdx].sub.b * 0.88f, 1f);
                    }
                }
                if (b.rowBgRend != null)
                    b.rowBgRend.material.color = THEMES[_themeIdx].row;
            }

            RefreshSidebarHighlights();
        }

        const float HomeTitleScale = 1.42f;
        const float HomeLineScale = 1.22f;
        const float HomeSmallScale = 1.18f;

        void CollectEnabledFeatureLabels(List<string> dest)
        {
            dest.Clear();
            if (!disableNotifications) dest.Add("Notifications");
            if (fpsCounter) dest.Add("FPS overlay");
            if (disconnectButton) dest.Add("Disconnect strip");
            if (plugin == null) return;
            if (plugin.autoReauth) dest.Add("Auto reauth");
            if (plugin.disableNetworkTriggers) dest.Add("Net triggers off");
            if (plugin.playerTracers) dest.Add("Player tracers");
            if (plugin.lavaTrails) dest.Add("Lava trails");
            if (plugin.speedDetection) dest.Add("Speed detection");
            if (plugin.showThreatScores) dest.Add("Threat scores");
            if (plugin.badgeTracers) dest.Add("Badge tracers");
            if (plugin.roomAnalyticsEnabled) dest.Add("Room analytics");
            if (plugin.testBadgeMode) dest.Add("Test badge");
            if (plugin.showOverlay) dest.Add("Overlay");
            if (plugin.uiVisible) dest.Add("Extra UI");
        }

        void BuildSearchBar(float y0)
        {
            float rW = _modsCW - 0.008f;
            float height = RH * 0.92f;
            float x = 0f;
            float y = y0 - RH * 0.62f;

            MenuButton field = HBtn(_modsContentRoot, "SearchBar", x, y, rW, height,
                string.Empty, _mPAGER, TS_ROW);

            field.isSearchUi = true;
            field.onPress = () => iiMenu.Menu.Main.Toggle("Search", true);
            _btns.Add(field);

            TM(_modsContentRoot, "SearchHint", "Search mods",
                new Vector3(x - rW * 0.5f + 0.014f, y, -0.006f), TS_ROW,
                THEMES[_themeIdx].sub, TextAnchor.MiddleLeft);

            TM(_modsContentRoot, "SearchGlyph", "⌕",
                new Vector3(x + rW * 0.5f - 0.020f, y, -0.006f), TS_ROW * 1.3f,
                THEMES[_themeIdx].acc, TextAnchor.MiddleCenter);
        }

        void BuildSearchFieldOverlay(float y0)
        {
            float rW = _modsCW - 0.008f;
            float height = RH * 0.92f;
            float y = y0 - RH * 0.62f;

            Cube(_modsContentRoot, "SearchBarActive", new Vector3(0f, y, 0f),
                new Vector3(rW, height, 0.006f), _mPAGER);

            string query = iiMenu.Menu.Main.keyboardInput ?? string.Empty;
            string shown = string.IsNullOrEmpty(query) ? "Type to search..." : query + ((Time.time % 1f) > 0.5f ? "|" : "");

            TM(_modsContentRoot, "SearchQuery", shown,
                new Vector3(-rW * 0.5f + 0.014f, y, -0.006f), TS_ROW,
                string.IsNullOrEmpty(query) ? THEMES[_themeIdx].sub : THEMES[_themeIdx].txt,
                TextAnchor.MiddleLeft);

            MenuButton close = HBtn(_modsContentRoot, "SearchClose",
                rW * 0.5f - 0.020f, y, 0.024f, height * 0.7f, "✕", _mDark, TS_BTN);

            close.isSearchUi = true;
            close.onPress = () => iiMenu.Menu.Main.Toggle("Search", true);
            _btns.Add(close);

            TM(_modsContentRoot, "SearchGlyph", "⌕",
                new Vector3(-rW * 0.5f + 0.020f, y, -0.006f), TS_ROW * 1.3f,
                THEMES[_themeIdx].acc, TextAnchor.MiddleCenter);
        }

        void BuildSearchResults(float y0)
        {
            var found = iiMenu.Menu.Main.SearchedRowSource();

            if (found == null || found.Length == 0)
            {
                TM(_modsContentRoot, "NoResults", "No mods match that.",
                    new Vector3(0f, y0 - (RH + RG) * 1.4f, -0.006f), TS_ROW,
                    THEMES[_themeIdx].sub, TextAnchor.MiddleCenter);
                RefreshModsPager(1);
                return;
            }

            int total = found.Length;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)ModsPageSize));
            _modsPage = Mathf.Clamp(_modsPage, 0, pageCount - 1);
            int pageStart = _modsPage * ModsPageSize;
            int rows = Mathf.Min(ModsPageSize, total - pageStart);

            float y = y0 - (RH + RG) * 0.62f;

            for (int r = 0; r < rows; r++)
            {
                var origin = iiMenu.Menu.Buttons.GetIndex(found[pageStart + r].buttonText);
                if (origin == null)
                    continue;

                iiMenu.CanvasMenuUI.Classes.ButtonInfo info = null;

                for (int c = 0; c < iiMenu.CanvasMenuUI.Classes.Buttons.buttons.Length && info == null; c++)
                {
                    foreach (var view in iiMenu.CanvasMenuUI.Classes.Buttons.buttons[c])
                    {
                        if (view != null && view.buttonText == (origin.overlapText ?? origin.buttonText))
                        {
                            info = view;
                            break;
                        }
                    }
                }

                if (info == null)
                    info = new iiMenu.CanvasMenuUI.Classes.ButtonInfo
                    {
                        buttonText = origin.overlapText ?? origin.buttonText,
                        isTogglable = origin.isTogglable,
                        method = () => iiMenu.Menu.Main.Toggle(origin.buttonText, false),
                        toolTip = origin.toolTip,
                        enabled = origin.enabled
                    };

                BuildSearchRow(y - r * (RH + RG), origin.overlapText ?? origin.buttonText,
                    origin.toolTip, info);
            }

            RefreshModsPager(pageCount);
        }

        void BuildSearchRow(float y, string title, string description,
            iiMenu.CanvasMenuUI.Classes.ButtonInfo info)
        {
            float rW = _modsCW - 0.008f;

            Transform anc = Anchor(_modsContentRoot, "sr", new Vector3(0f, y, 0f)).transform;
            MenuButton btn = Row(anc, "srx", 0f, rW, -1, title, description, null, null,
                () => info.method?.Invoke());
            btn.isOn = info.isTogglable && (info.enableMethod != null || info.disableMethod != null)
                ? (Func<bool>)(() => info.enabled)
                : null;

            _modDynamicBtns.Add(btn);
        }

        void BuildHomePanel(float y0, float cW)
        {
            if (_modsContentRoot == null) return;
            float rW = cW - 0.008f;
            _homePanelRoot = Anchor(_modsContentRoot, "HomePanel", new Vector3(0f, y0, 0f));
            Transform anc = _homePanelRoot.transform;
            string dName = plugin != null ? plugin.DisplayDiscordName : "Customer";
            string dId = plugin != null ? plugin.DisplayDiscordId : "—";
            string licKey = plugin != null ? plugin.DisplayLicenseKey : "—";
            string hwid = plugin != null ? plugin.DisplayHwid : "—";

            float lineStep = 0.048f;
            float top = RH * 0.42f;

            TM(anc, "HL0", "Thanks for buying Nocternal " + dName,
               new Vector3(-rW * 0.45f, top, -0.004f), TS_ROW * HomeTitleScale, THEMES[_themeIdx].txt, TextAnchor.UpperLeft);

            float iconS = 0.058f;
            float iconX = -rW * 0.45f;
            float iconY = top - lineStep * 1.38f;
            var discGo = new GameObject("DiscIcon");
            discGo.transform.SetParent(anc, false);
            discGo.transform.localPosition = new Vector3(iconX, iconY, -0.005f);
            discGo.transform.localRotation = Quaternion.identity;
            discGo.transform.localScale = new Vector3(iconS, iconS, 1f);
            var mf = discGo.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedDiskMesh();
            Renderer avR = discGo.AddComponent<MeshRenderer>();
            Shader ph = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (plugin != null && DiscordAvatarCache.TryApplyCached(plugin, avR)) { }
            else
            {
                avR.material = new Material(ph)
                {
                    color = new Color(0.11f, 0.13f, 0.2f, 1f)
                };
                if (plugin != null)
                    StartCoroutine(DiscordAvatarCache.EnsureAvatar(plugin, avR, null));
            }

            float tx = iconX + iconS * 0.62f;
            float ty = iconY + iconS * 0.16f;
            TM(anc, "HL1", "Discord Name : " + dName,
               new Vector3(tx, ty, -0.004f), TS_ROW * HomeLineScale, THEMES[_themeIdx].sub, TextAnchor.UpperLeft);
            TM(anc, "HL2", "Discord Id : " + dId,
               new Vector3(tx, ty - lineStep, -0.004f), TS_DESC * HomeSmallScale * 1.05f, THEMES[_themeIdx].sub,
               TextAnchor.UpperLeft);
            TM(anc, "HL3", "Key : " + licKey,
               new Vector3(tx, ty - lineStep * 2f, -0.004f), TS_DESC * HomeSmallScale * 1.05f, THEMES[_themeIdx].sub,
               TextAnchor.UpperLeft);
            TM(anc, "HL4", "HWID : " + hwid,
               new Vector3(tx, ty - lineStep * 3f, -0.004f), TS_DESC * HomeSmallScale * 1.05f, THEMES[_themeIdx].sub,
               TextAnchor.UpperLeft);
        }

        void TryHands()
        {
            try
            {
                if (GorillaTagger.Instance == null) return;
                var rig = GorillaTagger.Instance.offlineVRRig;
                if (rig == null) return;

                if (rig.leftHandTransform != null)
                {
                    _lHandTx = rig.leftHandTransform;
                    if (_lH == null)
                        _lH = rig.leftHandTransform.GetComponentInChildren<Collider>();
                }
                if (rig.rightHandTransform != null)
                {
                    _rHandTx = rig.rightHandTransform;
                    if (_rH == null)
                        _rH = rig.rightHandTransform.GetComponentInChildren<Collider>();
                }
            }
            catch { }
        }

        void BuildLasers()
        {
            Shader sh = Shader.Find("GUI/Text Shader") ?? Shader.Find("Unlit/Color");
            Color a = THEMES[_themeIdx].acc;
            _laserMat = new Material(sh) { color = new Color(a.r, a.g, a.b, 0.85f) };
            _laserMat.renderQueue = 3100;
            _dotMat = new Material(sh) { color = new Color(1f, 1f, 1f, 1f) };

            _laserL = MakeLaser("LaserL");
            _laserR = MakeLaser("LaserR");
            _dotL = MakeDot("DotL");
            _dotR = MakeDot("DotR");
        }

        LineRenderer MakeLaser(string n)
        {
            var go = new GameObject(n);
            DontDestroyOnLoad(go);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = _laserMat;
            lr.startWidth = 0.0045f;
            lr.endWidth = 0.0022f;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.enabled = false;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        GameObject MakeDot(string n)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = n;
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.016f;
            go.GetComponent<Renderer>().material = _dotMat;
            go.SetActive(false);
            DontDestroyOnLoad(go);
            return go;
        }

        bool IsUnderMenuRoot(Transform t)
        {
            if (t == null || _root == null) return false;
            return t == _root.transform || t.IsChildOf(_root.transform);
        }

        static bool TryResolveMenuButtonHit(RaycastHit hit, List<MenuButton> buttons, out MenuButton btn)
        {
            btn = null;
            Transform tr = hit.collider?.transform;
            while (tr != null)
            {
                GameObject go = tr.gameObject;
                if (go == null) { tr = tr.parent; continue; }
                foreach (var bb in buttons)
                {
                    if (bb == null || bb.go == null) continue;
                    if (!bb.go.activeSelf) continue;
                    if (bb.go == go)
                    {
                        btn = bb;
                        return true;
                    }
                }
                tr = tr.parent;
            }
            return false;
        }

        const float LaserAimForwardMinDot = 0.28f;

        static Vector3 PickHandPointerDirection(Transform hand, Vector3 menuWorldPos)
        {
            Vector3 towardMenu = menuWorldPos - hand.position;
            if (towardMenu.sqrMagnitude < 1e-8f)
                return hand.forward.normalized;

            Vector3 towardN = towardMenu.normalized;

            float fwdBestDot = -2f;
            Vector3 fwdBest = hand.forward;
            Vector3 f = hand.forward;
            if (f.sqrMagnitude > 1e-10f)
            {
                f.Normalize();
                float s = Vector3.Dot(f, towardN);
                fwdBestDot = s;
                fwdBest = f;
            }
            Vector3 nf = -hand.forward;
            if (nf.sqrMagnitude > 1e-10f)
            {
                nf.Normalize();
                float s = Vector3.Dot(nf, towardN);
                if (s > fwdBestDot)
                {
                    fwdBestDot = s;
                    fwdBest = nf;
                }
            }
            if (fwdBestDot >= LaserAimForwardMinDot)
                return fwdBest;

            float best = -1f;
            Vector3 pick = hand.forward;

            void Consider(Vector3 d)
            {
                if (d.sqrMagnitude < 1e-10f) return;
                d.Normalize();
                float s = Vector3.Dot(d, towardN);
                if (s > best)
                {
                    best = s;
                    pick = d;
                }
            }

            Consider(hand.forward);
            Consider(-hand.forward);
            Consider(hand.up);
            Consider(-hand.up);
            Consider(hand.right);
            Consider(-hand.right);

            if (best < 0.02f)
                return Vector3.zero;
            return pick;
        }

        void UpdateLaser(Transform hand, LineRenderer lr, GameObject dot, bool isLeft)
        {
            if (hand == null || lr == null || dot == null)
            {
                if (lr != null) lr.enabled = false;
                if (dot != null) dot.SetActive(false);
                if (isLeft) _laserHoveredL = null;
                else _laserHoveredR = null;
                return;
            }

            if (_menuDisplayScale <= MenuLaserOpenThreshold || _root == null)
            {
                lr.enabled = false;
                dot.SetActive(false);
                if (isLeft) _laserHoveredL = null;
                else _laserHoveredR = null;
                return;
            }

            Vector3 origin = hand.position;
            Vector3 dir = PickHandPointerDirection(hand, _root.transform.position);
            if (dir.sqrMagnitude < 1e-10f)
            {
                lr.enabled = false;
                dot.SetActive(false);
                if (isLeft) _laserHoveredL = null;
                else _laserHoveredR = null;
                return;
            }

            Vector3 menuFwd = _root.transform.forward;
            // Reject only when the aim is clearly along the panel outward normal (away from the menu).
            // VR hand poses vary; keep this permissive so the ray still draws toward the panel.
            if (Vector3.Dot(dir, menuFwd) > 0.55f)
            {
                lr.enabled = false;
                dot.SetActive(false);
                if (isLeft) _laserHoveredL = null;
                else _laserHoveredR = null;
                return;
            }

            const float handClearance = 0.04f;
            const float menuRayLen = 12f;
            Vector3 rayOrigin = origin + dir * handClearance;

            var hits = Physics.RaycastAll(
                new Ray(rayOrigin, dir), menuRayLen, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            MenuButton hovered = null;
            Vector3 hitPoint = rayOrigin + dir * menuRayLen;
            bool hitMenu = false;

            foreach (var hit in hits)
            {
                Transform ctr = hit.collider?.transform;
                if (!IsUnderMenuRoot(ctr)) continue;
                hitMenu = true;
                hitPoint = hit.point;
                if (TryResolveMenuButtonHit(hit, _btns, out hovered) && hovered != null)
                    break;
            }

            if (hitMenu && hovered == null && !_laserResolveDebugOnce)
            {
                _laserResolveDebugOnce = true;
                Plugin.Log?.LogDebug("Menu laser: hit under menu root but no MenuButton resolved.");
            }

            lr.enabled = true;
            lr.SetPosition(0, origin);
            if (!hitMenu)
            {
                Vector3 missEnd = rayOrigin + dir * menuRayLen;
                lr.SetPosition(1, missEnd);
                dot.SetActive(false);
                Color acLo = THEMES[_themeIdx].acc;
                _laserMat.color = new Color(acLo.r, acLo.g, acLo.b, 0.38f);
                if (isLeft) _laserHoveredL = null;
                else _laserHoveredR = null;
                return;
            }

            lr.SetPosition(1, hitPoint);

            if (hovered != null)
            {
                dot.SetActive(true);
                dot.transform.position = hitPoint;
                Color ac = THEMES[_themeIdx].acc;
                _laserMat.color = new Color(ac.r, ac.g, ac.b, 0.95f);
            }
            else
            {
                dot.SetActive(false);
                Color acLo = THEMES[_themeIdx].acc;
                _laserMat.color = new Color(acLo.r, acLo.g, acLo.b, 0.55f);
            }

            if (isLeft) _laserHoveredL = hovered;
            else _laserHoveredR = hovered;
        }

        void HideLasers()
        {
            if (_laserL != null) _laserL.enabled = false;
            if (_laserR != null) _laserR.enabled = false;
            if (_dotL != null) _dotL.SetActive(false);
            if (_dotR != null) _dotR.SetActive(false);
        }

        static bool XrHandPressed(InputDevice dev)
        {
            if (!dev.isValid) return false;
            dev.TryGetFeatureValue(CommonUsages.trigger, out float tr);
            dev.TryGetFeatureValue(CommonUsages.grip, out float gr);
            dev.TryGetFeatureValue(CommonUsages.triggerButton, out bool trB);
            dev.TryGetFeatureValue(CommonUsages.gripButton, out bool grB);
            return tr > XrPressThreshold || gr > XrPressThreshold || trB || grB;
        }

        void CheckControllerTrigger()
        {
            bool firedL = false, firedR = false;

            try
            {
                GorillaTriggerBridge.Sample(out float gL, out float gR);
                bool downL = GorillaTriggerBridge.IsPressed(gL);
                bool downR = GorillaTriggerBridge.IsPressed(gR);
                bool edgeGL = downL && !GorillaTriggerBridge.IsPressed(_prevGorillaL);
                bool edgeGR = downR && !GorillaTriggerBridge.IsPressed(_prevGorillaR);
                _prevGorillaL = gL;
                _prevGorillaR = gR;

                if (edgeGL && _laserHoveredL != null)
                {
                    Press(_laserHoveredL);
                    firedL = true;
                }
                if (edgeGR && _laserHoveredR != null)
                {
                    Press(_laserHoveredR);
                    firedR = true;
                }
            }
            catch
            {
                _prevGorillaL = _prevGorillaR = 0f;
            }

            try
            {
                bool trigL = false, trigR = false;

                var leftXr = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                var rightXr = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                bool xrLeftOk = leftXr.isValid;
                bool xrRightOk = rightXr.isValid;

                if (xrLeftOk || xrRightOk)
                {
                    if (xrLeftOk) trigL = XrHandPressed(leftXr);
                    if (xrRightOk) trigR = XrHandPressed(rightXr);
                }
                else
                {
                    var gp = UnityEngine.InputSystem.Gamepad.current;
                    if (gp != null)
                    {
                        trigL = gp.leftTrigger.ReadValue() > 0.7f;
                        trigR = gp.rightTrigger.ReadValue() > 0.7f;
                    }
                }

                bool edgeXL = trigL && !_prevTrigL;
                bool edgeXR = trigR && !_prevTrigR;
                _prevTrigL = trigL;
                _prevTrigR = trigR;

                if (!firedL && edgeXL && _laserHoveredL != null)
                    Press(_laserHoveredL);
                if (!firedR && edgeXR && _laserHoveredR != null)
                    Press(_laserHoveredR);
            }
            catch { }
        }

        void Click()
        {
            var cam = iiMenu.Menu.Main.TPC != null ? iiMenu.Menu.Main.TPC : Camera.main;
            if (cam == null) return;
            var ms = UnityEngine.InputSystem.Mouse.current;
            if (ms == null) return;
            Vector2 mp = ms.position.ReadValue();
            var ray = cam.ScreenPointToRay(new Vector3(mp.x, mp.y, 0f));

            if (ResolveAndPress(ray))
                return;

            if (Physics.SphereCast(ray, PickRadius, 60f, ~0, QueryTriggerInteraction.Collide))
                ResolveAndPress(ray, PickRadius);
        }

        const float PickRadius = 0.010f;

        bool ResolveAndPress(Ray ray, float radius = 0f)
        {
            var hits = radius > 0f
                ? Physics.SphereCastAll(ray, radius, 60f, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Collide);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                if (!IsUnderMenuRoot(hits[i].collider?.transform)) continue;
                if (TryResolveMenuButtonHit(hits[i], _btns, out MenuButton mb) && mb != null)
                {
                    Press(mb);
                    return true;
                }
            }

            return false;
        }

        static void Kill(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            Destroy(go);
        }

        void PurgeContentChildren()
        {
            if (_modsContentRoot == null) return;

            for (int i = _modsContentRoot.childCount - 1; i >= 0; i--)
                Kill(_modsContentRoot.GetChild(i).gameObject);

            foreach (var btn in new List<MenuButton>(_pressFlashT.Keys))
            {
                if (btn == null || btn.go == null)
                {
                    _pressFlashT.Remove(btn);
                    continue;
                }

                if (btn.go.transform.IsChildOf(_modsContentRoot))
                {
                    _pressFlashT.Remove(btn);
                    _rowSlideT.Remove(btn);
                    _rowBasePos.Remove(btn);
                    _btns.Remove(btn);
                    _modDynamicBtns.Remove(btn);
                }
            }
        }

        void ClearDynamicModButtons()
        {
            if (_homePanelRoot != null)
            {
                Kill(_homePanelRoot);
                _homePanelRoot = null;
            }

            foreach (var b in _modDynamicBtns)
            {
                _btns.Remove(b);
                _rowSlideT.Remove(b);
                _rowBasePos.Remove(b);
                _pressFlashT.Remove(b);
                Kill(b.go);
            }
            _modDynamicBtns.Clear();


        }

        internal void RebuildModsCategory(bool animate = true)
        {
            if (_modsContentRoot == null) return;
            ClearDynamicModButtons();
            PurgeContentChildren();
            int cat = _currentCategory;

            float y0 = _modsCH * 0.5f - RH * 0.5f - 0.006f;

            if (iiMenu.Menu.Main.isSearching)
            {
                BuildSearchFieldOverlay(y0);
                BuildSearchResults(y0);
                KickRowSlide();
                return;
            }

            int effPageSize = (cat == 0) ? (ModsPageSize - 1) : ModsPageSize;

            if (cat == 0)
            {
                BuildSearchBar(y0);
                y0 -= (RH + RG) * 1.15f;
            }

            var matrix = Buttons.buttons;
            if (matrix == null || cat < 0 || cat >= matrix.Length) return;
            var list = matrix[cat];
            if (list == null || list.Length == 0) return;

            int rowIdx = 0;
            int total = list.Length;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)effPageSize));
            _modsPage = Mathf.Clamp(_modsPage, 0, pageCount - 1);
            int pageStart = _modsPage * effPageSize;
            int pageCountRows = Mathf.Min(effPageSize, total - pageStart);

            for (int i = pageStart; i < pageStart + pageCountRows; i++)
            {
                var info = list[i];

                if (info.navTarget != int.MinValue && info.navTarget == cat)
                    continue;

                string title;
                title = string.IsNullOrEmpty(info.overlapText) ? info.buttonText : info.overlapText;

                string desc = info.toolTip ?? "";
                bool isReturnRow = info.buttonText.IndexOf("return to", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isRealToggle = info.isTogglable
                    && (info.enableMethod != null || info.disableMethod != null);
                int tk = ModToggleKey(cat, i);
                if (isRealToggle && !_modToggleState.ContainsKey(tk))
                    _modToggleState[tk] = info.enabled;

                float rowY = Y(y0, rowIdx);
                rowIdx++;

                if (isReturnRow)
                {
                    MenuButton btn = null;
                    System.Action act = () => info.method?.Invoke();

                    btn = Row(_modsContentRoot, "m" + i, rowY, _modsCW, -1, title, desc, null, null, act);
                    _modDynamicBtns.Add(btn);
                }
                else if (isRealToggle)
                {
                    var btn = TR(_modsContentRoot, "m" + i, rowY, _modsCW, -1, title, desc,
                        () =>
                        {
                            bool on = _modToggleState[tk];
                            if (on)
                            {
                                info.disableMethod?.Invoke();
                                _modToggleState[tk] = false;
                                info.enabled = false;
                            }
                            else
                            {
                                info.enableMethod?.Invoke();
                                _modToggleState[tk] = true;
                                info.enabled = true;
                            }
                        },
                        () => _modToggleState[tk],
                        false, false);
                    _modDynamicBtns.Add(btn);
                }
                else
                {
                    MenuButton btn = null;
                    System.Action act = () => info.method?.Invoke();

                    btn = Row(_modsContentRoot, "m" + i, rowY, _modsCW, -1, title, desc, null, null, act);
                    _modDynamicBtns.Add(btn);
                }
            }

RefreshModsPager(pageCount);
            if (!pagerLogged) { pagerLogged = true; Debug.Log("[ii Reborn] mods pager ready. cat=" + cat + " rows=" + total + " pages=" + pageCount + " root=" + (_modsPagerRoot != null)); }

            if (animate)
                KickRowSlide();
        }

void BuildModsPager(int pageCount)
        {
            for (int i = _modsPagerBtns.Count - 1; i >= 0; i--)
            {
                MenuButton old = _modsPagerBtns[i];
                _btns.Remove(old);
                Kill(old.go);
                _modsPagerBtns.RemoveAt(i);
            }

            if (_modsPageLabel != null)
            {
                Kill(_modsPageLabel.gameObject);
                _modsPageLabel = null;
            }

            float y = -_modsCH * 0.5f + ModsPagerHeight * 0.5f + 0.004f;
            float bw = 0.048f;
            float gap = 0.006f;
            float halfW = _modsCW * 0.5f - 0.008f;
            float slot = bw * 0.5f + gap * 0.5f;

            MenuButton Add(string name, float x, string glyph, System.Action act)
            {
                MenuButton btn = HBtn(_modsContentRoot, name, x, y, bw, ModsPagerHeight,
                    glyph, _mPAGER, TS_BTN * 1.15f);
                btn.onPress = act;
                _btns.Add(btn);
                _modsPagerBtns.Add(btn);
                return btn;
            }

            Add("MFirst", -halfW + slot, "|◀", () =>
            {
                if (_modsPage == 0) return;
                _modsPage = 0;
                RebuildModsCategory();
                PlaySound(SoundId.TabSwitch);
            });

            Add("MPrev", -halfW + slot * 3f, "◀", () =>
            {
                if (_modsPage <= 0) return;
                _modsPage--;
                RebuildModsCategory();
                PlaySound(SoundId.TabSwitch);
            });

            Add("MNext", halfW - slot * 3f, "▶", () =>
            {
                if (_modsPage >= pageCount - 1) return;
                _modsPage++;
                RebuildModsCategory();
                PlaySound(SoundId.TabSwitch);
            });

            Add("MLast", halfW - slot, "▶|", () =>
            {
                if (_modsPage >= pageCount - 1) return;
                _modsPage = pageCount - 1;
                RebuildModsCategory();
                PlaySound(SoundId.TabSwitch);
            });

            _modsPageLabel = TM(_modsContentRoot, "MLabel",
                (_modsPage + 1) + " / " + pageCount,
new Vector3(0f, y, -0.004f), TS_ROW * 0.9f, THEMES[_themeIdx].sub, TextAnchor.MiddleCenter);
        }

        float _modsCW, _modsCH;

        public void ApplyExternalTheme(Color bg, Color sb, Color bar, Color row, Color acc,
            Color txt, Color sub, Color onC, Color offC, Color red, Color dark)
        {
            int index = Mathf.Clamp(_themeIdx, 0, THEMES.Length - 1);

            ThemePreset t = THEMES[index];
            t.bg = bg; t.sb = sb; t.bar = bar; t.row = row; t.acc = acc;
            t.txt = txt; t.sub = sub; t.on_c = onC; t.off_c = offC;
            t.red = red; t.grn = acc; t.dark = dark;

            THEMES[index] = t;

            ApplyTheme(t);
            ApplyModRowThemeColorsForAll();
            RefreshSidebarHighlights();
        }

        void ApplyModRowThemeColorsForAll()
        {
            var t = THEMES[Mathf.Clamp(_themeIdx, 0, THEMES.Length - 1)];

            if (_homePanelRoot != null)
                ApplyModRowThemeColors(_homePanelRoot.transform, t);

            foreach (var b in _modDynamicBtns)
                if (b != null && b.go != null)
                    ApplyModRowThemeColors(b.go.transform, t);

            if (_modsPageLabel != null)
                _modsPageLabel.color = t.sub;

            if (_mPAGER != null) _mPAGER.color = Color.Lerp(t.row, t.acc, 0.26f);
        }

        void BuildModsContent(Transform p, float cW, float cH)
        {
            _modsCW = cW;
            _modsCH = cH;
            var rootGo = Anchor(p, "ModsRoot", Vector3.zero);
            _modsContentRoot = rootGo.transform;
            BuildModsPager();
            RebuildModsCategory();
        }

        void RefreshModsPager(int pageCount)
        {
            _modsPageCount = pageCount;

            bool show = pageCount > 1;
            int pages = Mathf.Max(1, pageCount);

            if (_modsPageLabel != null)
                _modsPageLabel.text = (_modsPage + 1) + " / " + pages;

            foreach (MenuButton btn in _modsPagerBtns)
            {
                if (btn == null || btn.go == null) continue;
                btn.go.SetActive(show);
            }
        }

        void BuildModsPager()
        {
            if (_modsPagerBtns.Count > 0)
                return;

            _modsPagerRoot = Anchor(_modsContentRoot.parent, "ModsPager", Vector3.zero).transform;

            float y = -_modsCH * 0.5f + ModsPagerHeight * 0.5f + 0.004f;
            float bw = 0.048f;
            float gap = 0.006f;
            float halfW = _modsCW * 0.5f - 0.008f;
            float slot = bw * 0.5f + gap * 0.5f;

            MenuButton Add(string name, float x, string glyph, System.Action act)
            {
                MenuButton btn = HBtn(_modsPagerRoot, name, x, y, bw, ModsPagerHeight,
                    glyph, _mPAGER, TS_BTN * 1.15f);
                btn.onPress = act;
                _btns.Add(btn);
                _modsPagerBtns.Add(btn);
                return btn;
            }

            Add("MFirst", -halfW + slot, "|◀", () => GoToModPage(0));
            Add("MPrev", -halfW + slot * 3f, "◀", () => GoToModPage(_modsPage - 1));
            Add("MNext", halfW - slot * 3f, "▶", () => GoToModPage(_modsPage + 1));
            Add("MLast", halfW - slot, "▶|", () => GoToModPage(_modsPageCount - 1));

            _modsPageLabel = TM(_modsPagerRoot, "MLabel", "1 / 1",
                new Vector3(0f, y, -0.004f), TS_ROW * 0.9f, THEMES[_themeIdx].sub,
                TextAnchor.MiddleCenter);
        }

        void GoToModPage(int target)
        {
            int clamped = Mathf.Clamp(target, 0, Mathf.Max(0, _modsPageCount - 1));

            if (clamped == _modsPage)
                return;

            _modsPage = clamped;
            RebuildModsCategory();
            PlaySound(SoundId.TabSwitch);
        }

        float Y(float y0, float i) => y0 - i * (RH + RG);

        MenuButton Row(Transform p, string n, float y, float cW, int tab,
            string title, string desc,
            Action press, Func<bool> state, Action action = null)
        {
            float rW = cW - 0.008f;
            var anc = Anchor(p, n, new Vector3(0f, y, 0f));

            Material rowMat = new Material(_mROW.shader) { color = THEMES[_themeIdx].row };

            Cube(anc.transform, "Bg", Vector3.zero,
                 new Vector3(rW, RH, 0.007f), rowMat);

            Color edgeLight = new Material(_mROW.shader).color;
            edgeLight = Color.Lerp(THEMES[_themeIdx].row, THEMES[_themeIdx].bar, 0.55f);

            Cube(anc.transform, "EdgeHi", new Vector3(0f, RH * 0.5f - 0.0011f, -0.0035f),
                 new Vector3(rW - 0.024f, 0.0016f, 0.001f),
                 new Material(_mROW.shader) { color = edgeLight });

            Cube(anc.transform, "EdgeLo", new Vector3(0f, -RH * 0.5f + 0.0011f, -0.0035f),
                 new Vector3(rW - 0.012f, 0.0016f, 0.001f), _mDark);

            Cube(anc.transform, "Bar", new Vector3(-rW * 0.5f + 0.003f, 0f, -0.0035f),
                 new Vector3(0.004f, RH * 0.62f, 0.002f), _mACC);
            float rHalf = RH * 0.5f;
            TM(anc.transform, "T", title,
               new Vector3(-rW * 0.5f + 0.014f, rHalf * 0.34f, -0.004f),
               TS_ROW, THEMES[_themeIdx].txt, TextAnchor.MiddleLeft);
            TM(anc.transform, "D", desc,
               new Vector3(-rW * 0.5f + 0.014f, -rHalf * 0.42f, -0.004f),
               TS_DESC, THEMES[_themeIdx].sub, TextAnchor.MiddleLeft);
            var col = anc.AddComponent<BoxCollider>();
            col.isTrigger = true; col.size = new Vector3(rW, RH, 0.060f);
            var rb = anc.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var btn = new MenuButton { go = anc, tab = tab, rowBgRend = anc.transform.Find("Bg")?.GetComponent<Renderer>() };
            btn.onPress = action ?? press; btn.isOn = state;
            anc.AddComponent<MenuButtonCollider>().Set(btn, this);
            btn.neutralToggleRow = false;
            _btns.Add(btn);
            return btn;
        }

        MenuButton TR(Transform p, string n, float y, float cW, int tab,
            string title, string desc, Action rowPress, Func<bool> state,
            bool neutralRow = false, bool suppressToggleStatus = false)
        {
            var btn = Row(p, n, y, cW, tab, title, desc, rowPress, state);
            btn.neutralToggleRow = neutralRow;
            btn.suppressToggleStatus = suppressToggleStatus;
            float rW = cW - 0.008f;
            float pW = 0.050f, pH = 0.023f;
            var pAnc = Anchor(btn.go.transform, "PillAnc", new Vector3(rW * 0.5f - 0.028f, 0f, -0.004f));
            Cube(pAnc.transform, "Pill", Vector3.zero, new Vector3(pW - pH, pH, 0.005f), _mOff);
            btn.pillRend = pAnc.transform.Find("Pill")?.GetComponent<Renderer>();
            PillCap(pAnc.transform, "CapL", new Vector3(-(pW - pH) * 0.5f, 0f, 0f), pH * 1.04f, _mOff);
            PillCap(pAnc.transform, "CapR", new Vector3((pW - pH) * 0.5f, 0f, 0f), pH * 1.04f, _mOff);
            PillCap(pAnc.transform, "Dot", new Vector3(-(pW - pH) * 0.5f, 0f, -0.001f), pH * 0.68f,
                new Material(Shader.Find("Unlit/Color") ?? Shader.Find("GUI/Text Shader")) { color = Color.white });

            if (!suppressToggleStatus)
            {
                TM(btn.go.transform, "St", "OFF",
                   new Vector3(rW * 0.5f - 0.086f, -RH * 0.015f, -0.004f),
                   TS_ROW * 0.9f, THEMES[_themeIdx].sub, TextAnchor.MiddleRight);
                btn.statusTM = btn.go.transform.Find("St").GetComponent<TextMesh>();
            }
            else btn.statusTM = null;

            var titleTm = btn.go.transform.Find("T")?.GetComponent<TextMesh>();
            if (titleTm != null)
            {
                var lp = titleTm.transform.localPosition;
                lp.x = -rW * 0.5f + 0.012f;
                titleTm.transform.localPosition = lp;
                titleTm.characterSize = TS_ROW * 0.88f * 0.5f;
            }

            return btn;
        }

        static Mesh _sharedDiskMesh;

        /// <summary>Unit-diameter flat disc in XY, +Z normal — UVs map square texture to a circular crop (Discord-style).</summary>
        static Mesh SharedDiskMesh()
        {
            if (_sharedDiskMesh != null) return _sharedDiskMesh;
            const int segments = 64;
            var m = new Mesh { name = "iiMenu.CanvasMenuUIAvatarDisk" };
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var norms = new Vector3[segments + 1];
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            norms[0] = Vector3.forward;
            const float r = 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * r;
                float y = Mathf.Sin(ang) * r;
                verts[i + 1] = new Vector3(x, y, 0f);
                uvs[i + 1] = new Vector2(0.5f + x, 0.5f + y);
                norms[i + 1] = Vector3.forward;
            }
            var tris = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = 1 + i;
                tris[i * 3 + 2] = 1 + ((i + 1) % segments);
            }
            m.vertices = verts;
            m.uv = uvs;
            m.normals = norms;
            m.triangles = tris;
            m.RecalculateBounds();
            _sharedDiskMesh = m;
            return m;
        }

        static GameObject Anchor(Transform p, string n, Vector3 lp)
        { var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.localPosition = lp; return g; }

        static GameObject Cube(Transform p, string n, Vector3 lp, Vector3 sc, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(g.GetComponent<Collider>());
            g.name = n; g.transform.SetParent(p, false);
            g.transform.localPosition = lp; g.transform.localScale = sc;
            g.GetComponent<Renderer>().material = m; return g;
        }

        static GameObject PillCap(Transform p, string n, Vector3 lp, float d, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.name = n; g.transform.SetParent(p, false);
            g.transform.localPosition = lp;
            g.transform.localScale = new Vector3(d, d, 0.005f);
            g.GetComponent<Renderer>().material = m; return g;
        }

        static GameObject RoundDot(Transform p, string n, Vector3 lp, float d, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.name = n; g.transform.SetParent(p, false);
            g.transform.localPosition = lp;
            g.transform.localScale = new Vector3(d, d, 0.006f);
            g.GetComponent<Renderer>().material = m; return g;
        }

        MenuButton HBtn(Transform p, string n, float x, float y, float w, float h, string lbl, Material m, float ts)
        {
            var anc = Anchor(p, n, new Vector3(x, y, -0.004f));
            Cube(anc.transform, "Bg", Vector3.zero, new Vector3(w, h, 0.006f), m);
            var col = anc.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(w, h, 0.060f);
            col.center = new Vector3(0f, 0f, -0.004f);
            var rb = anc.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            TM(anc.transform, n + "L", lbl, new Vector3(0f, 0f, -0.004f), ts, Color.white, TextAnchor.MiddleCenter);
            var btn = new MenuButton { go = anc };
            anc.AddComponent<MenuButtonCollider>().Set(btn, this);
            return btn;
        }

        TextMesh TM(Transform p, string n, string txt, Vector3 lp, float cs, Color col, TextAnchor anchor)
        {
            return TM(p, n, txt, lp, cs, col, anchor, null);
        }

        TextMesh TM(Transform p, string n, string txt, Vector3 lp, float cs, Color col, TextAnchor anchor, Font fontOverride)
        {
            var g = new GameObject(n); g.transform.SetParent(p, false);
            g.transform.localPosition = lp; g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = Vector3.one;
            var tm = g.AddComponent<TextMesh>();
            tm.text = txt; tm.fontSize = 200; tm.characterSize = cs * 0.5f;
            tm.color = col; tm.anchor = anchor; tm.alignment = TextAlignment.Left; tm.richText = true;
            Font f = fontOverride ?? _font;
            if (f != null) tm.font = f;
            var mr = g.GetComponent<MeshRenderer>();
            if (f != null) mr.material = f.material;
            mr.sortingOrder = 30; return tm;
        }

        void KickRowSlide()
        {
            var visible = new System.Collections.Generic.List<MenuButton>();
            foreach (var b in _modDynamicBtns)
                visible.Add(b);

            visible.Sort((a, b2) =>
                b2.go.transform.localPosition.y.CompareTo(
                 a.go.transform.localPosition.y));

            for (int i = 0; i < visible.Count; i++)
            {
                var btn = visible[i];
                _rowSlideT[btn] = -(i * RowSlideCascade);
                if (!_rowBasePos.ContainsKey(btn))
                    _rowBasePos[btn] = btn.go.transform.localPosition;
            }
        }

        void UpdateRowSlides(float dt)
        {
            var keys = new System.Collections.Generic.List<MenuButton>(_rowSlideT.Keys);
            foreach (var btn in keys)
            {
                if (btn.go == null) continue;
                _rowSlideT[btn] += dt;
                float t = Mathf.Clamp01(_rowSlideT[btn]);
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);

                if (_rowBasePos.TryGetValue(btn, out Vector3 baseP))
                {
                    float offsetX = Mathf.Lerp(RowSlideOffset, 0f, ease);
                    btn.go.transform.localPosition = new Vector3(
                        baseP.x + offsetX, baseP.y, baseP.z);
                }

                if (t >= 1f) _rowSlideT.Remove(btn);
            }
        }

        void UpdatePressFlash(float dt)
        {
            if (_pressFlashT.Count == 0)
                return;

            var keys = new System.Collections.Generic.List<MenuButton>(_pressFlashT.Keys);
            foreach (var btn in keys)
            {
                if (btn == null || btn.go == null)
                {
                    _pressFlashT.Remove(btn);
                    if (_pressFlashBase.TryGetValue(btn, out Color dead)) _pressFlashBase.Remove(btn);
                    continue;
                }

                _pressFlashT[btn] -= dt * 6f;
                float f = Mathf.Clamp01(_pressFlashT[btn]);

                var bgRend = btn.go.transform.Find("Bg")?.GetComponent<Renderer>();

                if (bgRend != null && !_pressFlashBase.ContainsKey(btn))
                    _pressFlashBase[btn] = bgRend.material.color;

                bool toggleRow = btn.pillRend != null;
                if (bgRend != null && !toggleRow)
                {
                    Color flashCol = Color.Lerp(THEMES[_themeIdx].row, THEMES[_themeIdx].acc, f * 0.35f);
                    bgRend.material.color = flashCol;
                }

                if (_pressFlashT[btn] <= 0f)
                {
                    if (bgRend != null && _pressFlashBase.TryGetValue(btn, out Color baseColor))
                        bgRend.material.color = baseColor;

                    _pressFlashBase.Remove(btn);
                    _pressFlashT.Remove(btn);
                }
            }
        }

        readonly Dictionary<MenuButton, Color> _pressFlashBase = new Dictionary<MenuButton, Color>();

        readonly List<string> _featureHudScratch = new List<string>(32);
        static Texture2D _hudWhite;

        static void HudFill(Rect r, Color c)
        {
            Color p = GUI.color;
            GUI.color = c;
            if (_hudWhite == null)
            {
                _hudWhite = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _hudWhite.SetPixel(0, 0, Color.white);
                _hudWhite.Apply(false, true);
            }
            GUI.DrawTexture(r, _hudWhite);
            GUI.color = p;
        }

        void OnGUI()
        {

            var th = THEMES[_themeIdx];
            Color hudAccent = new Color(th.acc.r, th.acc.g, th.acc.b, 0.45f);
            Color hudTxt = new Color(th.txt.r, th.txt.g, th.txt.b, 0.96f);
            Color hudSub = new Color(th.sub.r, th.sub.g, th.sub.b, 0.92f);

            CollectEnabledFeatureLabels(_featureHudScratch);
            GUI.depth = -125;
            const float listW = 268f;
            const float lineH = 23f;
            const float titleH = 22f;
            float lx = 12f;
            float ly = 10f;
            int n = _featureHudScratch.Count;

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold
            };
            hdr.normal.textColor = hudTxt;

            var rowSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            rowSt.normal.textColor = hudTxt;

            GUI.Label(new Rect(lx, ly, listW, titleH), "Array List", hdr);

            Color sepCol = new Color(th.acc.r, th.acc.g, th.acc.b, 0.55f);
            float sepY = ly + titleH + 6f;
            HudFill(new Rect(lx, sepY, listW, 1f), sepCol);

            const float accentBarW = 3f;
            float rowTop = sepY + 12f;
            Color barCol = new Color(hudAccent.r * 1.1f, hudAccent.g * 1.1f, hudAccent.b * 1.15f, 0.75f);

            for (int i = 0; i < n; i++)
            {
                float ry = rowTop + i * lineH;
                float innerBarH = lineH - 6f;
                HudFill(new Rect(lx, ry + 3f, accentBarW, innerBarH), barCol);
                GUI.Label(new Rect(lx + accentBarW + 10f, ry, listW - accentBarW - 12f, lineH),
                    _featureHudScratch[i], rowSt);
            }

            if (n == 0)
            {
                float ry = rowTop;
                float innerBarH = lineH - 6f;
                HudFill(new Rect(lx, ry + 3f, accentBarW, innerBarH), barCol);
                var empty = new GUIStyle(rowSt);
                empty.fontStyle = FontStyle.Italic;
                empty.normal.textColor = hudSub;
                GUI.Label(new Rect(lx + accentBarW + 10f, ry, listW - accentBarW - 12f, lineH),
                    "No modules enabled", empty);
            }

            GUI.depth = -120;
            int fps = Mathf.Max(0, Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 1e-5f)));
            string pingStr = TryHudPing();
            float sw = Screen.width;
            float barW = Mathf.Clamp(sw * 0.42f, 280f, 520f);
            float barH = 28f;
            float x = (sw - barW) * 0.5f;
            float y = 8f;

            Color menuFace = new Color(
                Mathf.Lerp(th.bg.r, th.row.r, 0.52f),
                Mathf.Lerp(th.bg.g, th.row.g, 0.52f),
                Mathf.Lerp(th.bg.b, th.row.b, 0.52f), 0.97f);
            Color menuBorder = new Color(th.bar.r, th.bar.g, th.bar.b, 0.94f);
            Color menuAccentLine = new Color(th.acc.r, th.acc.g, th.acc.b, 0.5f);
            Color menuShadow = new Color(th.dark.r, th.dark.g, th.dark.b, 0.45f);

            HudFill(new Rect(x + 2f, y + 3f, barW, barH), menuShadow);
            HudFill(new Rect(x - 1f, y - 1f, barW + 2f, barH + 2f), menuBorder);
            HudFill(new Rect(x, y, barW, barH), menuFace);
            HudFill(new Rect(x + 1f, y + 1f, barW - 2f, 1f), new Color(1f, 1f, 1f, 0.07f));
            HudFill(new Rect(x + 4f, y + 2f, 2f, barH - 4f), new Color(th.acc.r, th.acc.g, th.acc.b, 0.65f));
            HudFill(new Rect(x + 4f, y + barH - 2f, barW - 8f, 1f), menuAccentLine);

            const string wideSep = "      \u00B7      ";
            string barLine = "\u25C6" + wideSep + PluginInfo.Name + wideSep + "v" + PluginInfo.Version
                + wideSep + pingStr + wideSep + fps + " FPS";

            var barSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            barSt.normal.textColor = hudTxt;
            barSt.padding = new RectOffset(14, 14, 2, 2);
            GUI.Label(new Rect(x, y, barW, barH), barLine, barSt);
        }

        static string TryHudPing()
        {
            try
            {
                return global::Photon.Pun.PhotonNetwork.GetPing() + " ms";
            }
            catch
            {
                return "\u2014 ms";
            }
        }
    }
}
