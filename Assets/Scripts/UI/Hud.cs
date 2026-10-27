using UnityEngine;
using Zombie.Building;
using Zombie.Core;
using Zombie.Logic;
using Zombie.Player;

namespace Zombie.UI
{
    /// <summary>
    /// Prototype HUD and menus drawn with IMGUI: health, resources, hotbar, day/night clock, horde info,
    /// building hints, craft menu (Tab), pause (Esc) and the death screen.
    /// Text that changes rarely is cached and rebuilt only when its numbers change.
    /// Will be replaced by a proper UI Toolkit HUD once the gameplay is locked.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        PlayerAvatar _player;
        bool _craftOpen;
        bool _pauseOpen;
        bool _showHelp = true;

        GUIStyle _label;
        GUIStyle _labelCenter;
        GUIStyle _big;
        GUIStyle _small;
        GUIStyle _button;
        GUIStyle _buttonCenter;
        Texture2D _white;

        // Cached strings.
        int _resVersion = -1;
        string _resText;
        int _clockKey = -1;
        string _clockText;
        int _hordeKey = -1;
        string _hordeText;
        int _hpKey = -1;
        string _hpText;
        int _hotbarVersion = -1;
        readonly System.Collections.Generic.List<string> _hotbarText = new System.Collections.Generic.List<string>();
        int _craftVersion = -1;
        readonly string[] _craftLabels = new string[ItemCatalog.Count];
        int _deathSecs = -1;
        string _deathText;
        int _hintKey = -1;
        string _hintReason;
        string _hintText;

        static readonly Color Panel = new Color(0.08f, 0.09f, 0.08f, 0.72f);
        static readonly Color HealthColor = new Color(0.78f, 0.22f, 0.2f);
        static readonly Color Accent = new Color(1f, 0.82f, 0.4f);

        public void Init(PlayerAvatar player)
        {
            _player = player;
            SetCursor(false);
        }

        static void SetCursor(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        void Update()
        {
            if (GameInput.Pressed(GameInput.Button.Pause))
            {
                if (_craftOpen) _craftOpen = false;
                else
                {
                    _pauseOpen = !_pauseOpen;
                    Game.SetPaused(_pauseOpen);
                }
            }

            if (!_pauseOpen && !_player.IsDead && GameInput.Pressed(GameInput.Button.CraftMenu)) _craftOpen = !_craftOpen;
            if (_player.IsDead) _craftOpen = false;

            bool uiOpen = _craftOpen || _pauseOpen;
            if (uiOpen != Game.UiOpen)
            {
                Game.UiOpen = uiOpen;
                SetCursor(uiOpen);
            }

            // Clicking back into the game window re-locks the cursor (macOS releases it on focus loss).
            if (!uiOpen && Cursor.lockState != CursorLockMode.Locked && GameInput.PrimaryPressed) SetCursor(false);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && !Game.UiOpen) SetCursor(false);
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _white = Texture2D.whiteTexture;
            int baseSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 45f, 13f, 26f));
            _label = new GUIStyle(GUI.skin.label) { fontSize = baseSize, richText = true };
            _label.normal.textColor = Color.white;
            _labelCenter = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _big = new GUIStyle(_labelCenter) { fontSize = baseSize * 2, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(baseSize * 0.8f) };
            _button = new GUIStyle(GUI.skin.button) { fontSize = baseSize, alignment = TextAnchor.MiddleLeft, richText = true };
            _buttonCenter = new GUIStyle(_button) { alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (_player == null) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;
            float unit = _label.fontSize;

            DrawCrosshair(w, h);
            DrawTopLeft(unit);
            DrawClock(w, unit);
            DrawHealth(w, h, unit);
            DrawHotbar(w, h, unit);
            DrawMessages(w, h, unit);
            DrawAim(w, h, unit);
            DrawBuildHint(w, h, unit);
            if (_showHelp) DrawHelp(w, h, unit);
            if (_craftOpen) DrawCraftMenu(w, h, unit);
            if (_pauseOpen) DrawPause(w, h, unit);
            if (_player.IsDead) DrawDeath(w, h);
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        void DrawCrosshair(float w, float h)
        {
            if (Game.UiOpen || _player.IsDead) return;
            Fill(new Rect(w * 0.5f - 1f, h * 0.5f - 7f, 2f, 14f), new Color(1, 1, 1, 0.8f));
            Fill(new Rect(w * 0.5f - 7f, h * 0.5f - 1f, 14f, 2f), new Color(1, 1, 1, 0.8f));
        }

        void DrawTopLeft(float unit)
        {
            var inv = _player.Inventory;
            if (_resVersion != inv.Version)
            {
                _resVersion = inv.Version;
                _resText = "Дерево  <b>" + inv.Get(ResourceType.Wood) + "</b>\nКамень  <b>" + inv.Get(ResourceType.Stone) +
                           "</b>\nМеталл  <b>" + inv.Get(ResourceType.Metal) + "</b>";
            }
            var r = new Rect(12f, 12f, unit * 9f, unit * 4.2f);
            Fill(r, Panel);
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, r.height), _resText, _label);
        }

        void DrawClock(float w, float unit)
        {
            var dn = Game.DayNight;
            var horde = Game.Horde;
            if (dn == null || horde == null) return;

            int secs = Mathf.CeilToInt(dn.PhaseTimeLeft);
            int key = (dn.Day * 2 + (dn.IsNight ? 1 : 0)) * 100000 + secs;
            if (key != _clockKey)
            {
                _clockKey = key;
                string phase = dn.IsNight ? "Ночь " + dn.Day : "День " + dn.Day;
                string until = dn.IsNight ? "до рассвета" : "до ночи";
                _clockText = "<b>" + phase + "</b>   " + until + " " + (secs / 60) + ":" + (secs % 60).ToString("00");
            }

            int hordeKey = horde.Alive * 10000 + horde.BestNight * 10 + (dn.IsNight ? 1 : 0);
            if (hordeKey != _hordeKey)
            {
                _hordeKey = hordeKey;
                _hordeText = (dn.IsNight ? "Зомби: " + horde.Alive + "   " : "") + "Рекорд: " + horde.BestNight + " ноч.";
            }

            var r = new Rect(w * 0.5f - unit * 11f, 10f, unit * 22f, unit * 3.4f);
            Fill(r, Panel);
            GUI.Label(new Rect(r.x, r.y + 4f, r.width, unit * 1.5f), _clockText, _labelCenter);
            GUI.Label(new Rect(r.x, r.y + unit * 1.6f, r.width, unit * 1.5f), _hordeText, _labelCenter);

            if (!dn.IsNight && dn.PhaseTimeLeft < Game.Config.hordeWarningSeconds)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
                var old = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.35f, pulse);
                GUI.Label(new Rect(r.x, r.yMax + 4f, r.width, unit * 1.6f), "<b>Орда приближается! Готовьте оборону</b>", _labelCenter);
                GUI.color = old;
            }
        }

        void DrawHealth(float w, float h, float unit)
        {
            var hp = _player.Health;
            int value = Mathf.CeilToInt(hp.Current);
            if (value != _hpKey)
            {
                _hpKey = value;
                _hpText = "Здоровье " + value;
            }
            var r = new Rect(12f, h - unit * 2.6f - 12f, unit * 14f, unit * 2.6f);
            Fill(r, Panel);
            var bar = new Rect(r.x + 8f, r.y + unit * 1.45f, r.width - 16f, unit * 0.8f);
            Fill(bar, new Color(0.2f, 0.05f, 0.05f, 0.9f));
            Fill(new Rect(bar.x, bar.y, bar.width * hp.Normalized, bar.height), HealthColor);
            GUI.Label(new Rect(r.x + 10f, r.y + 2f, r.width, unit * 1.4f), _hpText, _small);

            // Red flash at the screen edges when hurt.
            if (hp.Normalized < 0.35f && hp.IsAlive)
            {
                float a = (0.35f - hp.Normalized) * 0.8f;
                Fill(new Rect(0, 0, w, 10f), new Color(0.8f, 0f, 0f, a));
                Fill(new Rect(0, h - 10f, w, 10f), new Color(0.8f, 0f, 0f, a));
            }
        }

        void DrawHotbar(float w, float h, float unit)
        {
            var tools = _player.Tools;
            var bar = tools.Hotbar;
            if (bar.Count == 0) return;
            float slotW = unit * 7.2f, slotH = unit * 2.4f, gap = 6f;
            float total = bar.Count * slotW + (bar.Count - 1) * gap;
            float x = w * 0.5f - total * 0.5f;
            float y = h - slotH - 12f;
            if (_hotbarVersion != _player.Inventory.Version || _hotbarText.Count != bar.Count)
            {
                _hotbarVersion = _player.Inventory.Version;
                _hotbarText.Clear();
                for (int i = 0; i < bar.Count; i++)
                {
                    var d = ItemCatalog.Get(bar[i]);
                    int count = _player.Inventory.Count(bar[i]);
                    _hotbarText.Add("<b>" + (i + 1) + "</b> " + d.Name + (d.Kind == ItemKind.Placeable ? " ×" + count : ""));
                }
            }
            for (int i = 0; i < bar.Count; i++)
            {
                var id = bar[i];
                var r = new Rect(x + i * (slotW + gap), y, slotW, slotH);
                Fill(r, id == tools.Selected ? new Color(0.45f, 0.36f, 0.16f, 0.9f) : Panel);
                if (id == tools.Selected) Fill(new Rect(r.x, r.y, r.width, 3f), Accent);
                GUI.Label(new Rect(r.x + 6f, r.y, r.width - 8f, r.height), _hotbarText[i], _small);
            }
        }

        void DrawMessages(float w, float h, float unit)
        {
            for (int i = 0; i < 4; i++)
            {
                string msg = _player.GetMessage(i, out float alpha);
                if (msg == null) continue;
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.Label(new Rect(w * 0.5f + unit * 2f, h * 0.5f + unit * (1.5f + i * 1.4f), unit * 24f, unit * 1.4f), msg, _label);
                GUI.color = old;
            }
        }

        void DrawAim(float w, float h, float unit)
        {
            var text = _player.Tools.AimText;
            if (string.IsNullOrEmpty(text) || Game.UiOpen) return;
            GUI.Label(new Rect(w * 0.5f - unit * 20f, h * 0.5f - unit * 3f, unit * 40f, unit * 1.5f), text, _labelCenter);
        }

        void DrawBuildHint(float w, float h, float unit)
        {
            var tools = _player.Tools;
            if (Game.UiOpen) return;
            var reason = tools.LastPlacement.Valid ? null : tools.LastPlacement.Reason;
            // Reasons are string literals, so a reference compare is enough and allocates nothing.
            int key = (int)tools.Selected * 100 + (int)tools.PlanMode;
            if (key == _hintKey && ReferenceEquals(reason, _hintReason))
            {
                if (_hintText != null) GUI.Label(new Rect(w * 0.5f - unit * 22f, h - unit * 7.4f, unit * 44f, unit * 3f), _hintText, _labelCenter);
                return;
            }
            _hintKey = key;
            _hintReason = reason;
            string hint = null;
            if (tools.Selected == ItemId.BuildingPlan)
            {
                var p = tools.LastPlacement;
                string mode = BuildingSystem.ModeName(tools.PlanMode);
                string cost = BuildingSystem.CostText(BuildingSystem.ModeCost(tools.PlanMode));
                hint = "<b>" + mode + "</b> (" + cost + ")   [ЛКМ] поставить   [ПКМ] сменить   [R] повернуть" +
                       (p.Valid || p.Reason == null ? "" : "\n<color=#ff8a7a>" + p.Reason + "</color>");
            }
            else if (tools.Selected == ItemId.Hammer)
            {
                hint = "[ЛКМ] чинить   [ПКМ] улучшить: солома → дерево → камень → металл";
            }
            else if (tools.Selected == ItemId.SleepingBag || tools.Selected == ItemId.WoodenSpikes)
            {
                var p = tools.LastPlacement;
                hint = "[ЛКМ] поставить" + (p.Valid || p.Reason == null ? "" : "   <color=#ff8a7a>" + p.Reason + "</color>");
            }
            _hintText = hint;
            if (hint == null) return;
            GUI.Label(new Rect(w * 0.5f - unit * 22f, h - unit * 7.4f, unit * 44f, unit * 3f), hint, _labelCenter);
        }

        void DrawHelp(float w, float h, float unit)
        {
            const string help =
                "<b>Управление</b>\nWASD — ходить, Shift — бег, Space — прыжок\nЛКМ — бить / добывать / строить\n" +
                "1-9 или колесо — предметы\nTab или C — крафт\nE — дверь, F — фонарик\nEsc — пауза\nH — скрыть подсказку";
            var r = new Rect(w - unit * 17f - 12f, 12f, unit * 17f, unit * 10.5f);
            Fill(r, Panel);
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 16f, r.height), help, _small);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.H) _showHelp = false;
        }

        void DrawCraftMenu(float w, float h, float unit)
        {
            var inv = _player.Inventory;
            float rowH = unit * 2.2f;
            int rows = ItemCatalog.Count - 1;
            var r = new Rect(w * 0.5f - unit * 18f, h * 0.5f - (rows * rowH + unit * 4f) * 0.5f, unit * 36f, rows * rowH + unit * 4f);
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.35f));
            Fill(r, new Color(0.1f, 0.1f, 0.09f, 0.95f));
            GUI.Label(new Rect(r.x, r.y + 6f, r.width, unit * 1.8f), "<b>Крафт</b>   (Tab — закрыть)", _labelCenter);

            if (_craftVersion != inv.Version)
            {
                _craftVersion = inv.Version;
                for (int i = 0; i < ItemCatalog.Count; i++)
                {
                    var d = ItemCatalog.GetByIndex(i);
                    bool owned = d.Kind != ItemKind.Placeable && inv.Has(d.Id);
                    string status = owned ? "<color=#9fd39a>есть</color>" : BuildingSystem.CostText(d.Recipe);
                    _craftLabels[i] = "<b>" + d.Name + "</b>   " + status;
                }
            }

            float y = r.y + unit * 2.6f;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                var def = ItemCatalog.GetByIndex(i);
                if (!ItemCatalog.IsCraftable(def.Id)) continue;
                bool can = inv.CanCraft(def.Id);
                string label = _craftLabels[i];
                GUI.enabled = can;
                if (GUI.Button(new Rect(r.x + 12f, y, r.width - 24f, rowH - 6f), label, _button))
                {
                    if (inv.TryCraft(def.Id)) _player.Notify("Создано: " + def.Name);
                }
                GUI.enabled = true;
                y += rowH;
            }
        }

        void DrawPause(float w, float h, float unit)
        {
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.55f));
            GUI.Label(new Rect(0, h * 0.3f, w, unit * 3f), "Пауза", _big);
            var b = new Rect(w * 0.5f - unit * 8f, h * 0.45f, unit * 16f, unit * 2.4f);
            if (GUI.Button(b, "Продолжить", _buttonCenter))
            {
                _pauseOpen = false;
                Game.SetPaused(false);
            }
            b.y += unit * 3f;
            if (GUI.Button(b, "Выйти из игры", _buttonCenter))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        void DrawDeath(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(0.25f, 0f, 0f, 0.5f));
            GUI.Label(new Rect(0, h * 0.35f, w, _big.fontSize * 1.6f), "Вы погибли", _big);
            int secs = Mathf.CeilToInt(_player.RespawnTimeLeft);
            if (secs != _deathSecs)
            {
                _deathSecs = secs;
                _deathText = "Всё, что было с собой, потеряно. Возрождение через " + secs;
            }
            GUI.Label(new Rect(0, h * 0.35f + _big.fontSize * 1.8f, w, _label.fontSize * 1.6f), _deathText, _labelCenter);
        }
    }
}
