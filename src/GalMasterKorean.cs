using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Reflection;

namespace GalMasterKoreanPoC
{
    [BepInPlugin("kr.galmaster.korean.ui", "GalMaster Korean UI", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private readonly Dictionary<string, string> translations = new Dictionary<string, string>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly HashSet<int> fixedTitleImages = new HashSet<int>();
        private readonly HashSet<int> patchedTextures = new HashSet<int>();
        private string dictionaryPath;
        private string capturePath;
        private float nextScan;
        private DateTime dictionaryWriteTime;
        private TMP_FontAsset koreanFont;
        private TMP_FontAsset koreanBoldFont;
        private readonly Dictionary<int, Material> dialogueMaterials = new Dictionary<int, Material>();
        private readonly HashSet<int> outlinedLabels = new HashSet<int>();
        private readonly Dictionary<int, string> day1Dialogue = new Dictionary<int, string>();
        private readonly Dictionary<string, string> day1Options = new Dictionary<string, string>();
        private readonly Dictionary<string, string> expectedSources = new Dictionary<string, string>();
        private Harmony harmony;
        private static Plugin instance;
        public static TMP_FontAsset EndingFont { get { return instance == null ? null : (instance.koreanBoldFont != null ? instance.koreanBoldFont : instance.koreanFont); } }

        private void Awake()
        {
            instance = this;
            dictionaryPath = Path.Combine(Paths.ConfigPath, "GalMaster_UI_ko.tsv");
            capturePath = Path.Combine(Paths.ConfigPath, "GalMaster_UI_captured.tsv");
            foreach (string path in Directory.GetFiles(Paths.ConfigPath, "*_ko.png")) imagePaths.Add(path);
            string phones = Path.Combine(Paths.ConfigPath, "GalMaster_PhoneImages");
            if (Directory.Exists(phones)) foreach (string path in Directory.GetFiles(phones, "*.png", SearchOption.AllDirectories)) imagePaths.Add(path);
            LoadDictionary();
            LoadDay1();
            Type managerType = AccessTools.TypeByName("GameDataManger");
            MethodInfo target = managerType == null ? null : AccessTools.Method(managerType, "Awake");
            if (target != null)
            {
                harmony = new Harmony("kr.galmaster.korean.day1");
                harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "ApplyDay1Postfix")));
            }
            else Logger.LogError("GameDataManger.Awake를 찾지 못했습니다.");
            if (harmony == null) harmony = new Harmony("kr.galmaster.korean.text");
            MethodInfo textSetter = AccessTools.PropertySetter(typeof(TMP_Text), "text");
            if (textSetter != null) harmony.Patch(textSetter, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "TranslateTextPrefix")));
            MethodInfo legacySetter = AccessTools.PropertySetter(typeof(UnityEngine.UI.Text), "text");
            if (legacySetter != null) harmony.Patch(legacySetter, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "TranslateLegacyPrefix")));
            foreach (string property in new string[] { "sprite", "overrideSprite" })
            {
                MethodInfo setter = AccessTools.PropertySetter(typeof(Image), property);
                if (setter != null) harmony.Patch(setter, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "PhoneSpritePrefix")));
            }
            MethodInfo enable = AccessTools.Method(typeof(Image), "OnEnable");
            if (enable != null) harmony.Patch(enable, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "PhoneEnablePrefix")));
            foreach (Type uiType in new Type[] { typeof(TextMeshProUGUI), typeof(UnityEngine.UI.Text) }) {
                MethodInfo on = AccessTools.Method(uiType, "OnEnable");
                if (on != null) harmony.Patch(on, postfix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "TextEnablePostfix")));
            }
            // Translate at the game dialog boundary, before clone activation/animation.
            foreach (string entry in new string[] { "ConfirmTipAction:Show", "SaveloadAction:ShowLoadTip" }) {
                string[] part = entry.Split(':');
                Type owner = AccessTools.TypeByName(part[0]);
                MethodInfo show = owner == null ? null : AccessTools.Method(owner, part[1]);
                if (show != null) harmony.Patch(show,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "ConfirmShowPrefix")),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "ConfirmShowPostfix")));
                else Logger.LogError("Confirm dialog hook unavailable: " + entry);
            }
            Type endingType = AccessTools.TypeByName("UIHanderCenter");
            MethodInfo ending = endingType == null ? null : AccessTools.Method(endingType, "PlayDemoEndVideo");
            if (ending != null) harmony.Patch(ending, prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), "EndingVideoPrefix")));
            else Logger.LogError("Ending video subtitle hook unavailable");
            foreach (Image image in Resources.FindObjectsOfTypeAll<Image>()) RegisterImage(image);
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>()) trackedText.Add(text);
            foreach (UnityEngine.UI.Text text in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>()) trackedLegacy.Add(text);
            Canvas.preWillRenderCanvases += PrepareCanvas;
            Logger.LogInfo("UI 한글화 모듈 준비 완료: " + translations.Count + "개 항목");
        }

        private static void EndingVideoPrefix(object __instance, UnityEngine.Video.VideoClip __0)
        {
            if (__instance == null || __0 == null) return;
            try {
                Type t = __instance.GetType();
                RawImage image = AccessTools.Field(t, "DemoEndVideoImage").GetValue(__instance) as RawImage;
                UnityEngine.Video.VideoPlayer player = AccessTools.Field(t, "DemoEndVideoPlayer").GetValue(__instance) as UnityEngine.Video.VideoPlayer;
                if (image == null || player == null) return;
                EndingKoreanSubtitles overlay = image.GetComponent<EndingKoreanSubtitles>();
                if (overlay == null) overlay = image.gameObject.AddComponent<EndingKoreanSubtitles>();
                overlay.Bind(player, __0);
                instance.Logger.LogInfo("Ending subtitles bound: " + __0.name);
            } catch (Exception e) { instance.Logger.LogError("Ending subtitles: " + e); }
        }

        private static void ConfirmShowPrefix(ref string __0)
        {
            if (instance == null || string.IsNullOrEmpty(__0)) return;
            string translated;
            if (instance.translations.TryGetValue(__0, out translated)) __0 = translated;
        }
        private static void ConfirmShowPostfix(object __instance)
        {
            if (instance == null) return;
            Type type = __instance.GetType();
            string field = type.Name == "ConfirmTipAction" ? "_loadTip" : "LoadTip";
            FieldInfo info = AccessTools.Field(type, field);
            GameObject root = info == null ? null : info.GetValue(__instance) as GameObject;
            if (root == null) return;
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true)) {
                RefreshEnabledText(label);
                // A cloned TMP component can retain the template's mesh until rebuild.
                label.ForceMeshUpdate(true, true);
            }
        }
        private static void RefreshEnabledText(Component component)
        {
            if (instance == null) return;
            TMP_Text tmp = component as TMP_Text;
            if (tmp != null) {
                instance.trackedText.Add(tmp);
                string translated;
                if (instance.translations.TryGetValue(tmp.text ?? "", out translated)) tmp.text = translated;
                if (instance.koreanFont != null && NeedsKoreanFont(tmp.text)) instance.ApplyFont(tmp, tmp.text);
            }
            UnityEngine.UI.Text legacy = component as UnityEngine.UI.Text;
            if (legacy != null) {
                instance.trackedLegacy.Add(legacy);
                string translated;
                if (instance.translations.TryGetValue(legacy.text ?? "", out translated)) legacy.text = translated;
            }
        }

        private static void TranslateLegacyPrefix(ref string value)
        {
            if (instance == null || string.IsNullOrEmpty(value)) return;
            string translated;
            if (instance.translations.TryGetValue(value, out translated)) value = translated;
        }

        private static void TranslateTextPrefix(TMP_Text __instance, ref string value)
        {
            if (instance == null || String.IsNullOrEmpty(value)) return;
            string translated;
            if (instance.translations.TryGetValue(value, out translated)) value = translated;
            // U+2015 is absent from Nanum; U+2014 exists in both bundled weights.
            // Normalize display only so script text and translation lookup keys stay intact.
            value = value.Replace('\u2015', '\u2014');
            // User requested upward arrows in place of unsupported diagonal arrows.
            // Script/save text remains unchanged.
            if (value.IndexOf('\u2197') >= 0)
                value = value.Replace("\u2197", "\u2191");
            value = NormalizeSaveChapter(__instance, value);
            instance.ApplySaveMetadataFont(__instance);
            CenterSpeakerName(__instance);
            FitName(__instance, value);
            if (instance.koreanFont != null && NeedsKoreanFont(value) )
                instance.ApplyFont(__instance, value);
        }

        private static void CenterSpeakerName(TMP_Text label)
        {
            bool nameLabel = label.name == "SpeakingName" || label.name == "SpeakingName_Speak";
            if (!nameLabel && historyType != null)
            {
                Component history = label.GetComponentInParent(historyType);
                nameLabel = history != null &&
                    ReferenceEquals(AccessTools.Field(historyType, "historyNameTxt").GetValue(history), label);
            }
            if (!nameLabel) return;
            // Center visible glyphs rather than the font ascender/descender box.
            // Also runs for Latin-only NPC names, which bypass Korean font handling.
            if (label.verticalAlignment != VerticalAlignmentOptions.Geometry)
                label.verticalAlignment = VerticalAlignmentOptions.Geometry;
            Vector4 margin = label.margin;
            if (!Mathf.Approximately(margin.y, 0f) || !Mathf.Approximately(margin.w, 0f))
                label.margin = new Vector4(margin.x, 0f, margin.z, 0f);
        }

        private static void FitName(TMP_Text label, string value)
        {
            if (value != "샤오예" && value != "모추링" && value != "바이셴위" && value != "한샤오청" && value != "리밍") return;
            if (label.GetComponentInParent<PhoneKoreanOverlay>() != null) return;
            if (!label.enableAutoSizing) {
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = Math.Min(12f, label.fontSize);
                label.enableAutoSizing = true;
            }
            label.enableWordWrapping = false;
        }
        private static readonly Type historyType = AccessTools.TypeByName("HistoryObj");
        private static bool IsHistoryLabel(TMP_Text label)
        {
            if (historyType == null) return false;
            Component owner = label.GetComponentInParent(historyType);
            if (owner == null) return false;
            return ReferenceEquals(AccessTools.Field(historyType, "historyText").GetValue(owner), label)
                || ReferenceEquals(AccessTools.Field(historyType, "historyNameTxt").GetValue(owner), label);
        }
        private TMP_FontAsset FontFor(string value) { return koreanBoldFont != null && translations.ContainsValue(value) ? koreanBoldFont : koreanFont; }
        private readonly Dictionary<TMP_Text, float> originalLineSpacing = new Dictionary<TMP_Text, float>();
        private void ApplyDialogueSpacing(TMP_Text label)
        {
            bool body = label.name == "SpeakingContent";
            if (!body && historyType != null)
            {
                Component owner = label.GetComponentInParent(historyType);
                body = owner != null && ReferenceEquals(AccessTools.Field(historyType, "historyText").GetValue(owner), label);
            }
            if (!body) return;
            float baseline;
            if (!originalLineSpacing.TryGetValue(label, out baseline))
            {
                baseline = label.lineSpacing;
                originalLineSpacing.Add(label, baseline);
            }
            // Small, non-cumulative increase for dialogue and backlog body only.
            float target = baseline + 6f;
            if (!Mathf.Approximately(label.lineSpacing, target)) label.lineSpacing = target;
        }

        private readonly Dictionary<int, Material> largeDialogueMaterials = new Dictionary<int, Material>();
        private void ApplyFont(TMP_Text label, string value)
        {
            if (label.name == "KoreanEndingSubtitles") return;
            // Save dates contain only digits; do not gate these labels on Hangul.
            if (ApplySaveMetadataFont(label)) return;
            ApplyDialogueSpacing(label);
            Shader originalShader = label.fontSharedMaterial == null ? null : label.fontSharedMaterial.shader;
            bool choiceLabel = day1Options.ContainsValue(value) && label.GetComponentInParent<Button>() != null;
            bool dialogueLabel = label.name == "SpeakingContent" || label.name == "SpeakingName" || choiceLabel || IsHistoryLabel(label);
            TMP_FontAsset font = dialogueLabel && koreanBoldFont != null ? koreanBoldFont : FontFor(value);
            if (label.font != font) label.font = font;
            if (!dialogueLabel) return;
            bool largeDialogue = (label.name == "SpeakingContent" || IsHistoryLabel(label)) && label.fontSize > 36.5f;
            Dictionary<int, Material> materials = largeDialogue ? largeDialogueMaterials : dialogueMaterials;
            Material material;
            if (!materials.TryGetValue(font.GetInstanceID(), out material))
            {
                material = new Material(font.material);
                material.name = font.name + (largeDialogue ? "_LargeDialogueOutline" : "_OriginalDialogueOutline");
                if (originalShader != null) material.shader = originalShader;
                material.EnableKeyword("OUTLINE_ON");
                material.SetFloat("_OutlineWidth", 0.22f);
                material.SetFloat("_FaceDilate", largeDialogue ? 0.12f : 0.32f);
                material.SetColor("_FaceColor", Color.white);
                material.SetColor("_OutlineColor", new Color(0.035601325f, 0.009134057f, 0.049706575f, 1f));
                DontDestroyOnLoad(material);
                materials.Add(font.GetInstanceID(), material);
            }
            if (label.fontSharedMaterial != material)
            {
                label.fontSharedMaterial = material;
                label.UpdateMeshPadding();
            }
            if (outlinedLabels.Add(label.GetInstanceID()))
                Logger.LogInfo("원본 대사 외곽선 복원: " + label.name + ", shader=" + material.shader.name + ", width=" + material.GetFloat("_OutlineWidth"));
        }
        private static readonly Type saveSlotType = AccessTools.TypeByName("SaveLoadSlot");
        private static readonly FieldInfo saveChapterField = saveSlotType == null ? null : AccessTools.Field(saveSlotType, "saveDataNameText");
        private static readonly FieldInfo saveTimeField = saveSlotType == null ? null : AccessTools.Field(saveSlotType, "saveTimeText");
        private static readonly Regex chineseChapterDate = new Regex(@"^(\d{1,2})月\s*(\d{1,2})日$", RegexOptions.CultureInvariant);
        private Material saveMetadataMaterial;
        private readonly HashSet<int> saveMetadataLabels = new HashSet<int>();

        private static bool IsSaveMetadata(TMP_Text label, out bool chapter)
        {
            chapter = false;
            if (label == null || saveSlotType == null) return false;
            Component slot = label.GetComponentInParent(saveSlotType);
            if (slot == null) return false;
            chapter = saveChapterField != null && ReferenceEquals(saveChapterField.GetValue(slot), label);
            return chapter || (saveTimeField != null && ReferenceEquals(saveTimeField.GetValue(slot), label));
        }
        private static string NormalizeSaveChapter(TMP_Text label, string value)
        {
            bool chapter;
            if (String.IsNullOrEmpty(value) || !IsSaveMetadata(label, out chapter) || !chapter) return value;
            // Display only: existing save files and chapter identifiers are untouched.
            return chineseChapterDate.Replace(value, "$1월 $2일");
        }
        private bool ApplySaveMetadataFont(TMP_Text label)
        {
            bool chapter;
            if (!IsSaveMetadata(label, out chapter)) return false;
            TMP_FontAsset font = koreanBoldFont != null ? koreanBoldFont : koreanFont;
            if (font == null) return true;
            if (saveMetadataMaterial == null)
            {
                // Start from this font's own atlas, gradient scale and shader.
                // Keep Nanum Bold and its face weight; restore the original SaveSlot border.
                // Use a clean transparent material, without the old Chinese preset.
                saveMetadataMaterial = new Material(font.material);
                saveMetadataMaterial.name = font.name + "_SaveMetadataOutline";
                // Use the game's proven outline shader, as the dialogue path does.
                // The bundled font's default shader did not restore the visible border.
                Shader outlineShader = Shader.Find("TextMeshPro/Distance Field");
                if (outlineShader == null && label.fontSharedMaterial != null)
                    outlineShader = label.fontSharedMaterial.shader;
                if (outlineShader != null) saveMetadataMaterial.shader = outlineShader;
                saveMetadataMaterial.EnableKeyword("OUTLINE_ON");
                saveMetadataMaterial.DisableKeyword("UNDERLAY_ON");
                saveMetadataMaterial.DisableKeyword("UNDERLAY_INNER");
                saveMetadataMaterial.DisableKeyword("GLOW_ON");
                saveMetadataMaterial.SetFloat("_OutlineWidth", 0.4f);
                saveMetadataMaterial.SetFloat("_OutlineSoftness", 0f);
                saveMetadataMaterial.SetFloat("_FaceDilate", 0.32f);
                saveMetadataMaterial.SetColor("_FaceColor", Color.white);
                saveMetadataMaterial.SetColor("_OutlineColor", new Color(0.035601325f, 0.009134057f, 0.049706575f, 1f));
                saveMetadataMaterial.SetColor("_UnderlayColor", Color.clear);
                DontDestroyOnLoad(saveMetadataMaterial);
            }
            if (label.font != font) label.font = font;
            // The font asset is already bold; avoid synthetic bold/highlight styles.
            if (label.fontStyle != FontStyles.Normal) label.fontStyle = FontStyles.Normal;
            if (label.fontSharedMaterial != saveMetadataMaterial)
            {
                label.fontSharedMaterial = saveMetadataMaterial;
                label.UpdateMeshPadding();
            }
            if (saveMetadataLabels.Add(label.GetInstanceID()))
                Logger.LogInfo("저장 메타데이터 글꼴 통일: " + label.name + ", font=" + font.name + ", shader=" + saveMetadataMaterial.shader.name + ", outline=" + saveMetadataMaterial.GetFloat("_OutlineWidth"));
            return true;
        }

        private Component heartOwner;
        private static Vector3 previousHeartCorrection;
        private static Vector3 previousHeartPosition;
        private void LateUpdate()
        {
            if (heartOwner == null) {
                Type type = AccessTools.TypeByName("DialogueHandle");
                if (type == null) return;
                UnityEngine.Object[] owners = Resources.FindObjectsOfTypeAll(type);
                if (owners.Length == 0) return;
                heartOwner = owners[0] as Component;
            }
            if (heartOwner == null) return;
            TMP_Text activeDialogue = AccessTools.Field(heartOwner.GetType(), "mainDiagueText").GetValue(heartOwner) as TMP_Text;
            if (activeDialogue != null && koreanFont != null && NeedsKoreanFont(activeDialogue.text))
                ApplyFont(activeDialogue, activeDialogue.text);
            Image heart = AccessTools.Field(heartOwner.GetType(), "finishImage").GetValue(heartOwner) as Image;
            if (heart == null || !heart.gameObject.activeInHierarchy) return;
            AlignFinishHeart(heartOwner, true);
        }
        private static void AlignFinishHeart(object __instance, bool __result)
        {
            if (!__result) return;
            Type t = __instance.GetType();
            TMP_Text text = AccessTools.Field(t, "mainDiagueText").GetValue(__instance) as TMP_Text;
            Image heart = AccessTools.Field(t, "finishImage").GetValue(__instance) as Image;
            if (text == null || heart == null || heart.rectTransform.parent == null) return;
            TMP_TextInfo info = text.textInfo;
            int end = info.characterCount - 1;
            while (end >= 0 && !info.characterInfo[end].isVisible) end--;
            if (end < 0) return;
            TMP_CharacterInfo last = info.characterInfo[end];
            float top = float.MinValue, bottom = float.MaxValue;
            for (int i = end; i >= 0 && info.characterInfo[i].lineNumber == last.lineNumber; i--) {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible || !Char.IsLetterOrDigit(c.character)) continue;
                top = Mathf.Max(top, c.topLeft.y); bottom = Mathf.Min(bottom, c.bottomLeft.y);
            }
            if (top == float.MinValue) {
                TMP_LineInfo line = info.lineInfo[last.lineNumber];
                top = line.ascender; bottom = line.descender;
            }
            float gap = text.fontSize * 0.25f;
            TMP_Character space;
            if (last.fontAsset != null && last.fontAsset.characterLookupTable.TryGetValue(32, out space))
                gap = space.glyph.metrics.horizontalAdvance * last.scale;
            RectTransform rect = heart.rectTransform;
            Transform parent = rect.parent;
            Vector3 edge = parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(last.topRight.x + gap, (top + bottom) * 0.5f, 0)));
            Vector3 centerOffset = parent.InverseTransformVector(rect.TransformVector(rect.rect.center));
            Vector3 halfWidth = parent.InverseTransformVector(rect.TransformVector(new Vector3(rect.rect.width * 0.5f, 0, 0)));
            Vector3 pos = edge - centerOffset + halfWidth;
            pos.z = rect.localPosition.z;
            Vector3 originalBase = (Vector3)AccessTools.Field(t, "_finishImageBaseLocalPosition").GetValue(__instance);
            // DOTween updates Y only. Never add yesterday's X correction to the current X.
            float animatedY = rect.localPosition.y;
            if (Mathf.Approximately(animatedY, previousHeartPosition.y))
                animatedY -= previousHeartCorrection.y;
            float amplitude = Mathf.Abs((float)AccessTools.Field(t, "_finishImageFloatOffset").GetValue(__instance));
            float bob = Mathf.Clamp(animatedY - originalBase.y, -amplitude, amplitude);
            previousHeartCorrection = pos - originalBase;
            pos.y += bob;
            previousHeartPosition = pos;
            rect.localPosition = pos;

        }
        private static string SourceHash(string value)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "");
        }
        private void LoadDay1()
        {
            string path = Path.Combine(Paths.ConfigPath, "GalMaster_All_ko.tsv");
            if (!File.Exists(path)) path = Path.Combine(Paths.ConfigPath, "GalMaster_Day1_ko.tsv");
            if (!File.Exists(path)) { Logger.LogWarning("Day 1 번역 TSV가 없습니다."); return; }
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (String.IsNullOrWhiteSpace(raw) || raw.StartsWith("kind\t")) continue;
                string[] cols = raw.Split('\t');
                if (cols.Length < 7) continue;
                int index;
                if (!Int32.TryParse(cols[1], out index)) continue;
                if (cols[6] != "REVIEWED" && cols[6] != "REVIEWED_DAY1") continue;
                string key = cols[0] + ":" + index + ":" + (cols[0] == "option" ? cols[2] : "-1");
                expectedSources.Add(key, Unescape(cols[4]));
                string value = Unescape(cols[5]);
                if (String.IsNullOrWhiteSpace(value)) continue;
                if (cols[0] == "dialogue") day1Dialogue[index] = value;
                else if (cols[0] == "option") day1Options[index + ":" + cols[2]] = value;
            }
            Logger.LogInfo("전체 번역 데이터 읽음: 대사 " + day1Dialogue.Count + ", 선택지 " + day1Options.Count);
        }

        private static void ApplyDay1Postfix(object __instance)
        {
            if (instance == null) return;
            try
            {
                FieldInfo mainField = AccessTools.Field(__instance.GetType(), "MainData_SO");
                object main = mainField == null ? null : mainField.GetValue(__instance);
                FieldInfo listField = main == null ? null : AccessTools.Field(main.GetType(), "MainDataList");
                IList list = listField == null ? null : listField.GetValue(main) as IList;
                if (list == null) { instance.Logger.LogError("MainDataList를 찾지 못했습니다."); return; }
                int dialogueApplied = 0, optionApplied = 0;
                foreach (object row in list)
                {
                    FieldInfo indexField = AccessTools.Field(row.GetType(), "dialogueIndex");
                    if (indexField == null) continue;
                    int index = (int)indexField.GetValue(row);
                    string value;
                    if (instance.day1Dialogue.TryGetValue(index, out value))
                    {
                        FieldInfo dialogueField = AccessTools.Field(row.GetType(), "dialogue");
                        if (dialogueField != null) {
                            string key = "dialogue:" + index + ":-1";
                            if (SourceHash(Convert.ToString(dialogueField.GetValue(row))) != instance.expectedSources[key]) { instance.Logger.LogError("SOURCE_MISMATCH " + key); continue; }
                            dialogueField.SetValue(row, value); dialogueApplied++;
                        }
                    }
                    FieldInfo optionsField = AccessTools.Field(row.GetType(), "dialogueSelectOptions");
                    IList options = optionsField == null ? null : optionsField.GetValue(row) as IList;
                    if (options == null) continue;
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (instance.day1Options.TryGetValue(index + ":" + i, out value)) {
                            string key = "option:" + index + ":" + i;
                            if (SourceHash(Convert.ToString(options[i])) != instance.expectedSources[key]) { instance.Logger.LogError("SOURCE_MISMATCH " + key); continue; }
                            options[i] = value; optionApplied++;
                        }
                    }
                }
                instance.Logger.LogInfo("전체 한글 적용 완료: 대사 " + dialogueApplied + ", 선택지 " + optionApplied);
                int verified = 0;
                foreach (object row in list) {
                    Type t = row.GetType(); int idx = (int)AccessTools.Field(t, "dialogueIndex").GetValue(row);
                    string v;
                    if (instance.day1Dialogue.TryGetValue(idx, out v) && Convert.ToString(AccessTools.Field(t, "dialogue").GetValue(row)) == v) verified++;
                    IList opts = AccessTools.Field(t, "dialogueSelectOptions").GetValue(row) as IList;
                    if (opts != null) for (int i=0; i<opts.Count; i++) if (instance.day1Options.TryGetValue(idx + ":" + i, out v) && Convert.ToString(opts[i]) == v) verified++;
                }
                instance.Logger.LogInfo("TRANSLATION_READBACK " + verified + "/" + instance.expectedSources.Count);

            }
            catch (Exception ex) { instance.Logger.LogError(ex); }
        }

        private void LoadDictionary()
        {
            if (!File.Exists(dictionaryPath)) return;
            translations.Clear();
            foreach (string raw in File.ReadAllLines(dictionaryPath, Encoding.UTF8))
            {
                if (String.IsNullOrWhiteSpace(raw) || raw.StartsWith("#") || raw.StartsWith("source\t")) continue;
                string[] cols = raw.Split(new[] {'\t'}, 2);
                if (cols.Length == 2 && cols[0].Length > 0 && cols[1].Length > 0)
                    translations[Unescape(cols[0])] = Unescape(cols[1]);
            }
            dictionaryWriteTime = File.GetLastWriteTimeUtc(dictionaryPath);
        }

        private static string Unescape(string value)
        {
            return value.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");
        }

        private static string Escape(string value)
        {
            return value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static bool ContainsCjk(string value)
        {
            foreach (char c in value)
                if ((c >= 0x3400 && c <= 0x9fff) || (c >= 0xf900 && c <= 0xfaff)) return true;
            return false;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;
            if (koreanFont == null)
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Paths.ConfigPath, "galmaster_nanum_u2022"));
                if (bundle != null)
                {
                    TMP_FontAsset[] fonts = bundle.LoadAllAssets<TMP_FontAsset>();
                    foreach (TMP_FontAsset font in fonts) { DontDestroyOnLoad(font); if (font.name.EndsWith("_B")) koreanBoldFont = font; else if (font.name.EndsWith("_R")) koreanFont = font; }
                    Logger.LogInfo("나눔스퀘어라운드 로드: Regular=" + (koreanFont != null) + ", Bold=" + (koreanBoldFont != null));

                    bundle.Unload(false);
                }
            }
            if (File.Exists(dictionaryPath) && File.GetLastWriteTimeUtc(dictionaryPath) != dictionaryWriteTime)
            {
                LoadDictionary();
                Logger.LogInfo("UI 번역 사전 다시 읽음: " + translations.Count + "개 항목");
            }

            trackedText.RemoveWhere(x => x == null);
            trackedLegacy.RemoveWhere(x => x == null);
            trackedImages.RemoveWhere(x => x == null);
            shiftedMuteRows.RemoveWhere(x => x == null);
            rectSprites.RemoveWhere(x => x == null);
            foreach (TMP_Text label in trackedText) {
                if (!label.isActiveAndEnabled) continue;
                string value = NormalizeSaveChapter(label, Handle(label.text));
                if (label.text != value) label.text = value;
                CenterSpeakerName(label);
                if (koreanFont != null && NeedsKoreanFont(value)) ApplyFont(label, value);
                else ApplySaveMetadataFont(label);
            }
            foreach (UnityEngine.UI.Text label in trackedLegacy) {
                if (!label.isActiveAndEnabled) continue;
                string value = Handle(label.text);
                if (label.text != value) label.text = value;
            }
            // Release destroyed native objects; never treat a reused instance ID as ready.
            List<Texture2D> dead = null;
            foreach (Texture2D t in textureVersions.Keys) if (t == null) {
                if (dead == null) dead = new List<Texture2D>(); dead.Add(t);
            }
            if (dead != null) foreach (Texture2D t in dead) textureVersions.Remove(t);
        }

        private readonly HashSet<string> imagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Image> trackedImages = new HashSet<Image>();
        private readonly HashSet<TMP_Text> trackedText = new HashSet<TMP_Text>();
        private readonly HashSet<UnityEngine.UI.Text> trackedLegacy = new HashSet<UnityEngine.UI.Text>();
        private readonly Dictionary<Texture2D, uint> textureVersions = new Dictionary<Texture2D, uint>();
        private readonly Dictionary<string, byte[]> uiImageBytes = new Dictionary<string, byte[]>();
        private readonly HashSet<Sprite> rectSprites = new HashSet<Sprite>();
        private static void TextEnablePostfix(Component __instance) {
            RefreshEnabledText(__instance);
        }

        private static void PhoneSpritePrefix(Sprite __0) {
            if (instance != null && __0 != null) instance.PrepareTexture(__0.texture, false);
        }
        private static void PhoneEnablePrefix(Image __instance) {
            if (instance == null || __instance == null) return;
            instance.RegisterImage(__instance);
            Sprite sprite = __instance.overrideSprite;
            if (sprite != null) instance.PrepareTexture(sprite.texture, !sprite.texture.name.StartsWith("手机"));
        }
        private void RegisterImage(Image image) {
            if (image != null) trackedImages.Add(image);
        }
        private void PrepareTexture(Texture2D texture, bool force) {
            if (texture == null) return;
            uint version;
            if (!force && textureVersions.TryGetValue(texture, out version) && version == texture.updateCount) return;
            bool phone = texture.name.StartsWith("手机");
            string file = texture.name == "config_bg_text_sc" ? "config_bg_text_ko.png" : texture.name + "_ko.png";
            string path = phone ? PhoneImagePath(texture) : Path.Combine(Paths.ConfigPath, file);
            if (!imagePaths.Contains(path)) return;
            byte[] bytes;
            if (!uiImageBytes.TryGetValue(path, out bytes)) {
                if (!File.Exists(path)) return;
                bytes = File.ReadAllBytes(path);
                // Small menu atlases can be reused; do not retain every full-size phone PNG.
                if (!phone) uiImageBytes[path] = bytes;
            }
            try {
                if (ImageConversion.LoadImage(texture, bytes, false)) {
                    textureVersions[texture] = texture.updateCount;
                    Logger.LogInfo(texture.name + " 표시 전 한글 이미지 적용");
                }
            } catch (Exception e) { Logger.LogError("이미지 적용 실패: " + e.Message); }
        }
        private readonly HashSet<Image> shiftedMuteRows = new HashSet<Image>();
        private void AlignMuteControls(Image img)
        {
            if (img == null || img.name != "VoiceNoSound" || img.transform.parent == null || img.transform.parent.name != "SettingAction") return;
            if (!shiftedMuteRows.Add(img)) return;
            // Shift each character container: its checkbox and hit area are children.
            foreach (Transform child in img.transform) {
                RectTransform rect = child as RectTransform;
                if (rect != null) rect.anchoredPosition += new Vector2(32f, 0f);
            }
            Logger.LogInfo("캐릭터 음소거 버튼 네 쌍 오른쪽 32px 이동");
        }
        private void PrepareCanvas() {
            foreach (Image img in trackedImages) {
                if (img == null || !img.isActiveAndEnabled) continue;
                AlignMuteControls(img);
                Sprite original = img.overrideSprite;
                if (original == null) continue;
                PrepareTexture(original.texture, false);
                string name = original.texture.name;
                if ((name == "config_title" || name == "config_bg_text_sc") && !rectSprites.Contains(original)) {
                    Sprite replacement = Sprite.Create(original.texture, original.rect,
                        new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height),
                        original.pixelsPerUnit, 0, SpriteMeshType.FullRect, original.border);
                    rectSprites.Add(replacement);
                    img.sprite = replacement; img.overrideSprite = replacement; img.useSpriteMesh = false;
                }
            }
        }
        private void OnDestroy()
        {
            Canvas.preWillRenderCanvases -= PrepareCanvas;
        }

        private static string PhoneImagePath(Texture2D texture)
        {
            return Path.Combine(Path.Combine(Path.Combine(Paths.ConfigPath, "GalMaster_PhoneImages"), texture.width + "x" + texture.height), texture.name + "_ko.png");
        }

        private string Handle(string source)
        {
            if (String.IsNullOrWhiteSpace(source)) return source;
            string target;
            if (translations.TryGetValue(source, out target)) return target;
            if (!ContainsCjk(source) || !seen.Add(source)) return source;
            File.AppendAllText(capturePath, Escape(source) + "\t\n", new UTF8Encoding(false));
            Logger.LogInfo("UI 수집: " + Escape(source));
            return source;
        }

        private static bool NeedsKoreanFont(string value)
        {
            return !string.IsNullOrEmpty(value) && (ContainsHangul(value)
                || value.IndexOf('\u2014') >= 0 || value.IndexOf('\u2015') >= 0);
        }
        private static bool ContainsHangul(string value)
        {
            foreach (char c in value) if (c >= 0xac00 && c <= 0xd7a3) return true;
            return false;
        }
    }
}




namespace GalMasterKoreanPoC
{
    public sealed class PhoneKoreanOverlay : MonoBehaviour
    {
        private Image owner;
        private TMP_FontAsset font;
        private GameObject[] boxes = new GameObject[3];
        private TextMeshProUGUI[] texts = new TextMeshProUGUI[3];
        public void Initialize(Image image, TMP_FontAsset korean)
        {
            owner = image; font = korean;
            Make(0, .278f, .090f, .21f, .047f, new Color32(233,230,245,255));
            Make(1, .205f, .208f, .52f, .055f, new Color32(255,242,204,255));
            Make(2, .315f, .356f, .49f, .046f, new Color32(204,255,227,255));
        }
        private void Make(int i, float x, float y, float w, float h, Color color)
        {
            GameObject box = new GameObject("GalMaster_Korean_Phone_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            box.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)box.transform;
            rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Image background = box.GetComponent<Image>(); background.color = color; background.raycastTarget = false;
            GameObject text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            text.transform.SetParent(box.transform, false);
            RectTransform tr = (RectTransform)text.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            TextMeshProUGUI label = text.GetComponent<TextMeshProUGUI>();
            label.font = font; label.color = Color.black; label.alignment = TextAlignmentOptions.Midline; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = 8; label.fontSizeMax = 50;
            boxes[i] = box; texts[i] = label;
        }
        private void Position(int i, float x, float y, float w, float h)
        {
            Rect parent = owner.rectTransform.rect;
            Rect draw = owner.GetPixelAdjustedRect();
            if (owner.preserveAspect && owner.sprite != null)
            {
                float aspect = owner.sprite.rect.width / owner.sprite.rect.height;
                if (draw.width / draw.height > aspect)
                {
                    float width = draw.height * aspect;
                    draw.x += (draw.width-width) * owner.rectTransform.pivot.x;
                    draw.width = width;
                }
                else
                {
                    float height = draw.width / aspect;
                    draw.y += (draw.height-height) * owner.rectTransform.pivot.y;
                    draw.height = height;
                }
            }
            if (parent.width <= 0 || parent.height <= 0) return;
            RectTransform box = (RectTransform)boxes[i].transform;
            box.anchorMin = new Vector2((draw.x-parent.x+x*draw.width)/parent.width, (draw.y-parent.y+(1-y-h)*draw.height)/parent.height);
            box.anchorMax = new Vector2((draw.x-parent.x+(x+w)*draw.width)/parent.width, (draw.y-parent.y+(1-y)*draw.height)/parent.height);
            box.offsetMin = Vector2.zero; box.offsetMax = Vector2.zero;
            texts[i].fontSizeMin = 1;
            texts[i].fontSizeMax = draw.height * (i==0 ? .026f : .019f);
        }
        private void LateUpdate()
        {
            if (owner == null || texts[0] == null) return;
            Position(0, .278f, .090f, .21f, .047f);
            Position(1, .205f, .208f, .52f, .055f);
            Position(2, .315f, .356f, .49f, .046f);
            string name = owner.sprite == null ? "" : owner.sprite.texture.name;
            bool day1 = name == "手机1_1" || name == "手机1_2" || name == "手机1_3" || name == "手机1_4";
            bool other = name == "手机11_3";
            boxes[0].SetActive(owner.enabled && (day1 || other));
            boxes[1].SetActive(owner.enabled && day1 && name != "手机1_1");
            boxes[2].SetActive(owner.enabled && (name == "手机1_3" || name == "手机1_4"));
            texts[0].text = other ? "바이셴위" : "샤오예";
            texts[1].text = "나 '그래플링 하우스'에서 비 피하고 있어.\n도착하면 위치 확인해";
            texts[2].text = "거기가 어디야, 격투기 체육관?";
        }
    }
}








namespace GalMasterKoreanPoC
{
    public sealed class EndingKoreanSubtitles : MonoBehaviour
    {
        private struct Cue {
            public double Start, End; public string Text;
            public Cue(double start, double end, string text) { Start=start; End=end; Text=text; }
        }
        private static readonly Cue[] Bonus = new Cue[] {
            new Cue(0.6333d, 3.9333d, "벌써 끝인가요? 이야기는 이제 막 시작됐는데?!"),
            new Cue(4.4667d, 6.3d, "떠나고 싶지 않아."),
            new Cue(6.9333d, 10.1333d, "하지만, 그림밖에 그릴 줄 몰라서…"),
            new Cue(10.6667d, 13.3d, "아무것도 할 수 없어……"),
            new Cue(13.6667d, 15.6667d, "샤오청 선배는 낫죠."),
            new Cue(16.2d, 18.0333d, "전 게임밖에 할 줄 모르는데요."),
            new Cue(18.8d, 21.8d, "그러니까, 앞으로는 어떻게 해야 할까요?"),
            new Cue(21.9333d, 26.7d, "아… 그게, 아직은 잘 모르겠네요—"),
            new Cue(27.0d, 30.4333d, "그래도 리밍이랑 모두가 함께라면"),
            new Cue(30.4667d, 31.7667d, "분명 괜찮을 거예요!"),
            new Cue(32.2d, 35.2667d, "리밍이 있으면 안심돼."),
            new Cue(35.4333d, 37.6667d, "그… 그런가요?"),
            new Cue(38.0667d, 40.3d, "전 아직 선배에 대해 잘 모르지만,"),
            new Cue(40.7333d, 44.6d, "그래도 선배라면…… 열심히 방법을 찾아주겠죠."),
            new Cue(45.3333d, 47.8333d, "우리 오빠, 원래 이렇게 신뢰받는 사람이었나요?"),
            new Cue(48.1333d, 52.2667d, "그럼 분명 다음 이야기도 있겠죠… 그렇겠죠?!"),
            new Cue(52.5667d, 55.2667d, "다 같이 힘을 합쳐 미연시의 집을 구하고,"),
            new Cue(55.3d, 58.9d, "그러다 리밍과의 사이도 점점 가까워지고—"),
            new Cue(59.6333d, 62.1333d, "미연시에서는 보통 그렇게 흘러가잖아요."),
            new Cue(62.6d, 64.9d, "벌써 그런 얘기를 하기엔 너무 이르잖아요!?"),
            new Cue(65.2333d, 67.0333d, "……? 무슨 소리예요?"),
        };
        private static readonly Cue[] PV = new Cue[] {
            new Cue(2.35d, 3.25d, "이어져"),
            new Cue(3.5667d, 4.6499999999999995d, "계속 연결된 채로!"),
            new Cue(6.1833d, 8.0667d, "하나, 둘! 게임 시작!"),
            new Cue(28.400000000000002d, 30.1167d, "붓끝에 담은 약속"),
            new Cue(30.5d, 34.1167d, "대화창 맨 위에 그 사람 이름을 몰래 고정해"),
            new Cue(35.0d, 36.6667d, "표현이 서툰 이 마음"),
            new Cue(37.2167d, 40.9667d, "가끔 건네는 안부도 내 마음으로 받아줘"),
            new Cue(41.9d, 44.4d, "네 일상에 로그인해, 멋진 본보기가 되고 싶어"),
            new Cue(44.800000000000004d, 46.5833d, "현실과 가상의 경계에서"),
            new Cue(47.316700000000004d, 49.4167d, "망설임으로 가득한 생각들을"),
            new Cue(49.816700000000004d, 51.816700000000004d, "클릭 한 번으로 전부 지워"),
            new Cue(52.2167d, 53.7667d, "지금 확인을 눌러"),
            new Cue(54.9d, 56.6d, "내 곁에 함께 있어줘"),
            new Cue(57.4833d, 59.566700000000004d, "여름 바람이 푸른 발자취를 남기고"),
            new Cue(59.9333d, 61.5167d, "멈추지 않는 두근거림이 졸음을 쫓아내"),
            new Cue(61.8667d, 64.2667d, "가슴속을 맴도는 시곗바늘이 고요를 두드려"),
            new Cue(64.66669999999999d, 66.6d, "밀물이 물러가고, 달빛이 편지지를 적셔"),
            new Cue(67.0667d, 68.0667d, "너를 위해 적은 말들"),
            new Cue(68.6d, 71.1167d, "이 마음에 너와 나의 이야기를 저장해"),
            new Cue(71.46669999999999d, 73.05d, "편지를 봉해 바람에 멀리 보내"),
            new Cue(73.38329999999999d, 74.85d, "선택지들을 엮어 추억으로 만들어"),
            new Cue(75.19999999999999d, 77.8167d, "네게 가장 소중한 해피 엔딩을 선물할게"),
            new Cue(78.19999999999999d, 81.5833d, "서툴게, 간절하게, 말없이, 두근거리며 고백하는 모습"),
            new Cue(81.8667d, 85.5333d, "손을 내밀어, 맑은 하늘 아래 함께 우리의 기적을 이어가"),
        };

        private UnityEngine.Video.VideoPlayer player;
        private UnityEngine.Video.VideoClip clip;
        private Cue[] cues;
        private TextMeshProUGUI label;
        private Material subtitleMaterial;
        private RectTransform bounds;

        private float margin;
        private int lastIndex = -2;
        private Vector2 lastSize = Vector2.zero;
        public void Bind(UnityEngine.Video.VideoPlayer target, UnityEngine.Video.VideoClip video)
        {
            player=target; clip=video;
            cues=video.name=="PV" ? PV : video.name=="彩蛋" ? Bonus : null;
            margin=video.name=="彩蛋" ? 112f : 35f;
            lastIndex=-2; lastSize=Vector2.zero;
            if (label == null) {
                TMP_FontAsset font = Plugin.EndingFont;
                if (font == null) return;
                GameObject go = new GameObject("KoreanEndingSubtitles", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(transform, false);
                label=go.GetComponent<TextMeshProUGUI>();
                label.font=font; label.fontStyle=FontStyles.Normal;
                label.alignment=TextAlignmentOptions.Bottom; label.color=Color.white;
                label.enableWordWrapping=true; label.overflowMode=TextOverflowModes.Overflow;
                label.richText=false; label.raycastTarget=false;
                // SDF outline uses one glyph mesh, rather than offset copies of a raster glyph.
                subtitleMaterial=new Material(font.material);
                subtitleMaterial.name="KoreanEndingSubtitleSDF";
                Shader shader=Shader.Find("TextMeshPro/Distance Field");
                if(shader!=null) subtitleMaterial.shader=shader;
                subtitleMaterial.EnableKeyword("OUTLINE_ON");
                subtitleMaterial.DisableKeyword("UNDERLAY_ON");
                subtitleMaterial.DisableKeyword("UNDERLAY_INNER");
                subtitleMaterial.DisableKeyword("GLOW_ON");
                subtitleMaterial.SetFloat("_FaceDilate",0.32f);
                subtitleMaterial.SetFloat("_OutlineWidth",0.28f);
                subtitleMaterial.SetFloat("_OutlineSoftness",0f);
                subtitleMaterial.SetColor("_FaceColor",Color.white);
                subtitleMaterial.SetColor("_OutlineColor",new Color(57f/255f,32f/255f,58f/255f,1f));
                label.fontSharedMaterial=subtitleMaterial;
                label.UpdateMeshPadding();
                bounds=go.GetComponent<RectTransform>();
                bounds.anchorMin=Vector2.zero; bounds.anchorMax=Vector2.one;
            }
            label.text="";
            label.transform.SetAsLastSibling();
        }
        private void LateUpdate()
        {
            if (label == null) return;
            int index=-1;
            if (cues != null && player != null && player.clip == clip && player.isPrepared
                && (player.isPlaying || player.isPaused) && player.frame >= 0) {
                double time=player.time;
                for (int i=0;i<cues.Length;i++) if(time>=cues[i].Start && time<cues[i].End) { index=i; break; }
            }
            if (index!=lastIndex) { label.text=index<0 ? "" : cues[index].Text; lastIndex=index; }
            RectTransform host=transform as RectTransform;
            Vector2 size=host.rect.size;
            if (size!=lastSize) {
                // Fit the approved 1280x720 subtitle layout to the original video surface.
                float scale=Mathf.Min(size.x/1280f,size.y/720f);
                if(scale<=0f) return;
                Vector2 letterbox=(size-new Vector2(1280f,720f)*scale)*0.5f;
                label.fontSize=Mathf.Max(1,Mathf.RoundToInt(29f*scale));
                bounds.offsetMin=new Vector2(letterbox.x+60f*scale,letterbox.y+margin*scale);
                bounds.offsetMax=new Vector2(-letterbox.x-60f*scale,-letterbox.y);

                lastSize=size;
            }
        }
        private void OnDisable() { if(label!=null) label.text=""; lastIndex=-2; }
        private void OnDestroy() { if(subtitleMaterial!=null) Destroy(subtitleMaterial); }
    }
}
