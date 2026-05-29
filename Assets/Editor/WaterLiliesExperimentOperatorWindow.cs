using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class WaterLiliesExperimentOperatorWindow : EditorWindow
{
    const string WindowTitle = "Water Lilies Operator";

    static WaterLiliesExperimentOperatorWindow()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    WaterLiliesExperimentManager _manager;
    Vector2 _scroll;
    GUIStyle _phaseStyle;
    GUIStyle _nextActionStyle;
    GUIStyle _statusStyle;

    [MenuItem("Window/Water Lilies/Operator Panel")]
    public static void ShowWindow()
    {
        var window = GetWindow<WaterLiliesExperimentOperatorWindow>(WindowTitle);
        window.minSize = new Vector2(520f, 420f);
        window.FindManager();
        window.Show();
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            var manager = FindManagerInOpenScenes();
            if (manager != null && manager.operatorUiEnabled)
            {
                ShowWindow();
            }
        };
    }

    void OnEnable()
    {
        FindManager();
    }

    void Update()
    {
        if (EditorApplication.isPlaying)
        {
            Repaint();
        }
    }

    void OnGUI()
    {
        EnsureStyles();
        DrawToolbar();

        if (_manager == null)
        {
            EditorGUILayout.HelpBox("No WaterLiliesExperimentManager was found in the open scene.", MessageType.Info);
            if (GUILayout.Button("Find Manager"))
            {
                FindManager();
            }

            return;
        }

        DrawPhaseHeader();
        DrawControls();
        DrawStatus();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Find", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                FindManager();
            }

            if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(56f)) && _manager != null)
            {
                Selection.activeObject = _manager.gameObject;
                EditorGUIUtility.PingObject(_manager.gameObject);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(EditorApplication.isPlaying ? "Play Mode" : "Edit Mode", EditorStyles.miniLabel, GUILayout.Width(72f));
        }

        _manager = (WaterLiliesExperimentManager)EditorGUILayout.ObjectField("Manager", _manager, typeof(WaterLiliesExperimentManager), true);
    }

    void DrawPhaseHeader()
    {
        var phaseRect = GUILayoutUtility.GetRect(10f, 64f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(phaseRect, new Color(0.02f, 0.18f, 0.22f, 1f));
        EditorGUI.LabelField(new Rect(phaseRect.x + 12f, phaseRect.y + 8f, phaseRect.width - 24f, 26f), _manager.operatorPhaseText, _phaseStyle);
        EditorGUI.LabelField(new Rect(phaseRect.x + 12f, phaseRect.y + 36f, phaseRect.width - 24f, 22f), _manager.operatorNextActionText, _nextActionStyle);
    }

    void DrawControls()
    {
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start", GUILayout.Height(32f)))
                {
                    _manager.StartExperiment();
                }

                if (GUILayout.Button("Continue", GUILayout.Height(32f)))
                {
                    _manager.ContinueCurrentBreak();
                }

                if (GUILayout.Button("Removed", GUILayout.Height(32f)))
                {
                    _manager.MarkHeadsetRemoved();
                }

                if (GUILayout.Button("Worn", GUILayout.Height(32f)))
                {
                    _manager.MarkHeadsetWorn();
                }

                if (GUILayout.Button("Skip", GUILayout.Height(32f)))
                {
                    _manager.RequestPilotSkip();
                }
            }

            if (GUILayout.Button("Abort Experiment", GUILayout.Height(28f)))
            {
                _manager.AbortExperiment();
            }
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use operator controls.", MessageType.None);
        }
    }

    void DrawStatus()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(_manager.operatorStatusText, _statusStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void EnsureStyles()
    {
        if (_statusStyle != null)
        {
            return;
        }

        _phaseStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            normal = { textColor = Color.white },
            clipping = TextClipping.Clip
        };

        _nextActionStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = new Color(1f, 0.92f, 0.42f, 1f) }
        };

        _statusStyle = new GUIStyle(EditorStyles.textArea)
        {
            fontSize = 13,
            wordWrap = true
        };
    }

    void FindManager()
    {
        _manager = FindManagerInOpenScenes();
    }

    static WaterLiliesExperimentManager FindManagerInOpenScenes()
    {
        var managers = Resources.FindObjectsOfTypeAll<WaterLiliesExperimentManager>();
        for (var i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager == null || EditorUtility.IsPersistent(manager))
            {
                continue;
            }

            var scene = manager.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                return manager;
            }
        }

        return null;
    }
}
