using System;
using System.Collections.Generic;
using iiMenu.Menu;
using UnityEngine;

namespace iiMenu.CanvasMenuUI.Classes
{
    public enum SidebarCategoryIcon
    {
        Home,
        Settings,
        Room,
        Movement,
        Safety,
        Detected,
        Visuals
    }

    public static class SidebarCategoryIconGlyphs
    {
        public static char For(SidebarCategoryIcon k)
        {
            switch (k)
            {
                case SidebarCategoryIcon.Settings: return '⚙';
                case SidebarCategoryIcon.Room: return '⌂';
                case SidebarCategoryIcon.Movement: return '➤';
                case SidebarCategoryIcon.Safety: return '✚';
                default: return '★';
            }
        }
    }

    public static class SidebarIcons
    {
        public static void Build(Transform parent, SidebarCategoryIcon kind, float size, float z,
            Color color, Func<Transform, string, Vector3, Vector3, Material, GameObject> cube,
            Func<Transform, string, Vector3, float, Material, GameObject> ball)
        {
            Material mat = SidebarMaterial(color);
            float h = size * 0.5f;

            switch (kind)
            {
                case SidebarCategoryIcon.Home:
                    {
                        cube(parent, "icBody", new Vector3(0f, -h * 0.22f, z),
                            new Vector3(size * 0.62f, size * 0.42f, 0.003f), mat);
                        cube(parent, "icRoofL", new Vector3(-size * 0.17f, h * 0.22f, z),
                            new Vector3(size * 0.44f, size * 0.10f, 0.003f), mat).transform.localRotation =
                                Quaternion.Euler(0f, 0f, 34f);
                        cube(parent, "icRoofR", new Vector3(size * 0.17f, h * 0.22f, z),
                            new Vector3(size * 0.44f, size * 0.10f, 0.003f), mat).transform.localRotation =
                                Quaternion.Euler(0f, 0f, -34f);
                        cube(parent, "icDoor", new Vector3(0f, -h * 0.32f, z - 0.002f),
                            new Vector3(size * 0.18f, size * 0.26f, 0.003f),
                            SidebarMaterial(Color.black));
                        break;
                    }

                case SidebarCategoryIcon.Settings:
                    {
                        ball(parent, "icHub", new Vector3(0f, 0f, z), size * 0.26f, mat);

                        for (int i = 0; i < 6; i++)
                        {
                            float a = i * 60f;
                            float rad = a * Mathf.Deg2Rad;
                            cube(parent, "icTooth" + i,
                                new Vector3(Mathf.Cos(rad) * size * 0.34f, Mathf.Sin(rad) * size * 0.34f, z),
                                new Vector3(size * 0.16f, size * 0.07f, 0.003f), mat)
                                .transform.localRotation = Quaternion.Euler(0f, 0f, a);
                        }

                        break;
                    }

                case SidebarCategoryIcon.Room:
                    {
                        cube(parent, "icDoorL", new Vector3(-size * 0.26f, 0f, z),
                            new Vector3(size * 0.07f, size * 0.66f, 0.003f), mat);
                        cube(parent, "icDoorR", new Vector3(size * 0.26f, 0f, z),
                            new Vector3(size * 0.07f, size * 0.66f, 0.003f), mat);
                        cube(parent, "icDoorTop", new Vector3(0f, size * 0.33f, z),
                            new Vector3(size * 0.59f, size * 0.07f, 0.003f), mat);
                        cube(parent, "icDoorBot", new Vector3(0f, -size * 0.33f, z),
                            new Vector3(size * 0.59f, size * 0.07f, 0.003f), mat);
                        ball(parent, "icKnob", new Vector3(size * 0.14f, -size * 0.02f, z - 0.002f),
                            size * 0.09f, mat);
                        break;
                    }

                case SidebarCategoryIcon.Movement:
                    {
                        cube(parent, "icShaft", new Vector3(-size * 0.06f, -size * 0.06f, z),
                            new Vector3(size * 0.52f, size * 0.10f, 0.003f), mat);
                        cube(parent, "icHeadA", new Vector3(size * 0.16f, size * 0.14f, z),
                            new Vector3(size * 0.34f, size * 0.09f, 0.003f), mat)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, 42f);
                        cube(parent, "icHeadB", new Vector3(size * 0.16f, -size * 0.14f, z),
                            new Vector3(size * 0.34f, size * 0.09f, 0.003f), mat)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, -42f);
                        break;
                    }

                default:
                    {
                        cube(parent, "icShieldT", new Vector3(0f, size * 0.16f, z),
                            new Vector3(size * 0.52f, size * 0.24f, 0.003f), mat);
                        cube(parent, "icShieldL", new Vector3(-size * 0.20f, -size * 0.06f, z),
                            new Vector3(size * 0.12f, size * 0.22f, 0.003f), mat)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                        cube(parent, "icShieldR", new Vector3(size * 0.20f, -size * 0.06f, z),
                            new Vector3(size * 0.12f, size * 0.22f, 0.003f), mat)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
                        cube(parent, "icCrossV", new Vector3(0f, size * 0.10f, z - 0.002f),
                            new Vector3(size * 0.09f, size * 0.22f, 0.003f), mat);
                        cube(parent, "icCrossH", new Vector3(0f, size * 0.10f, z - 0.002f),
                            new Vector3(size * 0.22f, size * 0.09f, 0.003f), mat);
                        break;
                    }
            }
        }

        private static Material _shared;
        private static Color _sharedColour;

        public static Material SidebarMaterial(Color color)
        {
            if (_shared != null && _sharedColour == color)
                return _shared;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");

            _shared = new Material(shader) { color = color };
            _sharedColour = color;
            return _shared;
        }
    }

    public readonly struct CategoryHubEntry
    {
        public readonly int buttonsCategoryIndex;
        public readonly string title;
        public readonly string toolTip;
        public readonly SidebarCategoryIcon Icon;

        public CategoryHubEntry(int buttonsCategoryIndex, string title, string toolTip, SidebarCategoryIcon icon)
        {
            this.buttonsCategoryIndex = buttonsCategoryIndex;
            this.title = title;
            this.toolTip = toolTip;
            Icon = icon;
        }
    }

    internal static class Category
    {
        public static readonly CategoryHubEntry Home = new CategoryHubEntry(
            0, "Main", "Opens the main page.", SidebarCategoryIcon.Home);

        private static readonly List<CategoryHubEntry> hub = new List<CategoryHubEntry>();

        private static bool built;

        public static CategoryHubEntry[] Hub
        {
            get
            {
                Build();
                return hub.ToArray();
            }
        }

        public static void Build()
        {
            if (built)
                return;

            built = true;
            hub.Clear();

            string[] names = iiMenu.Menu.Buttons.categoryNames;
            iiMenu.Classes.Menu.ButtonInfo[][] lists = iiMenu.Menu.Buttons.buttons;

            for (int i = 0; i < names.Length && i < lists.Length; i++)
            {
                string name = names[i];

                if (name == "Main" || name == "Internal Mods" || name == "Temporary Category" || name == "Chat Messages")
                    continue;

                if (name != "Settings" && name.EndsWith("Settings"))
                    continue;

                hub.Add(new CategoryHubEntry(i, name, "Opens the " + name + " tab.", IconFor(name)));
            }
        }

        public static SidebarCategoryIcon IconFor(string name)
        {
            if (name.Contains("Setting")) return SidebarCategoryIcon.Settings;
            if (name.Contains("Room")) return SidebarCategoryIcon.Room;
            if (name.Contains("Movement") || name.Contains("Projectile")) return SidebarCategoryIcon.Movement;
            if (name.Contains("Safety") || name.Contains("Anti")) return SidebarCategoryIcon.Safety;
            if (name.Contains("Visual")) return SidebarCategoryIcon.Visuals;
            if (name.Contains("Detected") || name.Contains("OP") || name.Contains("Overpowered") || name.Contains("Master")) return SidebarCategoryIcon.Detected;
            return SidebarCategoryIcon.Home;
        }
    }

    internal static class Buttons
    {
        private static ButtonInfo[][] matrix;

        public static ButtonInfo[][] buttons => matrix ??= Build();

        public static void Invalidate()
        {
            matrix = null;
        }

        private static ButtonInfo[][] Build()
        {
            string[] names = iiMenu.Menu.Buttons.categoryNames;
            iiMenu.Classes.Menu.ButtonInfo[][] source = iiMenu.Menu.Buttons.buttons;

            int count = Math.Max(names.Length, source.Length);
            iiMenu.CanvasMenuUI.Classes.ButtonInfo[][] result = new iiMenu.CanvasMenuUI.Classes.ButtonInfo[count][];

            for (int c = 0; c < count; c++)
            {
                string category = c < names.Length ? names[c] : "Category " + c;

                if (source[c].Length == 0)
                {
                    result[c] = Array.Empty<ButtonInfo>();
                    continue;
                }

                List<iiMenu.CanvasMenuUI.Classes.ButtonInfo> mapped = new List<iiMenu.CanvasMenuUI.Classes.ButtonInfo>(source[c].Length);

                foreach (iiMenu.Classes.Menu.ButtonInfo info in source[c])
                {
                    mapped.Add(Convert(info, category));
                }

                result[c] = mapped.ToArray();
            }

            return result;
        }

        private static iiMenu.CanvasMenuUI.Classes.ButtonInfo Convert(iiMenu.Classes.Menu.ButtonInfo info, string category)
        {
            string display = info.overlapText ?? info.buttonText;
            string key = info.buttonText;
            bool navigates = !info.isTogglable && info.enabled == false;

            if (navigates && !info.incremental)
            {
                int direct = TryCategoryIndex(key);

                if (direct >= 0)
                {
                    return new ButtonInfo
                    {
                        buttonText = display,
                        method = () => iiMenu.CanvasMenuUI.Main.Menu.currentCategory = direct,
                        isTogglable = false,
                        navTarget = direct,
                        toolTip = info.toolTip
                    };
                }

                int up = TryBackTarget(key);

                if (up >= 0)
                {
                    return new ButtonInfo
                    {
                        buttonText = display,
                        method = () => iiMenu.CanvasMenuUI.Main.Menu.currentCategory = up,
                        isTogglable = false,
                        navTarget = up,
                        toolTip = info.toolTip
                    };
                }
            }

            if (info.incremental)
            {
                return new ButtonInfo
                {
                    buttonText = display,
                    method = () => iiMenu.Menu.Main.ToggleIncremental(key, true, false),
                    enableMethod = () => iiMenu.Menu.Main.ToggleIncremental(key, true, false),
                    disableMethod = () => iiMenu.Menu.Main.ToggleIncremental(key, false, false),
                    isTogglable = false,
                    enabled = info.enabled,
                    toolTip = info.toolTip
                };
            }

            if (!info.isTogglable)
            {
                return new ButtonInfo
                {
                    buttonText = display,
                    method = () => iiMenu.Menu.Main.Toggle(key, false),
                    isTogglable = false,
                    enabled = info.enabled,
                    toolTip = info.toolTip
                };
            }

            return new ButtonInfo
            {
                buttonText = display,
                enableMethod = () => iiMenu.Menu.Main.SetButtonEnabled(key, true),
                disableMethod = () => iiMenu.Menu.Main.SetButtonEnabled(key, false),
                isTogglable = true,
                enabled = info.enabled,
                toolTip = info.toolTip
            };
        }

        private static int TryCategoryIndex(string name)
        {
            string[] names = iiMenu.Menu.Buttons.categoryNames;

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                    return i;
            }

            return -1;
        }

        private static int TryBackTarget(string name)
        {
            if (name.StartsWith("Exit ", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (name.StartsWith("Return to ", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (name.IndexOf("return to", StringComparison.OrdinalIgnoreCase) >= 0)
                return 0;

            return -1;
        }

        internal static void ExecuteEnabledMethods()
        {
        }

        internal static void SyncStates()
        {
            iiMenu.CanvasMenuUI.Classes.ButtonInfo[][] target = buttons;

            if (target == null)
                return;

            iiMenu.Classes.Menu.ButtonInfo[][] source = iiMenu.Menu.Buttons.buttons;
            string[] names = iiMenu.Menu.Buttons.categoryNames;

            for (int c = 0; c < target.Length && c < source.Length; c++)
            {
                for (int i = 0; i < target[c].Length && i < source[c].Length; i++)
                {
                    iiMenu.CanvasMenuUI.Classes.ButtonInfo view = target[c][i];
                    iiMenu.Classes.Menu.ButtonInfo origin = source[c][i];

                    if (view != null && origin != null && view.isTogglable)
                        view.enabled = origin.enabled;
                }
            }
        }
    }
}
