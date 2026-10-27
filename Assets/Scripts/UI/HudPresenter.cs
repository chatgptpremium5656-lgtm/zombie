using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zombie.Core;
using Zombie.Player;

namespace Zombie.UI
{
    /// <summary>
    /// Style probe HUD: health bar, 6-slot hotbar and crosshair. Panels slide and fade in with a
    /// staggered ease-out-back, the selected slot pops on a spring, the crosshair ring kicks on swings.
    /// </summary>
    public sealed class HudPresenter : MonoBehaviour
    {
        [System.Serializable]
        public struct Panel
        {
            public CanvasGroup Group;
            public RectTransform Rect;
            public Vector2 Offset;
            public float Delay;
        }

        [SerializeField] Panel[] _panels;
        [SerializeField] float _introDuration = 0.65f;

        [Header("Health")]
        [SerializeField] RectTransform _healthFill;
        [SerializeField] RectTransform _healthTrail;
        [SerializeField] float _healthBarWidth = 300f;
        [SerializeField] Text _healthText;
        [SerializeField] RectTransform _heart;

        [Header("Hotbar")]
        [SerializeField] RectTransform[] _slots;
        [SerializeField] Graphic[] _slotHighlights;
        [SerializeField] Image[] _slotBackgrounds;
        [SerializeField] Color _slotColor = new Color(0.12f, 0.11f, 0.14f, 0.72f);
        [SerializeField] Color _slotSelectedColor = new Color(0.27f, 0.23f, 0.2f, 0.85f);

        [Header("Crosshair")]
        [SerializeField] RectTransform _crosshairRing;
        [SerializeField] ViewModelAnimator _viewModel;

        Vector2[] _panelTargets;
        float _time;
        bool _introDone;
        float _health = 100f;
        float _shownHealth = 100f;
        float _trailHealth = 100f;
        int _selected;
        Spring[] _slotScale;
        Spring _ring;

        public void SetHealth(float value)
        {
            _health = Mathf.Clamp(value, 0f, 100f);
        }

        void Awake()
        {
            _panelTargets = new Vector2[_panels.Length];
            for (int i = 0; i < _panels.Length; i++)
            {
                _panelTargets[i] = _panels[i].Rect.anchoredPosition;
                _panels[i].Group.alpha = 0f;
                _panels[i].Rect.anchoredPosition = _panelTargets[i] + _panels[i].Offset;
            }
            _slotScale = new Spring[_slots.Length];
            for (int i = 0; i < _slotScale.Length; i++)
                _slotScale[i].Value = 1f;
            ApplySelection(0, false);
        }

        void OnEnable()
        {
            if (_viewModel == null)
                return;
            _viewModel.SwingStarted += OnSwing;
            _viewModel.HitLanded += OnHit;
        }

        void OnDisable()
        {
            if (_viewModel == null)
                return;
            _viewModel.SwingStarted -= OnSwing;
            _viewModel.HitLanded -= OnHit;
        }

        void OnSwing() => _ring.Velocity += 6f;

        void OnHit() => _ring.Velocity += 14f;

        void Update()
        {
            // Clamp so a long first frame (scene load) does not skip the intro.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _time += dt;
            AnimateIntro();
            ReadHotbarInput();
            AnimateHotbar(dt);
            AnimateHealth(dt);

            _ring.Step(0f, 5f, 0.4f, dt);
            float ringScale = 1f + _ring.Value * 0.04f;
            _crosshairRing.localScale = new Vector3(ringScale, ringScale, 1f);
        }

        void AnimateIntro()
        {
            if (_introDone)
                return;
            bool done = true;
            for (int i = 0; i < _panels.Length; i++)
            {
                float t = Mathf.Clamp01((_time - _panels[i].Delay) / _introDuration);
                done &= t >= 1f;
                _panels[i].Group.alpha = Ease.OutCubic(t * 1.4f);
                _panels[i].Rect.anchoredPosition = _panelTargets[i] + _panels[i].Offset * (1f - Ease.OutBack(t, 1.4f));
            }
            _introDone = done;
        }

        void ReadHotbarInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int i = 0; i < _slots.Length && i < 9; i++)
                {
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                        ApplySelection(i, true);
                }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f)
                    ApplySelection((_selected + _slots.Length - 1) % _slots.Length, true);
                else if (scroll < -0.1f)
                    ApplySelection((_selected + 1) % _slots.Length, true);
            }
        }

        void ApplySelection(int index, bool pop)
        {
            _selected = index;
            for (int i = 0; i < _slots.Length; i++)
            {
                bool selected = i == index;
                _slotHighlights[i].enabled = selected;
                _slotBackgrounds[i].color = selected ? _slotSelectedColor : _slotColor;
            }
            if (pop)
                _slotScale[index].Velocity += 4.5f;
        }

        void AnimateHotbar(float dt)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                float target = i == _selected ? 1.1f : 1f;
                _slotScale[i].Step(target, 4.2f, 0.42f, dt);
                float s = _slotScale[i].Value;
                _slots[i].localScale = new Vector3(s, s, 1f);
            }
        }

        void AnimateHealth(float dt)
        {
            _shownHealth = Mathf.Lerp(_shownHealth, _health, Ease.Damp(14f, dt));
            _trailHealth = _trailHealth > _shownHealth ? Mathf.Lerp(_trailHealth, _shownHealth, Ease.Damp(3f, dt)) : _shownHealth;
            SetWidth(_healthFill, _shownHealth);
            SetWidth(_healthTrail, _trailHealth);
            _healthText.text = CachedNumber(Mathf.RoundToInt(_shownHealth));

            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 3.4f)), 12f);
            float hs = 1f + beat * 0.08f;
            _heart.localScale = new Vector3(hs, hs, 1f);
        }

        void SetWidth(RectTransform rect, float health)
        {
            float minWidth = rect.rect.height;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(minWidth, _healthBarWidth * health / 100f));
        }

        static readonly string[] Numbers = BuildNumbers();

        static string[] BuildNumbers()
        {
            var numbers = new string[101];
            for (int i = 0; i <= 100; i++)
                numbers[i] = i.ToString();
            return numbers;
        }

        static string CachedNumber(int value) => Numbers[Mathf.Clamp(value, 0, 100)];
    }
}
