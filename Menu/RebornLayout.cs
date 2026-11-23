/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using GorillaLocomotion;
using iiMenu.Classes.Menu;
using iiMenu.Extensions;
using iiMenu.Managers;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static iiMenu.Menu.Main;
using static iiMenu.Utilities.AssetUtilities;
using ButtonCollider = iiMenu.Classes.Menu.ButtonCollider;

namespace iiMenu.Menu
{
    public static class RebornLayout
    {
        public const int ThemeId = 100;

        private static GameObject root;
        private static GameObject categoryPanel;
        private static GameObject contentPanel;
        private static GameObject headerPanel;

        private static TextMeshPro titleLabel;
        private static TextMeshPro categoryLabel;
        private static TextMeshPro pageLabel;
        private static TextMeshPro fpsLabel;
        private static TextMeshPro roomLabel;

        private static readonly List<GameObject> categoryButtons = new List<GameObject>();
        private static readonly List<GameObject> contentButtons = new List<GameObject>();
        private static readonly List<TextMeshPro> themeTexts = new List<TextMeshPro>();
        private static readonly List<Renderer> bgRenderers = new List<Renderer>();
        private static readonly List<Renderer> btnRenderers = new List<Renderer>();
        private static readonly List<Renderer> activeBtnRenderers = new List<Renderer>();

        private static Material sharedBgMat;
        private static Material sharedBtnMat;
        private static Material sharedActiveBtnMat;

        private static Color cachedBg;
        private static Color cachedBtn;
        private static Color cachedActiveBtn;
        private static Color cachedText;

        private static float lastThemeUpdate;

        private const float TabletWidth = 0.55f;
        private const float TabletHeight = 0.38f;
        private const float TabletDepth = 0.012f;
        private const float Radius = 0.018f;
        private const float CatPanelWidth = 0.14f;
        private const float ContentPanelWidth = 0.37f;
        private const float ButtonHeight = 0.044f;
        private const float ButtonSpacing = 0.006f;
        private const float Padding = 0.012f;

        public static bool IsActive => root != null;

        public static void Build()
        {
            Destroy();

            sharedBgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            sharedBtnMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            sharedActiveBtnMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

            bgRenderers.Clear();
            btnRenderers.Clear();
            activeBtnRenderers.Clear();
            themeTexts.Clear();
            categoryButtons.Clear();
            contentButtons.Clear();

            root = new GameObject("RebornLayout_Root");
            Object.DontDestroyOnLoad(root);

            BuildTabletBody();
            BuildHeader();
            BuildCategoryPanel();
            BuildContentPanel();

            RecenterToHand();
            ApplyTheme();
        }

        public static void Destroy()
        {
            if (root != null)
                Object.Destroy(root);

            root = null;
            categoryPanel = null;
            contentPanel = null;
            headerPanel = null;
            titleLabel = null;
            categoryLabel = null;
            pageLabel = null;
            fpsLabel = null;
            roomLabel = null;

            bgRenderers.Clear();
            btnRenderers.Clear();
            activeBtnRenderers.Clear();
            themeTexts.Clear();
            categoryButtons.Clear();
            contentButtons.Clear();
        }

        public static void Tick()
        {
            if (root == null) return;

            RecenterToHand();

            if (Time.time - lastThemeUpdate > 0.08f)
            {
                lastThemeUpdate = Time.time;
                ApplyTheme();
            }

            UpdateLabels();
        }

        private static void BuildTabletBody()
        {
            GameObject body = CreateRoundedPanel(root.transform, new Vector3(TabletWidth, TabletHeight, TabletDepth));
            body.transform.localPosition = Vector3.zero;
            RegisterBg(body);

            GameObject edge = CreateRoundedPanel(root.transform, new Vector3(TabletWidth + 0.004f, TabletHeight + 0.004f, TabletDepth * 0.4f));
            edge.transform.localPosition = new Vector3(0f, 0f, -TabletDepth * 0.3f);
            RegisterActiveBtn(edge);
        }

        private static void BuildHeader()
        {
            headerPanel = new GameObject("Header");
            headerPanel.transform.SetParent(root.transform, false);
            headerPanel.transform.localPosition = new Vector3(0f, TabletHeight * 0.5f - 0.028f, TabletDepth * 0.5f + 0.001f);

            GameObject headerBg = CreateRoundedPanel(headerPanel.transform, new Vector3(TabletWidth - 0.008f, 0.042f, 0.002f));
            headerBg.transform.localPosition = Vector3.zero;
            RegisterActiveBtn(headerBg);

            Canvas headerCanvas = CreateCanvas(headerPanel.transform);

            titleLabel = CreateTMPro(headerCanvas.transform, new Vector2(0.22f, 0.036f), new Vector3(-0.09f, 0f, 0f));
            titleLabel.fontSize = 0.016f;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.alignment = TextAlignmentOptions.Left;
            titleLabel.text = doCustomName ? customMenuName : "ii<b>Reborn</b> Paid";
            themeTexts.Add(titleLabel);

            roomLabel = CreateTMPro(headerCanvas.transform, new Vector2(0.18f, 0.028f), new Vector3(0.1f, 0f, 0f));
            roomLabel.fontSize = 0.009f;
            roomLabel.alignment = TextAlignmentOptions.Right;
            themeTexts.Add(roomLabel);
        }

        private static void BuildCategoryPanel()
        {
            categoryPanel = new GameObject("CategoryPanel");
            categoryPanel.transform.SetParent(root.transform, false);
            float panelX = -(TabletWidth * 0.5f) + (CatPanelWidth * 0.5f) + Padding;
            categoryPanel.transform.localPosition = new Vector3(panelX, 0f, TabletDepth * 0.5f + 0.001f);

            GameObject catBg = CreateRoundedPanel(categoryPanel.transform, new Vector3(CatPanelWidth, TabletHeight - 0.056f, 0.002f));
            catBg.transform.localPosition = Vector3.zero;
            RegisterBg(catBg);

            Canvas catCanvas = CreateCanvas(categoryPanel.transform);

            categoryLabel = CreateTMPro(catCanvas.transform, new Vector2(CatPanelWidth - Padding * 2f, 0.016f), new Vector3(0f, (TabletHeight - 0.056f) * 0.5f - 0.012f, 0f));
            categoryLabel.fontSize = 0.008f;
            categoryLabel.fontStyle = FontStyles.Bold;
            categoryLabel.alignment = TextAlignmentOptions.Center;
            categoryLabel.text = "Categories";
            themeTexts.Add(categoryLabel);

            RebuildCategoryButtons(catCanvas.transform);
        }

        private static void RebuildCategoryButtons(Transform parent)
        {
            foreach (GameObject b in categoryButtons)
                if (b != null) Object.Destroy(b);
            categoryButtons.Clear();

            string[] cats = Buttons.categoryNames;
            float startY = (TabletHeight - 0.056f) * 0.5f - 0.038f;
            float maxY = -(TabletHeight - 0.056f) * 0.5f + Padding;

            for (int i = 0; i < cats.Length; i++)
            {
                float y = startY - i * (ButtonHeight * 0.72f + ButtonSpacing * 0.5f);
                if (y < maxY) break;

                string catName = cats[i];
                bool isCurrent = Buttons.CurrentCategoryName == catName;

                GameObject btn = CreateButton(parent, catName, new Vector2(CatPanelWidth - Padding * 2f, ButtonHeight * 0.68f), new Vector3(0f, y, 0.001f), isCurrent);
                ButtonCollider col = btn.GetComponent<ButtonCollider>();
                if (col == null) col = btn.AddComponent<ButtonCollider>();
                col.relatedText = catName;

                if (isCurrent)
                    RegisterActiveBtn(btn.GetComponent<Renderer>() ?? btn.GetComponentInChildren<Renderer>());
                else
                    RegisterBtn(btn.GetComponent<Renderer>() ?? btn.GetComponentInChildren<Renderer>());

                categoryButtons.Add(btn);
            }
        }

        private static void BuildContentPanel()
        {
            contentPanel = new GameObject("ContentPanel");
            contentPanel.transform.SetParent(root.transform, false);
            float panelX = (TabletWidth * 0.5f) - (ContentPanelWidth * 0.5f) - Padding;
            contentPanel.transform.localPosition = new Vector3(panelX, 0f, TabletDepth * 0.5f + 0.001f);

            GameObject contBg = CreateRoundedPanel(contentPanel.transform, new Vector3(ContentPanelWidth, TabletHeight - 0.056f, 0.002f));
            contBg.transform.localPosition = Vector3.zero;
            RegisterBg(contBg);

            Canvas contCanvas = CreateCanvas(contentPanel.transform);

            pageLabel = CreateTMPro(contCanvas.transform, new Vector2(ContentPanelWidth - Padding * 2, 0.016f), new Vector3(0f, -(TabletHeight - 0.056f) * 0.5f + 0.012f, 0f));
            pageLabel.fontSize = 0.008f;
            pageLabel.alignment = TextAlignmentOptions.Center;
            themeTexts.Add(pageLabel);

            fpsLabel = CreateTMPro(contCanvas.transform, new Vector2(0.09f, 0.014f), new Vector3(ContentPanelWidth * 0.5f - 0.058f, (TabletHeight - 0.056f) * 0.5f - 0.01f, 0f));
            fpsLabel.fontSize = 0.007f;
            fpsLabel.alignment = TextAlignmentOptions.Right;
            themeTexts.Add(fpsLabel);

            RebuildContentButtons(contCanvas.transform);
        }

        public static void RebuildContentButtons(Transform parent = null)
        {
            if (contentPanel == null) return;

            foreach (GameObject b in contentButtons)
                if (b != null) Object.Destroy(b);
            contentButtons.Clear();

            Canvas contCanvas = contentPanel.GetComponentInChildren<Canvas>();
            if (contCanvas == null) return;
            Transform target = parent ?? contCanvas.transform;

            ButtonInfo[] buttons = Buttons.buttons[Buttons.CurrentCategoryIndex];
            if (buttons == null) return;

            int pageSize = PageSize;
            int start = pageNumber * pageSize;
            float panelH = TabletHeight - 0.056f;
            float startY = panelH * 0.5f - 0.026f;
            float btnH = (panelH - 0.052f - Padding * 2f) / pageSize - ButtonSpacing;

            for (int i = 0; i < pageSize; i++)
            {
                int idx = start + i;
                if (idx >= buttons.Length) break;

                ButtonInfo info = buttons[idx];
                float y = startY - i * (btnH + ButtonSpacing);

                string displayText = info.overlapText ?? info.buttonText;
                displayText = FollowMenuSettings(displayText, true);

                GameObject btn = CreateButton(target, displayText, new Vector2(ContentPanelWidth - Padding * 2f, btnH), new Vector3(0f, y, 0.001f), info.enabled);

                ButtonCollider col = btn.AddComponent<ButtonCollider>();
                col.targetButton = info;
                col.relatedText = info.buttonText;

                Renderer r = btn.GetComponent<Renderer>() ?? btn.GetComponentInChildren<Renderer>();
                if (info.enabled)
                    RegisterActiveBtn(r);
                else
                    RegisterBtn(r);

                if (info.toolTip != null)
                {
                    TextMeshPro tip = CreateTMPro(target, new Vector2(ContentPanelWidth - Padding * 4f, btnH * 0.4f), new Vector3(0f, y - btnH * 0.28f, 0.0015f));
                    tip.fontSize = 0.0055f;
                    tip.text = info.toolTip.Length > 60 ? info.toolTip[..60] + "…" : info.toolTip;
                    tip.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
                    tip.alignment = TextAlignmentOptions.Center;
                }

                contentButtons.Add(btn);
            }

            AddNavButtons(target);
        }

        private static void AddNavButtons(Transform parent)
        {
            float y = -(TabletHeight - 0.056f) * 0.5f + 0.028f;

            if (LastPage > 0)
            {
                GameObject prev = CreateButton(parent, arrowTypes[arrowType][0], new Vector2(0.055f, 0.022f), new Vector3(-0.07f, y, 0.001f), false);
                ButtonCollider pc = prev.AddComponent<ButtonCollider>();
                pc.relatedText = "PreviousPage";
                RegisterBtn(prev.GetComponent<Renderer>() ?? prev.GetComponentInChildren<Renderer>());

                GameObject next = CreateButton(parent, arrowTypes[arrowType][1], new Vector2(0.055f, 0.022f), new Vector3(0.07f, y, 0.001f), false);
                ButtonCollider nc = next.AddComponent<ButtonCollider>();
                nc.relatedText = "NextPage";
                RegisterBtn(next.GetComponent<Renderer>() ?? next.GetComponentInChildren<Renderer>());
            }
        }

        private static void UpdateLabels()
        {
            if (titleLabel != null)
            {
                string t = doCustomName ? customMenuName : "ii<b>Reborn</b> Paid";
                t = FollowMenuSettings(t, !doCustomName, true);
                titleLabel.SafeSetText(t);
            }

            if (roomLabel != null)
            {
                bool inRoom = Photon.Pun.PhotonNetwork.InRoom;
                string rs = inRoom ? "In room: " + Photon.Pun.PhotonNetwork.CurrentRoom.Name : "Not in room";
                roomLabel.SafeSetText(rs);
            }

            if (pageLabel != null)
                pageLabel.SafeSetText($"Page {pageNumber + 1} / {LastPage + 1}");

            if (fpsLabel != null)
                fpsLabel.SafeSetText($"FPS: {lastDeltaTime}");
        }

        private static void ApplyTheme()
        {
            Color bg = backgroundColor.GetCurrentColor();
            Color btn = buttonColors[0].GetCurrentColor();
            Color activeBtn = buttonColors[1].GetCurrentColor();
            Color txt = textColors[1].GetCurrentColor();

            if (bg == cachedBg && btn == cachedBtn && activeBtn == cachedActiveBtn && txt == cachedText)
                return;

            cachedBg = bg;
            cachedBtn = btn;
            cachedActiveBtn = activeBtn;
            cachedText = txt;

            Color bgDark = new Color(bg.r * 0.6f, bg.g * 0.6f, bg.b * 0.6f, bg.a);
            sharedBgMat.color = bgDark;
            sharedBtnMat.color = btn;
            sharedActiveBtnMat.color = activeBtn;

            foreach (TextMeshPro tmp in themeTexts)
                if (tmp != null) tmp.color = txt;
        }

        private static void RegisterBg(GameObject go)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.material = sharedBgMat;
            bgRenderers.Add(r);
        }

        private static void RegisterBtn(Renderer r)
        {
            if (r == null) return;
            r.material = sharedBtnMat;
            btnRenderers.Add(r);
        }

        private static void RegisterActiveBtn(Renderer r)
        {
            if (r == null) return;
            r.material = sharedActiveBtnMat;
            activeBtnRenderers.Add(r);
        }

        private static void RegisterActiveBtn(GameObject go)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.material = sharedActiveBtnMat;
            activeBtnRenderers.Add(r);
        }

        private static void RecenterToHand()
        {
            if (root == null || GorillaTagger.Instance == null) return;

            Transform hand = rightHand
                ? GorillaTagger.Instance.rightHandTransform
                : GorillaTagger.Instance.leftHandTransform;

            root.transform.position = hand.position;

            Vector3 euler = hand.rotation.eulerAngles;
            if (rightHand) euler += new Vector3(0f, 0f, 180f);
            root.transform.rotation = Quaternion.Euler(euler);
        }

        private static GameObject CreateRoundedPanel(Transform parent, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<BoxCollider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            GameObject go = new GameObject("Canvas");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            go.transform.localRotation = Quaternion.identity;

            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;

            CanvasScaler cs = go.AddComponent<CanvasScaler>();
            cs.dynamicPixelsPerUnit = 2500f;

            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        private static TextMeshPro CreateTMPro(Transform parent, Vector2 size, Vector3 localPos)
        {
            GameObject go = new GameObject("TMP");
            go.transform.SetParent(parent, false);

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.font = activeFont;
            tmp.fontSize = 0.012f;
            tmp.enableAutoSizing = false;
            tmp.richText = true;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.overflowMode = TextOverflowModes.Overflow;

            RectTransform rt = tmp.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.localPosition = localPos;
            rt.localRotation = Quaternion.identity;

            return tmp;
        }

        private static GameObject CreateButton(Transform parent, string label, Vector2 size, Vector3 localPos, bool active)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent.parent ?? parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 0.003f);
            go.transform.localRotation = Quaternion.identity;
            go.GetComponent<BoxCollider>().isTrigger = true;
            if (!UnityEngine.InputSystem.Keyboard.current?.qKey.isPressed ?? true)
                go.layer = 2;

            TextMeshPro tmp = CreateTMPro(parent, new Vector2(size.x - Padding, size.y - 0.004f), localPos + new Vector3(0f, 0f, -0.004f));
            tmp.text = label;
            tmp.fontSize = 0.01f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            themeTexts.Add(tmp);

            return go;
        }
    }
}
