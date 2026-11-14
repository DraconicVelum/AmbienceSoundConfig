using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AmbienceSoundConfig
{
    [HarmonyPatch]
    public static class SettingsInject
    {
        private sealed class Category
        {
            internal string Title;
            internal SoundGroup? Group;

            internal bool? ExtraClips;
            internal GameObject Body;
            internal TextMeshProUGUI ToggleText;
            internal TextMeshProUGUI Status;
            internal bool Expanded;
            internal ConfigEntry<float> Entry;
            internal TextMeshProUGUI MuteText;
            internal float RestoreVolume;
            internal int BuiltRevision = -1;
        }

        private sealed class Binding
        {
            internal Slider Slider;
            internal TextMeshProUGUI Value;
            internal ConfigEntry<float> Entry;
            internal int Percentage = int.MinValue;
        }

        private static readonly List<Category> Categories = new List<Category>();
        private static readonly List<Binding> Bindings = new List<Binding>();
        private static readonly List<TMP_InputField> Inputs = new List<TMP_InputField>();
        private static GameObject root;
        private static RectTransform content;
        private static TMP_Text textTemplate;
        private static TMP_Text valueTemplate;
        private static Slider sliderTemplate;
        private static Button buttonTemplate;
        private static Scrollbar scrollbarTemplate;
        private static GameObject cloneStaging;
        private static int renderedRevision = -1;
        private static string pendingFocus;
        private static Toggle nativeToggle;
        private static Button backButton;
        private static Button okButton;
        private static GameObject lastSelected;
        private static RectTransform ownerRect;
        private static RectTransform lastNativeRow;
        private static RectTransform layoutHost;
        private static Vector2 lastPanelSize;
        private static bool wasVisible;
        private static Vector2 lastScreenSize, lastOwnerSize, lastHostSize;
        private static readonly Vector3[] Corners = new Vector3[4];

        private static MethodBase TargetMethod()
        {
            var type = typeof(Valheim.SettingsGui.AudioSettings);
            return AccessTools.Method(type, "Initialize") ?? AccessTools.Method(type, "LoadSettings") ??
                throw new MissingMethodException("No compatible AudioSettings initializer found.");
        }

        private static void Postfix(Valheim.SettingsGui.AudioSettings __instance)
        {
            try
            {
                if (root != null && root.transform.IsChildOf(__instance.transform)) { SyncUI(); return; }
                if (root != null) UnityEngine.Object.Destroy(root);
                Categories.Clear(); Bindings.Clear(); Inputs.Clear();
                var music = Traverse.Create(__instance).Field("m_musicVolumeSlider").GetValue<Slider>();
                var continuous = Traverse.Create(__instance).Field("m_continousMusic").GetValue<Toggle>();
                nativeToggle = continuous;
                var text = Traverse.Create(__instance).Field("m_musicVolumeText").GetValue<TMP_Text>();
                if (text == null) text = __instance.GetComponentInChildren<TMP_Text>(true);
                if (music == null || text == null || text.font == null) throw new InvalidOperationException("Native audio UI templates are unavailable.");
                sliderTemplate = music; valueTemplate = text; textTemplate = FindLabelTemplate(__instance, text);
                buttonTemplate = FindButtonTemplate(__instance.transform);
                scrollbarTemplate = FindScrollbarTemplate(__instance.transform);
                if (buttonTemplate == null) throw new InvalidOperationException("Native menu button template is unavailable.");
                var captionTemplate = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
                if (captionTemplate != null && captionTemplate.font != null) textTemplate = captionTemplate;
                ownerRect = __instance.transform as RectTransform;
                lastNativeRow = (continuous != null ? continuous.transform : music.transform) as RectTransform;
                if (ownerRect == null || lastNativeRow == null) throw new InvalidOperationException("Audio settings RectTransform is unavailable.");
                Transform parent = ownerRect;
                cloneStaging = Node("AmbienceTemplates", ownerRect); cloneStaging.SetActive(false);
                var old = parent.Find("AmbienceGridContainer");
                if (old != null) { old.gameObject.SetActive(false); UnityEngine.Object.Destroy(old.gameObject); }
                root = Node("AmbienceGridContainer", parent);
                var rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = rootRect.anchorMax = new Vector2(.5f, .5f); rootRect.pivot = new Vector2(.5f, 1);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = new Vector2(760, 240);
                root.AddComponent<LayoutElement>().ignoreLayout = true;
                root.transform.SetAsLastSibling();
                root.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
                var scroll = root.AddComponent<ScrollRect>();
                var viewport = Node("Viewport", root.transform);
                Stretch((RectTransform)viewport.transform, 4, 4, 20, 4);
                viewport.AddComponent<RectMask2D>();
                viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
                content = (RectTransform)Node("Categories", viewport.transform).transform;
                content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
                content.sizeDelta = Vector2.zero;
                Stack(content.gameObject, 2, 2, true);
                scroll.viewport = (RectTransform)viewport.transform;
                scroll.content = content;
                scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 500;
                scroll.verticalScrollbar = CreateScrollbar(root.transform);
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

                CreateCategory("Wind Volume", AmbienceSoundConfig.WindVolume, SoundGroup.Wind, null);
                CreateCategory("Ocean Volume", AmbienceSoundConfig.OceanVolume, SoundGroup.Ocean, null);
                CreateCategory("Boat Volume", AmbienceSoundConfig.BoatVolume, SoundGroup.Boat, null);
                CreateCategory("Ambient Loop Volume", AmbienceSoundConfig.AmbientLoopVolume, SoundGroup.Ambient, null);
                CreateCategory("Shield Hum Volume", AmbienceSoundConfig.ShieldHumVolume, SoundGroup.Shield, null);
                CreateCategory("Extra SFX Volume", AmbienceSoundConfig.ExtraSfxVolume, null, false);
                CreateCategory("Extra Clip Volume", AmbienceSoundConfig.ExtraClipVolume, null, true);
                renderedRevision = SoundSettings.Revision;
                SyncUI(); RebuildLayout();
                Debug.Log("[AmbienceSoundConfig] Native sound categories created; font: " + textTemplate.font.name);
            }
            catch (Exception ex) { Debug.LogError("[AmbienceSoundConfig] Audio menu injection failed: " + ex); }
        }

        private static void CreateCategory(string title, ConfigEntry<float> entry, SoundGroup? group, bool? extra)
        {
            var box = Node(title, content); Stack(box, 2, 0);
            var header = Row(box.transform, 30);
            var category = new Category { Title = title, Group = group, ExtraClips = extra, Entry = entry, RestoreVolume = entry.Value > 0 ? entry.Value : 1f };
            var toggle = Button(header.transform, "+", 26, out var toggleLabel);
            category.ToggleText = toggleLabel;
            Label(header.transform, title, 230, 16);
            Volume(header.transform, entry);
            Button(header.transform, entry.Value > 0 ? "Mute" : "Unmute", 72, out category.MuteText).onClick.AddListener(() => {
                if (entry.Value > 0) { category.RestoreVolume = entry.Value; entry.Value = 0; }
                else entry.Value = AmbienceSoundConfig.ClampConfiguredVolume(category.RestoreVolume);
                RefreshAudio(); SyncUI();
            });
            category.Body = Node("Sounds", box.transform); Stack(category.Body, 3, 8);
            category.Body.SetActive(false);
            toggle.onClick.AddListener(() => {
                bool expanding = !category.Expanded;
                foreach (var other in Categories)
                    if (other != category && other.Expanded) { other.Expanded = false; other.ToggleText.text = "+"; other.Body.SetActive(false); }
                category.Expanded = expanding;
                lastSelected = null;
                category.ToggleText.text = category.Expanded ? "-" : "+";
                category.Body.SetActive(category.Expanded);
                if (category.Expanded && category.BuiltRevision != SoundSettings.Revision) BuildChildren(category);
                SyncUI();
                RebuildLayout();
                if (category.Expanded) ScrollToCategory(category);
            });
            Categories.Add(category);
        }

        private static void BuildChildren(Category category)
        {
            var body = category.Body.transform;
            Bindings.RemoveAll(binding => binding.Slider == null || binding.Slider.transform.IsChildOf(body));
            Inputs.RemoveAll(input => input == null || input.transform.IsChildOf(body));
            for (int index = body.childCount - 1; index >= 0; index--)
            {
                var child = body.GetChild(index).gameObject;
                child.SetActive(false); UnityEngine.Object.Destroy(child);
            }
            if (category.ExtraClips.HasValue)
            {
                bool clips = category.ExtraClips.Value;
                foreach (var item in SoundSettings.GetRows(clips))
                {
                    var row = Row(body, 30);
                    var input = Input(row.transform, item.Name.Value);
                    Volume(row.transform, item.Volume);
                    Button(row.transform, "X", 26, out _).onClick.AddListener(() => { SoundSettings.Remove(item); RefreshAudio(); });
                    input.onEndEdit.AddListener(value => {
                        if (!SoundSettings.Rename(item, value, out string error))
                        {
                            input.SetTextWithoutNotify(item.Name.Value);
                            if (category.Status != null) category.Status.text = error;
                        }
                        else { if (category.Status != null) category.Status.text = ""; category.BuiltRevision = renderedRevision = SoundSettings.Revision; RefreshAudio(true); }
                    });
                    if (pendingFocus == item.Id)
                    {
                        pendingFocus = null; input.Select(); input.ActivateInputField();
                    }
                }
                var addRow = Row(body, 30);
                Button(addRow.transform, "Add sound", 120, out _).onClick.AddListener(() => {
                    pendingFocus = SoundSettings.AddRow(clips).Id;
                    BuildChildren(category); renderedRevision = SoundSettings.Revision; RebuildLayout();
                });
                Button(addRow.transform, "Reset levels", 110, out _).onClick.AddListener(() => {
                    foreach (var item in SoundSettings.GetRows(clips)) item.Volume.Value = AmbienceSoundConfig.ClampConfiguredVolume(1f);
                    RefreshAudio(); SyncUI();
                });
                category.Status = Label(addRow.transform, "", 180, 13);
                category.Status.color = new Color(1f, .65f, .45f);
            }
            else
            {
                var sounds = SoundSettings.GetControls(category.Group);
                int variation = 0;
                foreach (var sound in sounds)
                {
                    var row = Row(body, 30);
                    string name = SoundSettings.DisplayName(sound, sound.IsChannel ? 0 : ++variation);
                    var label = Label(row.transform, name, 270, 15);
                    label.GetComponent<LayoutElement>().flexibleWidth = 1;
                    Volume(row.transform, sound.Volume);
                }
                var footer = Row(body, 28);
                Button(footer.transform, "Reset sounds", 132, out _).onClick.AddListener(() => {
                    SoundSettings.ResetControls(category.Group); RefreshAudio(); SyncUI();
                });
            }
            category.BuiltRevision = SoundSettings.Revision;
        }

        internal static void Tick()
        {
            if (root == null || !root.activeInHierarchy) { wasVisible = false; return; }
            var screenSize = new Vector2(Screen.width, Screen.height);
            var hostSize = layoutHost != null ? layoutHost.rect.size : Vector2.zero;
            if (!wasVisible || screenSize != lastScreenSize || ownerRect.rect.size != lastOwnerSize || hostSize != lastHostSize)
            {
                RebuildLayout();
                lastScreenSize = screenSize; lastOwnerSize = ownerRect.rect.size;
                lastHostSize = layoutHost != null ? layoutHost.rect.size : Vector2.zero;
            }
            wasVisible = true;
            SyncUI();
            KeepSelectedVisible();
            if (renderedRevision == SoundSettings.Revision || Inputs.Exists(input => input != null && input.isFocused)) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.transform.IsChildOf(root.transform) && selected.GetComponent<Slider>() != null) return;
            foreach (var category in Categories)
                if (category.Expanded && category.BuiltRevision != SoundSettings.Revision) BuildChildren(category);
            renderedRevision = SoundSettings.Revision;
            RebuildLayout();
        }

        public static void SyncUI()
        {
            foreach (var category in Categories)
            {
                if (category.Entry.Value > 0) category.RestoreVolume = category.Entry.Value;
                if (category.MuteText != null) category.MuteText.text = category.Entry.Value > 0 ? "Mute" : "Unmute";
            }
            for (int i = Bindings.Count - 1; i >= 0; i--)
            {
                var binding = Bindings[i];
                if (binding.Slider == null) { Bindings.RemoveAt(i); continue; }
                float maximum = AmbienceSoundConfig.GetVolumeSliderMax();
                if (binding.Slider.maxValue != maximum) binding.Slider.maxValue = maximum;
                if (binding.Slider.value != binding.Entry.Value) binding.Slider.SetValueWithoutNotify(binding.Entry.Value);
                int percentage = Mathf.RoundToInt(binding.Entry.Value * 100f);
                if (binding.Percentage != percentage) { binding.Percentage = percentage; binding.Value.text = percentage + "%"; }
            }
        }

        internal static void Cleanup()
        {
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            if (cloneStaging != null) UnityEngine.Object.Destroy(cloneStaging);
            cloneStaging = null; textTemplate = null; valueTemplate = null; sliderTemplate = null; buttonTemplate = null; scrollbarTemplate = null;
            root = null; content = null; pendingFocus = null;
            nativeToggle = null; backButton = null; okButton = null; lastSelected = null;
            ownerRect = null; lastNativeRow = null; layoutHost = null; lastPanelSize = Vector2.zero;
            wasVisible = false;
            Categories.Clear(); Bindings.Clear(); Inputs.Clear();
        }

        private static void RefreshAudio(bool discover = false)
        {
            AudioSourceFilter.Refresh();
            if (discover) AudioSourceFilter.ApplyToRunningSources();
            else AudioVolumeController.Refresh();
        }

        internal static bool IsEditing() => AudioTextFocus.IsEditing(root, Inputs);

        private static void Volume(Transform parent, ConfigEntry<float> entry)
        {
            // Native visual hierarchy: child scales, image
            // overrides, fill/handle references and authored track geometry.
            var go = CloneNative(sliderTemplate.gameObject, parent); go.name = "Volume";
            float nativeHeight = ((RectTransform)sliderTemplate.transform).rect.height;
            Size(go, nativeHeight > 0 ? nativeHeight : 20f, 160, 1);
            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true)) text.enabled = false;
            var slider = go.GetComponent<Slider>();
            slider.onValueChanged = new Slider.SliderEvent();
            slider.interactable = true;
            slider.minValue = 0; slider.maxValue = AmbienceSoundConfig.GetVolumeSliderMax();
            slider.SetValueWithoutNotify(entry.Value);
            var value = Label(parent, Mathf.RoundToInt(entry.Value * 100f) + "%", 52, 15, true);
            value.alignment = TextAlignmentOptions.Right;
            slider.onValueChanged.AddListener(number => {
                entry.Value = AmbienceSoundConfig.ClampConfiguredVolume(number);
                if (entry.Value > 0) foreach (var category in Categories) if (category.Entry == entry) category.RestoreVolume = entry.Value;
                value.text = Mathf.RoundToInt(entry.Value * 100f) + "%"; RefreshAudio();
            });
            go.SetActive(true);
            Bindings.Add(new Binding { Slider = slider, Value = value, Entry = entry });
        }
        private static TMP_InputField Input(Transform parent, string value)
        {
            var go = Node("Sound Name", parent); Size(go, 28, 240, 1);
            var image = go.AddComponent<Image>(); CopyImage(image, buttonTemplate.targetGraphic as Image);
            image.color = new Color(.55f, .55f, .55f, .9f);
            var viewport = Node("Text Area", go.transform); Stretch((RectTransform)viewport.transform, 8, 2, 8, 2); viewport.AddComponent<RectMask2D>();
            var text = Label(viewport.transform, "", -1, 15, true); Stretch((RectTransform)text.transform, 0, 0, 0, 0);
            var placeholder = Label(viewport.transform, "Sound name", -1, 15); Stretch((RectTransform)placeholder.transform, 0, 0, 0, 0);
            var color = placeholder.color; color.a = .5f; placeholder.color = color;
            var input = go.AddComponent<TMP_InputField>(); input.textViewport = (RectTransform)viewport.transform;
            input.textComponent = text; input.placeholder = placeholder; input.targetGraphic = image;
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 256;
            input.richText = false; input.SetTextWithoutNotify(value);
            Inputs.Add(input); return input;
        }

        private static Button Button(Transform parent, string caption, float width, out TextMeshProUGUI text)
        {
            if (width <= 32 && nativeToggle != null && nativeToggle.targetGraphic is Image frame && frame.sprite != null)
                return CompactButton(parent, caption, width, frame, out text);
            var go = CloneNative(buttonTemplate.gameObject, parent); go.name = caption; Size(go, 26, width, 0);
            var button = go.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent(); button.interactable = true;
            text = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text == null) throw new InvalidOperationException("Native button has no text template.");
            foreach (var other in go.GetComponentsInChildren<TMP_Text>(true)) if (other != text) other.enabled = false;
            Stretch((RectTransform)text.transform, 1, 0, 1, 0);
            text.text = caption; text.fontSize = 14; text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = 14;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            go.SetActive(true); text.text = caption;
            return button;
        }

        private static Button CompactButton(Transform parent, string caption, float width, Image frame, out TextMeshProUGUI text)
        {
            // A square native frame avoids compressing the wide Back button's
            // decorative edges and glow into a 26-pixel expansion control.
            var go = Node(caption, parent); go.SetActive(false); Size(go, 26, width, 0);
            var image = go.AddComponent<Image>(); CopyImage(image, frame);
            image.preserveAspect = true;
            var button = go.AddComponent<Button>(); button.targetGraphic = image;
            button.colors = buttonTemplate.colors;
            button.transition = Selectable.Transition.ColorTint;
            text = Label(go.transform, caption, -1, 14);
            Stretch((RectTransform)text.transform, 0, 0, 0, 0);
            text.alignment = TextAlignmentOptions.Center;
            foreach (var sfxTemplate in buttonTemplate.GetComponents<MonoBehaviour>())
            {
                if (sfxTemplate == null || sfxTemplate.GetType().Name != "ButtonSfx") continue;
                var sfx = go.AddComponent(sfxTemplate.GetType());
                foreach (var field in sfxTemplate.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    field.SetValue(sfx, field.GetValue(sfxTemplate));
            }
            go.SetActive(true);
            return button;
        }

        private static TextMeshProUGUI Label(Transform parent, string value, float width, float size, bool numeric = false)
        {
            // Cloning carries the serialized native font and material into Awake.
            // Adding TMP_Text first would try to load missing LiberationSans.
            var template = numeric ? valueTemplate : textTemplate;
            var go = CloneNative(template.gameObject, parent); go.name = "Text";
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = template.font; label.fontSharedMaterial = template.fontSharedMaterial;
            label.fontSize = size; label.richText = false; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = size;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.overflowMode = TextOverflowModes.Ellipsis;
            if (width >= 0) Size(go, 26, width, 0);
            go.SetActive(true); label.text = value;
            return label;
        }

        private static GameObject CloneNative(GameObject template, Transform parent)
        {
            var clone = UnityEngine.Object.Instantiate(template, cloneStaging.transform, false);
            clone.SetActive(false); clone.transform.SetParent(parent, false); clone.transform.localScale = Vector3.one;
            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is EventTrigger trigger) { trigger.triggers.Clear(); continue; }
                if (behaviour is UIBehaviour || (behaviour.GetType().Name == "ButtonSfx" || behaviour.GetType().Name == "SliderSfx")) continue;
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
            foreach (var fitter in clone.GetComponentsInChildren<ContentSizeFitter>(true)) UnityEngine.Object.DestroyImmediate(fitter);
            foreach (var layout in clone.GetComponentsInChildren<LayoutGroup>(true)) UnityEngine.Object.DestroyImmediate(layout);
            foreach (var group in clone.GetComponentsInChildren<CanvasGroup>(true)) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
            foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true))
                if (text.font == null) { text.font = valueTemplate.font; text.fontSharedMaterial = valueTemplate.fontSharedMaterial; }
            return clone;
        }

        private static TMP_Text FindLabelTemplate(Component owner, TMP_Text fallback)
        {
            foreach (var candidate in owner.GetComponentsInChildren<TMP_Text>(true))
                if (candidate != fallback && candidate.font != null && candidate.text != null && candidate.text.IndexOf("volume", StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
            return fallback;
        }

        private static Button FindButtonTemplate(Transform owner)
        {
            Button fallback = null;
            for (var current = owner; current != null; current = current.parent)
                foreach (var button in current.GetComponentsInChildren<Button>(true))
                {
                    if (button.GetComponentInChildren<TMP_Text>(true) == null) continue;
                    if (fallback == null) fallback = button;
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (button.name.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0 || label.text.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0) return button;
                }
            return fallback;
        }

        private static Scrollbar FindScrollbarTemplate(Transform owner)
        {
            for (var current = owner; current != null; current = current.parent)
                foreach (var bar in current.GetComponentsInChildren<Scrollbar>(true)) return bar;
            foreach (var bar in Resources.FindObjectsOfTypeAll<Scrollbar>()) return bar;
            return null;
        }

        private static Scrollbar CreateScrollbar(Transform parent)
        {
            GameObject go;
            Scrollbar bar;
            if (scrollbarTemplate != null)
            {
                go = CloneNative(scrollbarTemplate.gameObject, parent); bar = go.GetComponent<Scrollbar>();
                bar.onValueChanged = new Scrollbar.ScrollEvent();
            }
            else
            {
                go = Node("Scrollbar", parent);
                var background = go.AddComponent<Image>(); background.color = new Color(0, 0, 0, .15f);
                var handle = Node("Handle", go.transform); Stretch((RectTransform)handle.transform, 1, 0, 1, 0);
                var image = handle.AddComponent<Image>(); CopyImage(image, buttonTemplate.targetGraphic as Image);
                bar = go.AddComponent<Scrollbar>(); bar.handleRect = (RectTransform)handle.transform; bar.targetGraphic = image;
            }
            go.name = "Scrollbar";
            var rect = (RectTransform)go.transform; rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-12, 4); rect.offsetMax = new Vector2(-2, -4);
            bar.direction = Scrollbar.Direction.BottomToTop;
            go.SetActive(true); return bar;
        }

        private static void CopyImage(Image target, Image template)
        {
            if (template == null) return;
            target.sprite = template.sprite; target.type = template.type; target.material = template.material;
            target.color = template.color; target.preserveAspect = template.preserveAspect;
            target.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
        }
        private static GameObject Row(Transform parent, float height)
        {
            var go = Node("Row", parent); Size(go, height, -1, 1);
            var layout = go.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 6;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return go;
        }

        private static void Stack(GameObject go, float spacing, int indent, bool fit = false)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>(); layout.spacing = spacing;
            layout.padding = new RectOffset(indent, 2, 0, 0);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            if (fit) go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go;
        }

        private static void Size(GameObject go, float height, float width, float flexible)
        {
            var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>(); layout.ignoreLayout = false; layout.layoutPriority = 2; layout.minHeight = height; layout.preferredHeight = height;
            if (width >= 0) { layout.preferredWidth = width; layout.minWidth = Mathf.Min(width, 70); }
            layout.flexibleWidth = flexible;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        private static void RebuildLayout()
        {
            if (content == null) return;
            Canvas.ForceUpdateCanvases();
            PositionPanel();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            LinkNavigation();
        }

        private static void ScrollToCategory(Category category)
        {
            var scroll = root.GetComponent<ScrollRect>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, category.Body.transform.parent);
            var position = content.anchoredPosition;
            position.y = Mathf.Clamp(content.rect.yMax - bounds.max.y, 0, Mathf.Max(0, content.rect.height - scroll.viewport.rect.height));
            content.anchoredPosition = position;
            scroll.StopMovement();
        }

        internal static void BindNavigation(Valheim.SettingsGui.AudioSettings owner, Button back, Button ok)
        {
            if (root == null || !root.transform.IsChildOf(owner.transform)) return;
            backButton = back; okButton = ok; if (back != null) buttonTemplate = back; RebuildLayout();
        }

        private static void PositionPanel()
        {
            if (root == null || ownerRect == null || lastNativeRow == null) return;
            var corners = Corners;
            lastNativeRow.GetWorldCorners(corners);
            Vector3 controlsBottomWorld = corners[0];
            for (Transform candidate = ownerRect; candidate != null; candidate = candidate.parent)
            {
                var host = candidate as RectTransform;
                if (host == null || host.rect.width < 480f) continue;
                float controlsBottom = host.rect.yMax - host.InverseTransformPoint(controlsBottomWorld).y;
                float buttonsTop = host.rect.height;
                foreach (var button in new[] { backButton, okButton })
                {
                    if (button == null) continue;
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    buttonsTop = Mathf.Min(buttonsTop, host.rect.yMax - host.InverseTransformPoint(corners[1]).y);
                }
                if (!AudioMenuLayout.TryCalculate(host.rect.width, host.rect.height, controlsBottom, buttonsTop, out var area)) continue;
                var rect = (RectTransform)root.transform;
                var scale = host.lossyScale;
                var parentScale = ownerRect.lossyScale;
                if (Mathf.Abs(parentScale.x) < .0001f || Mathf.Abs(parentScale.y) < .0001f) return;
                var size = new Vector2(area.Width * Mathf.Abs(scale.x / parentScale.x), area.Height * Mathf.Abs(scale.y / parentScale.y));
                rect.sizeDelta = size;
                rect.position = host.TransformPoint(new Vector3(host.rect.center.x, host.rect.yMax - area.Top, 0));
                if (layoutHost != host || (size - lastPanelSize).sqrMagnitude > 1f)
                {
                    layoutHost = host; lastPanelSize = size;
                    Debug.Log($"[AmbienceSoundConfig] Audio panel: {host.name}, {size.x:F0} x {size.y:F0}.");
                }
                return;
            }
        }

        private static void LinkNavigation()
        {
            if (root == null) return;
            var controls = new List<Selectable>();
            foreach (var control in root.GetComponentsInChildren<Selectable>())
                if (!(control is Scrollbar) && control.IsInteractable()) controls.Add(control);
            for (int index = 0; index < controls.Count; index++)
            {
                var navigation = controls[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = index > 0 ? controls[index - 1] : nativeToggle;
                navigation.selectOnDown = index + 1 < controls.Count ? controls[index + 1] : backButton;
                navigation.selectOnLeft = null; navigation.selectOnRight = null;
                controls[index].navigation = navigation;
            }
            if (controls.Count == 0) return;
            if (nativeToggle != null) { var navigation = nativeToggle.navigation; navigation.selectOnDown = controls[0]; nativeToggle.navigation = navigation; }
            foreach (var button in new[] { backButton, okButton })
                if (button != null) { var navigation = button.navigation; navigation.selectOnUp = controls[controls.Count - 1]; button.navigation = navigation; }
        }

        private static void KeepSelectedVisible()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == lastSelected) return;
            lastSelected = selected;
            if (selected == null || !selected.transform.IsChildOf(content)) return;
            var scroll = root.GetComponent<ScrollRect>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
            var viewport = scroll.viewport.rect;
            var position = content.anchoredPosition;
            if (bounds.min.y < viewport.yMin) position.y += viewport.yMin - bounds.min.y;
            else if (bounds.max.y > viewport.yMax) position.y -= bounds.max.y - viewport.yMax;
            position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, content.rect.height - viewport.height));
            content.anchoredPosition = position;
        }
    }

    [HarmonyPatch(typeof(TabHandler), "Update")]
    internal static class AudioTextFocus_Patch
    {
        private static bool Prefix() => !SettingsInject.IsEditing();
    }

    [HarmonyPatch(typeof(Valheim.SettingsGui.AudioSettings), "OnTabOpen")]
    internal static class AudioSettingsNavigation_Patch
    {
        private static void Postfix(Valheim.SettingsGui.AudioSettings __instance, Button backButton, Button okButton)
        {
            SettingsInject.BindNavigation(__instance, backButton, okButton);
        }
    }
}
