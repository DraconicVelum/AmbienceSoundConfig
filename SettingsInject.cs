using HarmonyLib;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valheim;
using System.Reflection;

namespace AmbienceSoundConfig
{
    [HarmonyPatch]
    public static class SettingsInject
    {
        static MethodBase TargetMethod()
        {
            var type = typeof(Valheim.SettingsGui.AudioSettings);

            MethodBase Find(string name) =>
                type.GetMethod(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

            var method =
                Find("Initialize") ??
                Find("Awake") ??
                Find("Start");

            if (method != null)
            {
                Debug.Log($"[AmbienceSoundConfig] UI inject patching → {method.Name}");
                return method;
            }

            method = Find("LoadSettings");
            if (method != null)
            {
                Debug.Log("[AmbienceSoundConfig] UI inject patching → LoadSettings (legacy fallback for Valheim 0.221.4)");
                return method;
            }

            throw new Exception("[AmbienceSoundConfig] No compatible AudioSettings method found to patch.");
        }
        private static Transform _gridContainer;
        private static bool _built = false;
        private const string ContainerName = "AmbienceGridContainer";

        internal static Slider _sMaster;
        internal static Slider _sWind;
        internal static Slider _sOcean;
        internal static Slider _sAmbientLoop;
        internal static Slider _sShieldHum;
        internal static Slider _sExtraSfx;
        internal static Slider _sExtraClip;

        internal static TextMeshProUGUI _vMaster;
        internal static TextMeshProUGUI _vWind;
        internal static TextMeshProUGUI _vOcean;
        internal static TextMeshProUGUI _vAmbientLoop;
        internal static TextMeshProUGUI _vShieldHum;
        internal static TextMeshProUGUI _vExtraSfx;
        internal static TextMeshProUGUI _vExtraClip;

        [HarmonyPostfix]
        private static void Postfix_LoadSettings(Valheim.SettingsGui.AudioSettings __instance)
        {
            try
            {
                if (_built && _gridContainer != null)
                {
                    SyncUI();
                    return;
                }

                var baseSlider = Traverse.Create(__instance)
                    .Field("m_musicVolumeSlider")
                    .GetValue<Slider>()?.gameObject;

                if (baseSlider == null)
                {
                    Debug.LogWarning("[AmbienceSoundConfig] Could not get m_musicVolumeSlider.");
                    return;
                }


                var continous = Traverse.Create(__instance).Field("m_continousMusic").GetValue<Toggle>();
                Transform parent = continous?.transform.parent ?? baseSlider.transform.parent;

                var existing = parent.Find(ContainerName);
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);

                GameObject container = new GameObject(ContainerName,
                    typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
                container.transform.SetParent(parent, false);

                int siblingIndex = (continous != null
                    ? continous.transform.GetSiblingIndex()
                    : baseSlider.transform.GetSiblingIndex()) + 1;
                container.transform.SetSiblingIndex(siblingIndex);

                var rect = container.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.anchoredPosition = new Vector2(0f, 0f);
                rect.sizeDelta = new Vector2(0f, 0f);

                var grid = container.GetComponent<GridLayoutGroup>();
                grid.padding = new RectOffset(0, 0, 0, 0);
                grid.cellSize = new Vector2(300, 20);
                grid.spacing = new Vector2(0, 8);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 1;
                grid.childAlignment = TextAnchor.UpperLeft;

                var fitter = container.GetComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                _gridContainer = container.transform;

                BindOrCreateSliders(_gridContainer, baseSlider);
                SyncUI();

                _built = true;

                LayoutRebuilder.ForceRebuildLayoutImmediate(parent.GetComponent<RectTransform>());

                Debug.Log("[AmbienceSoundConfig] AmbienceGridContainer built and anchored to top (no drift).");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AmbienceSoundConfig] LoadSettings patch failed: " + ex);
            }
        }

        private static void BindOrCreateSliders(Transform container, GameObject baseSlider)
        {
            CreateOrBind(container, baseSlider, "Ambience Master Volume",
                ref _sMaster, ref _vMaster, AmbienceSoundConfig.MasterVolume, (_) => AmbienceSoundConfig.ApplyAllVolumes());

            CreateOrBind(container, baseSlider, "Wind Volume",
                ref _sWind, ref _vWind, AmbienceSoundConfig.WindVolume, (_) => AudioMan_AmbienceVolume_Patch.ApplyWindVolumeWrapper());

            CreateOrBind(container, baseSlider, "Ocean Volume",
                ref _sOcean, ref _vOcean, AmbienceSoundConfig.OceanVolume, (_) => AudioMan_AmbienceVolume_Patch.ApplyOceanVolumeWrapper());

            CreateOrBind(container, baseSlider, "Ambient Loop Volume",
                ref _sAmbientLoop, ref _vAmbientLoop, AmbienceSoundConfig.AmbientLoopVolume, (_) => AudioMan_AmbienceVolume_Patch.ApplyAmbientLoopVolumeWrapper());

            CreateOrBind(container, baseSlider, "Shield Hum Volume",
                ref _sShieldHum, ref _vShieldHum, AmbienceSoundConfig.ShieldHumVolume, (_) => AudioMan_AmbienceVolume_Patch.ApplyShieldHumVolumeWrapper());

            CreateOrBind(container, baseSlider, "Extra SFX Volume",
                ref _sExtraSfx, ref _vExtraSfx, AmbienceSoundConfig.ExtraSfxVolume, (_) => AudioSourceFilter.Refresh());

            CreateOrBind(container, baseSlider, "Extra Clip Volume",
                ref _sExtraClip, ref _vExtraClip, AmbienceSoundConfig.ExtraClipVolume, (_) => AudioSourceFilter.Refresh());
        }

        internal static void CreateOrBind(
            Transform parent,
            GameObject baseSlider,
            string labelText,
            ref Slider sliderRef,
            ref TextMeshProUGUI valueTextRef,
            BepInEx.Configuration.ConfigEntry<float> config,
            Action<float> onChanged)
        {
            string goName = "Ambience_" + labelText.Replace(" ", "");
            Transform existing = parent.Find(goName);

            GameObject sliderGO;
            if (existing == null)
            {
                sliderGO = UnityEngine.Object.Instantiate(baseSlider, parent);
                sliderGO.name = goName;
                sliderGO.transform.localScale = Vector3.one;
                sliderGO.SetActive(true);

                var rect = sliderGO.GetComponent<RectTransform>();
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;

                foreach (var tmp in sliderGO.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp.font == null)
                    {
                        var src = baseSlider.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (src != null && src.font != null)
                        {
                            tmp.font = src.font;
                            tmp.fontSharedMaterial = src.fontSharedMaterial;
                        }
                        else
                        {
                            var fallback = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                            if (fallback.Length > 0)
                                tmp.font = fallback[0];
                        }
                    }
                }
            }
            else
            {
                sliderGO = existing.gameObject;
            }

            var slider = sliderGO.GetComponentInChildren<Slider>(true);
            if (slider == null)
            {
                Debug.LogError($"[AmbienceSoundConfig] Slider missing for {labelText}");
                return;
            }

            TextMeshProUGUI label = null;
            TextMeshProUGUI valueText = null;
            foreach (var t in sliderGO.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                var lower = t.name.ToLowerInvariant();
                if (lower.Contains("value")) valueText = t;
                else if (lower.Contains("text") || lower.Contains("label")) label = t;
            }

            if (label != null)
            {
                label.text = labelText;
                label.enableAutoSizing = true;
                label.fontSizeMin = 10;
                label.fontSizeMax = 14;
                label.raycastTarget = false;
            }
            if (valueText != null)
            {
                valueText.raycastTarget = false;
                valueText.text = $"{Mathf.RoundToInt(config.Value * 100f)}%";
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener((float val) =>
            {
                config.Value = val;
                config.ConfigFile.Save();
                if (valueText != null)
                    valueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
                onChanged?.Invoke(val);
            });

            sliderRef = slider;
            valueTextRef = valueText;
            slider.value = config.Value;
        }

        public static void SyncUI()
        {
            try
            {
                if (_gridContainer == null || _gridContainer.gameObject == null) return;

                if (_sMaster != null)
                {
                    _sMaster.SetValueWithoutNotify(AmbienceSoundConfig.MasterVolume.Value);
                    if (_vMaster != null) _vMaster.text = $"{Mathf.RoundToInt(_sMaster.value * 100f)}%";
                }
                if (_sWind != null)
                {
                    _sWind.SetValueWithoutNotify(AmbienceSoundConfig.WindVolume.Value);
                    if (_vWind != null) _vWind.text = $"{Mathf.RoundToInt(_sWind.value * 100f)}%";
                }
                if (_sOcean != null)
                {
                    _sOcean.SetValueWithoutNotify(AmbienceSoundConfig.OceanVolume.Value);
                    if (_vOcean != null) _vOcean.text = $"{Mathf.RoundToInt(_sOcean.value * 100f)}%";
                }
                if (_sAmbientLoop != null)
                {
                    _sAmbientLoop.SetValueWithoutNotify(AmbienceSoundConfig.AmbientLoopVolume.Value);
                    if (_vAmbientLoop != null) _vAmbientLoop.text = $"{Mathf.RoundToInt(_sAmbientLoop.value * 100f)}%";
                }
                if (_sShieldHum != null)
                {
                    _sShieldHum.SetValueWithoutNotify(AmbienceSoundConfig.ShieldHumVolume.Value);
                    if (_vShieldHum != null) _vShieldHum.text = $"{Mathf.RoundToInt(_sShieldHum.value * 100f)}%";
                }
                if (_sExtraSfx != null)
                {
                    _sExtraSfx.SetValueWithoutNotify(AmbienceSoundConfig.ExtraSfxVolume.Value);
                    if (_vExtraSfx != null) _vExtraSfx.text = $"{Mathf.RoundToInt(_sExtraSfx.value * 100f)}%";
                }
                if (_sExtraClip != null)
                {
                    _sExtraClip.SetValueWithoutNotify(AmbienceSoundConfig.ExtraClipVolume.Value);
                    if (_vExtraClip != null)
                        _vExtraClip.text = $"{Mathf.RoundToInt(_sExtraClip.value * 100f)}%";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AmbienceSoundConfig] SyncUI failed: " + ex);
            }
        }

        private static GameObject FindDeepChild(Transform parent, string name)
        {
            var t = FindDeepChildTransform(parent, name);
            return t ? t.gameObject : null;
        }

        private static Transform FindDeepChildTransform(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var result = FindDeepChildTransform(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
