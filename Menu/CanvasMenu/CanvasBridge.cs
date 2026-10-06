using System;
using UnityEngine;

namespace iiMenu.Menu
{
    public static class CanvasBridge
    {
        public static void Create()
        {
            try
            {
                Build();
            }
            catch (Exception exception)
            {
                Debug.LogError("[ii Reborn] menu create failed: " + exception);
            }
        }

        private static void Build()
        {
            iiMenu.CanvasMenuUI.Classes.Buttons.Invalidate();

            if (iiMenu.CanvasMenuUI.Main.Menu.Instance != null)
            {
                iiMenu.CanvasMenuUI.Main.Menu.Instance.RebuildModsCategory(false);
                iiMenu.CanvasMenuUI.Main.Menu.Instance.RebuildSidebar();
                return;
            }

            iiMenu.CanvasMenuUI.Plugin.Log.selectedThemeIndex = 0;

            GameObject host = new GameObject("iiRebornMenuHost");
            host.transform.SetParent(null);

            host.AddComponent<iiMenu.CanvasMenuUI.Main.Menu>().plugin = iiMenu.CanvasMenuUI.Plugin.Log;

            Debug.Log("[ii Reborn] menu built, open=" + iiMenu.CanvasMenuUI.Main.Menu.Instance.IsOpen);
        }

        public static void SetOpen(bool open)
        {
            try
            {
                if (iiMenu.CanvasMenuUI.Main.Menu.Instance == null)
                {
                    Debug.LogError("[ii Reborn] SetOpen(" + open + ") but no menu instance exists");
                    return;
                }

                iiMenu.CanvasMenuUI.Main.Menu.Instance.SetOpen(open);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ii Reborn] SetOpen failed: " + exception);
            }
        }

        public static void Destroy()
        {
            try
            {
                if (iiMenu.CanvasMenuUI.Main.Menu.Instance != null)
                {
                    UnityEngine.Object.Destroy(iiMenu.CanvasMenuUI.Main.Menu.Instance.gameObject);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[ii Reborn] Destroy failed: " + exception);
            }
        }

        public static void Tick()
        {
            try
            {
                iiMenu.CanvasMenuUI.Classes.Buttons.SyncStates();

                if (iiMenu.Menu.Main.themeType != lastTheme)
                {
                    lastTheme = iiMenu.Menu.Main.themeType;
                    ApplyMenuTheme();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[ii Reborn] Tick failed: " + exception);
            }
        }

        private static int lastTheme = -1;

        private static void ApplyMenuTheme()
        {
            if (iiMenu.Menu.Main.canvasLayout == false)
                return;

            iiMenu.CanvasMenuUI.Main.Menu card = iiMenu.CanvasMenuUI.Main.Menu.Instance;
            if (card == null)
                return;

            Color.RGBToHSV(AccentColour(), out float hue, out float sat, out float value);
            sat = Mathf.Clamp(sat, 0.30f, 0.95f);
            float dark = Mathf.Clamp(sat * 1.6f, 0.30f, 0.85f);

            card.ApplyExternalTheme(
                Color.HSVToRGB(hue, dark, 0.085f),
                Color.HSVToRGB(hue, dark, 0.055f),
                Color.HSVToRGB(hue, dark, 0.115f),
                Color.HSVToRGB(hue, dark * 0.90f, 0.170f),
                Color.HSVToRGB(hue, sat, 1.00f),
                Color.HSVToRGB(hue, 0.08f, 0.95f),
                Color.HSVToRGB(hue, 0.30f, 0.58f),
                new Color(0.14f, 0.68f, 0.38f, 1f),
                Color.HSVToRGB(hue, dark * 0.85f, 0.20f),
                new Color(0.82f, 0.22f, 0.26f, 1f),
                Color.HSVToRGB(hue, dark, 0.030f));
        }

        private static Color AccentColour()
        {
            Color best = Color.white;
            float bestScore = -1f;

            void Consider(Color c)
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (s < 0.10f || v < 0.05f)
                    return;

                if (s * v <= bestScore)
                    return;

                bestScore = s * v;
                best = c;
            }

            try
            {
                if (iiMenu.Menu.Main.backgroundColor?.colors.Length > 0)
                    Consider(iiMenu.Menu.Main.backgroundColor.colors[0].color);

                foreach (iiMenu.Classes.Menu.ExtGradient gradient in iiMenu.Menu.Main.buttonColors)
                    if (gradient?.colors.Length > 0)
                        Consider(gradient.colors[0].color);

                foreach (iiMenu.Classes.Menu.ExtGradient gradient in iiMenu.Menu.Main.textColors)
                    if (gradient?.colors.Length > 0)
                        Consider(gradient.colors[0].color);
            }
            catch { }

            return best;
        }
    }
}
