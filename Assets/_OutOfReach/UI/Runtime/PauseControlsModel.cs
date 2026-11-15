using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

namespace OutOfReach.Presentation.UI
{
    /// <summary>Which control set the pause menu explains. Mirrors the player's actual movement mode.</summary>
    public enum PauseControlsMode { Walking, TetherRoute, Floating }

    public enum PauseGlyphKind
    {
        /// <summary>A keyboard key cap labelled with <see cref="PauseGlyph.Text"/>.</summary>
        Key,
        /// <summary>A direction arrow key cap; <see cref="PauseGlyph.Text"/> is up, down, left or right.</summary>
        Arrow,
        /// <summary>A mouse with the named button(s) lit.</summary>
        Mouse,
        /// <summary>A mouse with motion marks: moving it, no button.</summary>
        MouseLook,
        /// <summary>A WASD-style direction cluster built from <see cref="PauseGlyph.Cluster"/>.</summary>
        Cluster
    }

    /// <summary>One pictogram: a key cap, a mouse or a direction cluster. Immutable.</summary>
    public sealed class PauseGlyph
    {
        public PauseGlyphKind Kind { get; private set; }
        public string Text { get; private set; }
        public bool Left { get; private set; }
        public bool Right { get; private set; }
        /// <summary>Cluster members in the order up, left, down, right; a null entry is not drawn.</summary>
        public IReadOnlyList<PauseGlyph> Cluster { get; private set; }

        private PauseGlyph() { }

        public static PauseGlyph Key(string text) => new PauseGlyph { Kind = PauseGlyphKind.Key, Text = text ?? string.Empty };
        public static PauseGlyph Arrow(string direction) => new PauseGlyph { Kind = PauseGlyphKind.Arrow, Text = direction };
        public static PauseGlyph Mouse(bool left, bool right) => new PauseGlyph { Kind = PauseGlyphKind.Mouse, Left = left, Right = right };
        public static PauseGlyph MouseLook() => new PauseGlyph { Kind = PauseGlyphKind.MouseLook };

        public static PauseGlyph DirectionCluster(PauseGlyph up, PauseGlyph left, PauseGlyph down, PauseGlyph right)
            => new PauseGlyph { Kind = PauseGlyphKind.Cluster, Cluster = new[] { up, left, down, right } };

        /// <summary>A stable text form used for de-duplication, change detection and tests.</summary>
        public string Signature
        {
            get
            {
                switch (Kind)
                {
                    case PauseGlyphKind.Mouse: return "mouse:" + (Left ? "L" : "") + (Right ? "R" : "");
                    case PauseGlyphKind.MouseLook: return "mouse:look";
                    case PauseGlyphKind.Cluster:
                        var text = new StringBuilder("cluster(");
                        for (int i = 0; i < Cluster.Count; i++)
                            text.Append(i == 0 ? "" : ",").Append(Cluster[i] == null ? "-" : Cluster[i].Signature);
                        return text.Append(')').ToString();
                    default: return Kind.ToString().ToLowerInvariant() + ":" + Text;
                }
            }
        }

        /// <summary>
        /// A glyph for one binding path, or null when it is not a keyboard key or a mouse button/motion
        /// (gamepad bindings are not pictured; the menu is a keyboard and mouse help).
        /// </summary>
        public static PauseGlyph FromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int slash = path.IndexOf('/');
            if (slash < 0 || slash + 1 >= path.Length) return null;
            string device = path.Substring(0, slash).ToLowerInvariant();
            string control = path.Substring(slash + 1);
            string lower = control.ToLowerInvariant();
            if (device == "<mouse>")
            {
                if (lower == "leftbutton") return Mouse(true, false);
                if (lower == "rightbutton") return Mouse(false, true);
                if (lower == "delta") return MouseLook();
                return null;
            }
            if (device != "<keyboard>") return null;
            switch (lower)
            {
                case "uparrow": return Arrow("up");
                case "downarrow": return Arrow("down");
                case "leftarrow": return Arrow("left");
                case "rightarrow": return Arrow("right");
                case "leftshift": case "rightshift": case "shift": return Key("SHIFT");
                case "leftctrl": case "rightctrl": case "ctrl": return Key("CTRL");
                case "leftalt": case "rightalt": case "alt": return Key("ALT");
                case "escape": return Key("ESC");
                case "space": return Key("SPACE");
                case "tab": return Key("TAB");
                case "enter": return Key("ENTER");
                case "backspace": return Key("BKSP");
            }
            string label = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return Key(string.IsNullOrWhiteSpace(label) ? control.ToUpperInvariant() : label.ToUpperInvariant());
        }
    }

    /// <summary>
    /// The player's keyboard and mouse bindings, read from the live <see cref="InputActionMap"/> so the menu always
    /// shows what the game actually listens to (rebinding is not offered, but an override would show).
    /// Actions are found by name: Move, Interact, Sprint, Pause, LookPointer (see PlayerInputActions).
    /// </summary>
    public sealed class PauseBindings
    {
        public const string MoveAction = "Move";
        public const string InteractAction = "Interact";
        public const string SprintAction = "Sprint";
        public const string PauseAction = "Pause";
        public const string LookAction = "LookPointer";

        /// <summary>Direction clusters in binding order (e.g. WASD then the arrow keys).</summary>
        public IReadOnlyList<PauseGlyph> Move { get; private set; } = Array.Empty<PauseGlyph>();
        public IReadOnlyList<PauseGlyph> Interact { get; private set; } = Array.Empty<PauseGlyph>();
        public IReadOnlyList<PauseGlyph> Sprint { get; private set; } = Array.Empty<PauseGlyph>();
        public IReadOnlyList<PauseGlyph> Pause { get; private set; } = Array.Empty<PauseGlyph>();
        public bool MouseLooks { get; private set; }
        /// <summary>True when no live map was available and the documented defaults stand in.</summary>
        public bool IsFallback { get; private set; }

        public string Signature
        {
            get
            {
                var text = new StringBuilder(IsFallback ? "fallback|" : "live|");
                Append(text, Move); Append(text, Interact); Append(text, Sprint); Append(text, Pause);
                return text.Append(MouseLooks ? "look" : "nolook").ToString();
            }
        }

        private static void Append(StringBuilder text, IReadOnlyList<PauseGlyph> glyphs)
        {
            foreach (var glyph in glyphs) text.Append(glyph.Signature).Append(';');
            text.Append('|');
        }

        /// <summary>Reads the bindings of <paramref name="map"/>; a missing map or action uses the default for that action.</summary>
        public static PauseBindings FromMap(InputActionMap map)
        {
            var result = new PauseBindings();
            if (map == null) return Fallback();
            var move = map.FindAction(MoveAction, false);
            var interact = map.FindAction(InteractAction, false);
            var sprint = map.FindAction(SprintAction, false);
            var pause = map.FindAction(PauseAction, false);
            var look = map.FindAction(LookAction, false);
            var defaults = Fallback();
            result.Move = move != null ? ReadClusters(move) : defaults.Move;
            result.Interact = interact != null ? ReadGlyphs(interact) : defaults.Interact;
            result.Sprint = sprint != null ? ReadGlyphs(sprint) : defaults.Sprint;
            result.Pause = pause != null ? ReadGlyphs(pause) : defaults.Pause;
            result.MouseLooks = look != null ? HasMouseLook(look) : defaults.MouseLooks;
            result.IsFallback = move == null || interact == null || sprint == null || pause == null || look == null;
            return result;
        }

        /// <summary>
        /// Stand-in used only when no player input map exists (for example a scripted test source). These repeat the
        /// defaults documented on PlayerInputActions and are never preferred over a live map.
        /// </summary>
        public static PauseBindings Fallback()
        {
            return new PauseBindings
            {
                Move = new[]
                {
                    PauseGlyph.DirectionCluster(PauseGlyph.Key("W"), PauseGlyph.Key("A"), PauseGlyph.Key("S"), PauseGlyph.Key("D")),
                    PauseGlyph.DirectionCluster(PauseGlyph.Arrow("up"), PauseGlyph.Arrow("left"), PauseGlyph.Arrow("down"), PauseGlyph.Arrow("right"))
                },
                Interact = new[] { PauseGlyph.Key("E"), PauseGlyph.Mouse(true, false) },
                Sprint = new[] { PauseGlyph.Key("SHIFT") },
                Pause = new[] { PauseGlyph.Key("ESC") },
                MouseLooks = true,
                IsFallback = true
            };
        }

        private static List<PauseGlyph> ReadGlyphs(InputAction action)
        {
            var glyphs = new List<PauseGlyph>();
            var seen = new HashSet<string>();
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite) continue;
                var glyph = PauseGlyph.FromPath(binding.effectivePath);
                if (glyph != null && glyph.Kind != PauseGlyphKind.MouseLook && seen.Add(glyph.Signature)) glyphs.Add(glyph);
            }
            return glyphs;
        }

        private static bool HasMouseLook(InputAction action)
        {
            foreach (var binding in action.bindings)
            {
                var glyph = PauseGlyph.FromPath(binding.effectivePath);
                if (glyph != null && glyph.Kind == PauseGlyphKind.MouseLook) return true;
            }
            return false;
        }

        private static List<PauseGlyph> ReadClusters(InputAction action)
        {
            var clusters = new List<PauseGlyph>();
            var seen = new HashSet<string>();
            PauseGlyph up = null, left = null, down = null, right = null;
            bool open = false;
            void Close()
            {
                if (!open) return;
                open = false;
                if (up == null && left == null && down == null && right == null) return;
                var cluster = PauseGlyph.DirectionCluster(up, left, down, right);
                if (seen.Add(cluster.Signature)) clusters.Add(cluster);
                up = left = down = right = null;
            }

            foreach (var binding in action.bindings)
            {
                if (binding.isComposite) { Close(); open = true; continue; }
                if (!binding.isPartOfComposite) { Close(); continue; }
                var glyph = PauseGlyph.FromPath(binding.effectivePath);
                if (glyph == null) continue;
                switch ((binding.name ?? string.Empty).ToLowerInvariant())
                {
                    case "up": up = glyph; break;
                    case "left": left = glyph; break;
                    case "down": down = glyph; break;
                    case "right": right = glyph; break;
                }
            }
            Close();
            return clusters;
        }
    }

    /// <summary>One pause-menu line: the pictograms that perform it and what they do.</summary>
    public sealed class PauseControlRow
    {
        public PauseControlRow(string label, IReadOnlyList<IReadOnlyList<PauseGlyph>> alternatives, string note = null)
        {
            Label = label;
            Alternatives = alternatives;
            Note = note;
        }

        public string Label { get; }
        /// <summary>Equivalent ways to do it (shown with "/"); each is a chord of glyphs held or pressed together (shown with "+").</summary>
        public IReadOnlyList<IReadOnlyList<PauseGlyph>> Alternatives { get; }
        /// <summary>An optional constraint shown beneath the label.</summary>
        public string Note { get; }

        public string Signature
        {
            get
            {
                var text = new StringBuilder(Label).Append('=');
                foreach (var chord in Alternatives)
                {
                    foreach (var glyph in chord) text.Append(glyph.Signature).Append('+');
                    text.Append('/');
                }
                return text.Append(Note).ToString();
            }
        }
    }

    /// <summary>
    /// Builds the pause-menu rows for a movement mode. Bindings that exist as Input System actions (move, look,
    /// interact, sprint, pause) come from <see cref="PauseBindings"/>. The remaining inputs are read directly from the
    /// devices in code and are named here once: Tab (MissionFlightBoard, hold for systems), and in microgravity the
    /// mouse buttons, Space and Ctrl (I06MicrogravityInputBindings).
    /// </summary>
    public static class PauseControlsModel
    {
        public const string SystemsKey = "TAB";
        public const string FootPushKey = "SPACE";
        public const string RailGripKey = "CTRL";

        public const string ModeLabelWalking = "On foot";
        public const string ModeLabelRoute = "Tether route";
        public const string ModeLabelFloating = "Floating";

        public static string ModeLabel(PauseControlsMode mode)
        {
            return mode == PauseControlsMode.Floating ? ModeLabelFloating
                : mode == PauseControlsMode.TetherRoute ? ModeLabelRoute : ModeLabelWalking;
        }

        public static IReadOnlyList<PauseControlRow> Build(PauseControlsMode mode, bool railGrip, PauseBindings bindings)
        {
            bindings = bindings ?? PauseBindings.Fallback();
            var rows = new List<PauseControlRow>();
            if (mode == PauseControlsMode.Floating) BuildFloating(rows, railGrip, bindings);
            else BuildOnFoot(rows, mode == PauseControlsMode.TetherRoute, bindings);
            rows.Add(new PauseControlRow("Systems · hold", One(PauseGlyph.Key(SystemsKey))));
            rows.Add(new PauseControlRow("Resume", Each(bindings.Pause)));
            return rows;
        }

        private static void BuildOnFoot(List<PauseControlRow> rows, bool route, PauseBindings bindings)
        {
            if (route)
            {
                var vertical = new List<PauseGlyph>();
                foreach (var cluster in bindings.Move) vertical.Add(VerticalOnly(cluster));
                rows.Add(new PauseControlRow("Move along the tether", Each(vertical)));
            }
            else rows.Add(new PauseControlRow("Move", Each(bindings.Move)));
            rows.Add(new PauseControlRow("Look", One(bindings.MouseLooks ? PauseGlyph.MouseLook() : PauseGlyph.Mouse(false, false))));
            if (!route) rows.Add(new PauseControlRow("Sprint · hold", Each(bindings.Sprint)));
            rows.Add(new PauseControlRow("Interact", Each(bindings.Interact)));
        }

        private static void BuildFloating(List<PauseControlRow> rows, bool railGrip, PauseBindings bindings)
        {
            rows.Add(new PauseControlRow("Look", One(bindings.MouseLooks ? PauseGlyph.MouseLook() : PauseGlyph.Mouse(false, false))));
            // Floating has no thrust: the mouse buttons push off a surface within reach.
            rows.Add(new PauseControlRow("Push off a surface in reach",
                Merge(One(PauseGlyph.Mouse(true, false)), One(PauseGlyph.Mouse(false, true)))));
            rows.Add(new PauseControlRow("Push with both hands", One(PauseGlyph.Mouse(true, true))));
            if (bindings.Move.Count > 0)
                rows.Add(new PauseControlRow("Aim the push · hold while clicking",
                    Chords(new PauseGlyph[] { bindings.Move[0], PauseGlyph.Mouse(true, false) })));
            rows.Add(new PauseControlRow("Foot push", One(PauseGlyph.Key(FootPushKey)), "Only while your feet touch support"));
            if (railGrip)
                rows.Add(new PauseControlRow("Grip a reachable rail · click to release",
                    Chords(new PauseGlyph[] { PauseGlyph.Key(RailGripKey), PauseGlyph.Mouse(true, false) })));
            var keys = new List<PauseGlyph>();
            foreach (var glyph in bindings.Interact) if (glyph.Kind == PauseGlyphKind.Key) keys.Add(glyph);
            rows.Add(new PauseControlRow("Use equipment", Each(keys)));
        }

        private static PauseGlyph VerticalOnly(PauseGlyph cluster)
        {
            var c = cluster.Cluster;
            return PauseGlyph.DirectionCluster(c[0], null, c[2], null);
        }

        private static IReadOnlyList<IReadOnlyList<PauseGlyph>> One(PauseGlyph glyph)
            => new IReadOnlyList<PauseGlyph>[] { new[] { glyph } };

        private static IReadOnlyList<IReadOnlyList<PauseGlyph>> Merge(params IReadOnlyList<IReadOnlyList<PauseGlyph>>[] groups)
        {
            var list = new List<IReadOnlyList<PauseGlyph>>();
            foreach (var group in groups) list.AddRange(group);
            return list;
        }

        private static IReadOnlyList<IReadOnlyList<PauseGlyph>> Chords(params PauseGlyph[][] chords)
        {
            var list = new List<IReadOnlyList<PauseGlyph>>();
            foreach (var chord in chords) list.Add(chord);
            return list;
        }

        /// <summary>Each glyph is an alternative; an empty list shows an unbound key cap rather than nothing.</summary>
        private static IReadOnlyList<IReadOnlyList<PauseGlyph>> Each(IReadOnlyList<PauseGlyph> glyphs)
        {
            var list = new List<IReadOnlyList<PauseGlyph>>();
            foreach (var glyph in glyphs) list.Add(new[] { glyph });
            if (list.Count == 0) list.Add(new[] { PauseGlyph.Key("—") });
            return list;
        }
    }
}
