using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZStudio.UIPathTween.Samples.Editor {
    public static class UIPathTweenDemoBuilder {
        private const string k_DemoRoot = "Assets/UIPathTween/Samples/Demo";
        private const string k_ScenePath = k_DemoRoot + "/Scenes/UIPathTweenTestScene.unity";

        [MenuItem("ZStudio/UIPathTween/Build Test Scene")]
        public static void BuildScene() {
            EnsureFolder("Assets/UIPathTween");
            EnsureFolder(k_DemoRoot);
            EnsureFolder(k_DemoRoot + "/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Canvas canvas = CreateCanvas();
            CreateEventSystemIfNeeded();

            RectTransform background = CreatePanel(
                canvas.transform,
                "Background",
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1600f, 900f),
                new Color(0.06f, 0.08f, 0.12f, 1f)
            );

            CreateLabel(
                background,
                "Title",
                "UIPathTween Test",
                new Vector2(0f, 380f),
                42,
                FontStyle.Bold,
                TextAnchor.MiddleCenter
            );

            CreateLabel(
                background,
                "Subtitle",
                "Bezier 模式：拖 Waypoint + 蓝/橙切线手柄调 S 形 · Scrub Path 预览 · Play 后点 Play Path",
                new Vector2(0f, 330f),
                20,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Color(0.75f, 0.8f, 0.9f, 1f)
            );

            RectTransform pathRoot = CreatePanel(
                background,
                "PathRoot",
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f),
                new Vector2(1200f, 520f),
                new Color(0.1f, 0.14f, 0.2f, 0.65f)
            );

            var pathTween = pathRoot.gameObject.AddComponent<UIPathTween>();

            RectTransform waypoint0 = CreateWaypoint(pathRoot, "Waypoint_0", new Vector2(-420f, -140f), Color.green);
            RectTransform waypoint1 = CreateWaypoint(pathRoot, "Waypoint_1", new Vector2(-160f, 160f), Color.yellow);
            RectTransform waypoint2 = CreateWaypoint(pathRoot, "Waypoint_2", new Vector2(180f, -150f), Color.yellow);
            RectTransform waypoint3 = CreateWaypoint(pathRoot, "Waypoint_3", new Vector2(420f, 130f), Color.red);

            ConfigureBezierWaypoint(waypoint0, new Vector2(140f, 60f), Vector2.zero);
            ConfigureBezierWaypoint(waypoint1, new Vector2(130f, -90f), new Vector2(-130f, 90f));
            ConfigureBezierWaypoint(waypoint2, new Vector2(120f, 100f), new Vector2(-120f, -100f));
            ConfigureBezierWaypoint(waypoint3, Vector2.zero, new Vector2(-140f, -50f));

            RectTransform coin = CreateCoin(pathRoot, "Coin", waypoint0.anchoredPosition);

            var pathSo = new SerializedObject(pathTween);
            pathSo.FindProperty("m_Target").objectReferenceValue = coin;
            pathSo.FindProperty("m_Duration").floatValue = 1.4f;
            SetEnumByName(pathSo.FindProperty("m_Ease"), "InOutSine");
            pathSo.FindProperty("m_CurveMode").enumValueIndex = (int)EUIPathCurveMode.Bezier;
            pathSo.FindProperty("m_SamplesPerSegment").intValue = 24;
            pathSo.FindProperty("m_SnapToStartOnPlay").boolValue = true;

            SerializedProperty waypointsProp = pathSo.FindProperty("m_Waypoints");
            waypointsProp.arraySize = 4;
            waypointsProp.GetArrayElementAtIndex(0).objectReferenceValue = waypoint0;
            waypointsProp.GetArrayElementAtIndex(1).objectReferenceValue = waypoint1;
            waypointsProp.GetArrayElementAtIndex(2).objectReferenceValue = waypoint2;
            waypointsProp.GetArrayElementAtIndex(3).objectReferenceValue = waypoint3;
            pathSo.ApplyModifiedPropertiesWithoutUndo();

            RectTransform controls = CreatePanel(
                background,
                "Controls",
                new Vector2(0.5f, 0f),
                new Vector2(0f, 60f),
                new Vector2(900f, 120f),
                new Color(0f, 0f, 0f, 0.25f)
            );

            Button playButton = CreateButton(
                controls,
                "PlayButton",
                "Play Path",
                new Vector2(-140f, 15f),
                new Vector2(220f, 56f)
            );

            Button resetButton = CreateButton(
                controls,
                "ResetButton",
                "Reset",
                new Vector2(120f, 15f),
                new Vector2(160f, 56f)
            );

            Toggle loopToggle = CreateToggle(controls, "LoopToggle", "Loop", new Vector2(320f, 15f));

            Text hintText = CreateLabel(
                controls,
                "Hint",
                "Select PathRoot to edit waypoints. Cyan curve = runtime path.",
                new Vector2(0f, -34f),
                18,
                FontStyle.Italic,
                TextAnchor.MiddleCenter,
                new Color(0.8f, 0.85f, 0.95f, 1f)
            );

            var demo = controls.gameObject.AddComponent<UIPathTweenDemo>();
            var demoSo = new SerializedObject(demo);
            demoSo.FindProperty("m_Path").objectReferenceValue = pathTween;
            demoSo.FindProperty("m_PlayButton").objectReferenceValue = playButton;
            demoSo.FindProperty("m_ResetButton").objectReferenceValue = resetButton;
            demoSo.FindProperty("m_LoopToggle").objectReferenceValue = loopToggle;
            demoSo.FindProperty("m_HintText").objectReferenceValue = hintText;
            demoSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, k_ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UIPathTween] Test scene saved to {k_ScenePath}");

            if (!Application.isBatchMode) {
                EditorSceneManager.OpenScene(k_ScenePath);
            }
        }

        private static void SetEnumByName(SerializedProperty property, string enumName) {
            int index = System.Array.IndexOf(property.enumNames, enumName);

            if (index >= 0) {
                property.enumValueIndex = index;
            }
        }

        private static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static Canvas CreateCanvas() {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        private static void CreateEventSystemIfNeeded() {
            if (Object.FindAnyObjectByType<EventSystem>() != null) {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform CreatePanel(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color
        ) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform CreateWaypoint(Transform parent, string name, Vector2 anchoredPosition, Color color) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UIPathWaypoint));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(24f, 24f);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void ConfigureBezierWaypoint(RectTransform waypoint, Vector2 tangentOut, Vector2 tangentIn) {
            var data = waypoint.GetComponent<UIPathWaypoint>();

            if (data == null) {
                return;
            }

            data.AutoTangents = false;
            data.TangentOut = tangentOut;
            data.TangentIn = tangentIn;
        }

        private static RectTransform CreateCoin(Transform parent, string name, Vector2 anchoredPosition) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(72f, 72f);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 0.78f, 0.15f, 1f);
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateLabel(
            RectTransform parent,
            string name,
            string text,
            Vector2 anchoredPosition,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Color? color = null
        ) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(1100f, 60f);

            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color ?? Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size
        ) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.45f, 0.95f, 1f);

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.28f, 0.55f, 1f, 1f);
            colors.pressedColor = new Color(0.12f, 0.32f, 0.75f, 1f);
            button.colors = colors;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }

        private static Toggle CreateToggle(RectTransform parent, string name, string label, Vector2 anchoredPosition) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(180f, 40f);

            var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundGo.transform.SetParent(go.transform, false);
            var bgRect = backgroundGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(28f, 28f);
            backgroundGo.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f, 1f);

            var checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(backgroundGo.transform, false);
            var checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(4f, 4f);
            checkRect.offsetMax = new Vector2(-4f, -4f);
            checkGo.GetComponent<Image>().color = new Color(0.3f, 0.85f, 0.45f, 1f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(36f, 0f);
            labelRect.offsetMax = Vector2.zero;

            var labelText = labelGo.GetComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 22;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = Color.white;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = backgroundGo.GetComponent<Image>();
            toggle.graphic = checkGo.GetComponent<Image>();
            toggle.isOn = true;
            return toggle;
        }
    }
}