using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Plugins.ButtonSoundsEditor.Editor
{
    /// <summary>
    /// Configures click sounds on UI objects in loaded scenes, with Undo support for edits.
    /// Window selection, previews, and repaint events do not themselves modify scene data.
    /// </summary>
    public class ButtonSoundsEditor : EditorWindow
    {
        private List<GameObject> _candidates = new List<GameObject>();
        private List<GameObject> _buttonCandidates = new List<GameObject>();
        private List<GameObject> _eventTriggerCandidates = new List<GameObject>();
        private GameObject _selectedCandidate;
        private ButtonSoundsEditorFilter _selectedFilter;

        private AudioSource _audioSource;
        private AudioClip _clickSound;
        private Vector2 _scrollPosition;

        #region Initialization

        [MenuItem("Window/Utils/Button sounds editor")]
        public static void OpenEditor()
        { 
            ButtonSoundsEditor window = GetWindow<ButtonSoundsEditor>();
            window.titleContent = new GUIContent("Button sounds editor");
            window.Initialize(); 
            window.Show();
        }

        /// <summary>Seeds window defaults from existing scene components without editing them.</summary>
        private void Initialize()
        {
            RefreshCandidates();
            ButtonClickSound[] clickSounds = _candidates.Select(_ => _.GetComponent<ButtonClickSound>()).Where(_ => _ != null).ToArray();
            _audioSource = GetFirstAudioSource(clickSounds);
            _clickSound = GetFirstClickSound(clickSounds);
        }

        private AudioSource GetFirstAudioSource(ButtonClickSound[] clickSounds)
        {
            ButtonClickSound buttonClickSound = clickSounds.FirstOrDefault(_ => _.AudioSource != null);
            return buttonClickSound == null ? null : buttonClickSound.AudioSource;
        }

        private AudioClip GetFirstClickSound(ButtonClickSound[] clickSounds)
        {
            ButtonClickSound buttonClickSound = clickSounds.FirstOrDefault(_ => _.ClickSound != null);
            return buttonClickSound == null ? null : buttonClickSound.ClickSound;
        }

        #endregion

        /// <summary>
        /// Collects editable scene instances, including inactive UI, while excluding prefab assets.
        /// Resources.FindObjectsOfTypeAll also sees imported assets, so scene filtering is required.
        /// </summary>
        private void RefreshCandidates()
        {
            _candidates.Clear();
            _buttonCandidates.Clear();
            _eventTriggerCandidates.Clear();

            var buttons = Resources.FindObjectsOfTypeAll<Button>()
                .Where(_ => !PrefabUtility.IsPartOfPrefabAsset(_) && _.gameObject.scene.IsValid() && _.gameObject.scene.isLoaded)
                .Select(_ => _.gameObject).ToList();
            _candidates.AddRange(buttons);
            _buttonCandidates.AddRange(buttons);

            var eventTriggers = Resources.FindObjectsOfTypeAll<EventTrigger>()
                .Where(_ => !PrefabUtility.IsPartOfPrefabAsset(_) && _.gameObject.scene.IsValid() && _.gameObject.scene.isLoaded &&
                            _.triggers.Any(e => e != null && e.eventID == EventTriggerType.PointerClick)).Select(_ => _.gameObject).ToList();
            _candidates.AddRange(eventTriggers);
            _eventTriggerCandidates.AddRange(eventTriggers);

            Func<GameObject, string> orderByFunc = _ => GetTransformPath(_.transform);
            // Objects with both input components belong to both filters, but bulk actions
            // must visit each GameObject once rather than adding/removing its sound twice.
            _candidates = _candidates.Distinct().OrderBy(orderByFunc).ToList();
            _buttonCandidates = _buttonCandidates.OrderBy(orderByFunc).ToList();
            _eventTriggerCandidates = _eventTriggerCandidates.OrderBy(orderByFunc).ToList();
        }

        private string GetTransformPath(Transform tr)
        {
            string path = tr.root.name;
            if (tr != tr.root)
                path += "/" + AnimationUtility.CalculateTransformPath(tr, tr.root);
            return path;
        }

        /// <summary>
        /// Draws the immediate-mode editor UI. Unity calls this for Layout and Repaint as
        /// well as input, so scene dirtying belongs only in handlers that actually mutate data.
        /// </summary>
        public void OnGUI()
        {
            RefreshCandidates();

            GUILayout.BeginVertical();
            DrawTopPanel();
            DrawMiddlePanel();
            DrawBottomPanel();
            GUILayout.EndVertical();

        }

        #region Top panel

        private void DrawTopPanel()
        {
            GUILayout.BeginVertical("Box");
            GUILayout.Space(5);

            DrawAudioSourceSettings();
          
            GUILayout.BeginHorizontal();

            GUILayout.Label("Click sound", GUILayout.Width(120));
            _clickSound = EditorGUILayout.ObjectField(_clickSound, typeof(AudioClip), false, GUILayout.Width(200)) as AudioClip;

            bool isEnabled = _audioSource != null && _clickSound != null;
            EditorGUI.BeginDisabledGroup(!isEnabled);
            GUILayout.Space(25f);
            if (GUILayout.Button(new GUIContent("Play", "Test assigned AudioClip."), GUILayout.Width(50)))
                _audioSource.PlayOneShot(_clickSound);
            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.EndVertical();
        }

        
        /// <summary>Chooses a shared source or creates an undoable scene object on demand.</summary>
        private void DrawAudioSourceSettings()
        {
            if (_audioSource == null)
                DrawTip("Tip: All buttons sounds are played using single AudioSource. \nAssign an existing AudioSource from the current scene or create a new AudioSource using 'Create' button!");

            GUILayout.BeginHorizontal();

            GUILayout.Label("Audio source", GUILayout.Width(120));
            _audioSource = EditorGUILayout.ObjectField(_audioSource, typeof(AudioSource), true, GUILayout.Width(200)) as AudioSource;

            GUILayout.Space(25f);
            if (GUILayout.Button(new GUIContent("Create", "Create new AudioSource"), GUILayout.Width(50)))
            {
                GameObject go = new GameObject("ButtonsAudioSource");
                // Register creation and component changes with Undo before configuring them.
                // Dirty the object's actual scene, which may differ in a multi-scene setup.
                Undo.RegisterCreatedObjectUndo(go, "Create button audio source");
                AudioSource audioSource = Undo.AddComponent<AudioSource>(go);
                Undo.RecordObject(audioSource, "Configure button audio source");
                audioSource.playOnAwake = false;
                _audioSource = audioSource;
                Selection.activeGameObject = go;
                EditorSceneManager.MarkSceneDirty(go.scene);
            }

            GUILayout.EndHorizontal();
        }

        #endregion

        #region Middle panel

        private void DrawMiddlePanel()
        {
            GUILayout.BeginVertical();

            if (IsNeedFilter())
            {
                DrawFilterPanel();
                GUILayout.Space(5f);
            }

            GUILayout.BeginHorizontal();
            DrawButtonsScrollView();
            DrawSelectedButtonInfoPanel();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private bool IsNeedFilter()
        {
            return _buttonCandidates.Any() && _eventTriggerCandidates.Any();
        }

        private void DrawFilterPanel()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Toggle(_selectedFilter == ButtonSoundsEditorFilter.Buttons, "Buttons", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                _selectedFilter = ButtonSoundsEditorFilter.Buttons;

            if (GUILayout.Toggle(_selectedFilter == ButtonSoundsEditorFilter.EventTriggers, "Event Triggers", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                _selectedFilter = ButtonSoundsEditorFilter.EventTriggers;

            GUILayout.EndHorizontal();
        }

        private void DrawButtonsScrollView()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.MinWidth(500));
            foreach (GameObject candidate in GetFilteredCandidates())
            {
                DrawButtonSettings(candidate);
                DrawButtonSettingsNotificationLine(candidate);
            }
            GUILayout.EndScrollView();
        }

        private List<GameObject> GetFilteredCandidates()
        {
            if (IsNeedFilter())
            {
                return _selectedFilter == ButtonSoundsEditorFilter.Buttons
                    ? _buttonCandidates
                    : _eventTriggerCandidates;
            }
            return _candidates;
        }

        /// <summary>Edits one component while leaving unchanged fields and previews read-only.</summary>
        private void DrawButtonSettings(GameObject candidate)
        {
            GUILayout.BeginHorizontal();

            MarkSelectedCandidate(candidate);

            ButtonClickSound clickSound = candidate.GetComponent<ButtonClickSound>();
            if (clickSound == null)
            {
                GUILayout.Label("No any click sound!", EditorStyles.whiteLabel, GUILayout.Width(225));

                if (GUILayout.Button(new GUIContent("Add", "Add 'ButtonClickSound' component to button."), GUILayout.Width(50)))
                {
                    AddButtonClickSound(candidate);
                    SelectButton(candidate);
                } 
            }
            else
            {
                AudioClip newClickSound = EditorGUILayout.ObjectField(clickSound.ClickSound, typeof(AudioClip), false, GUILayout.Width(200)) as AudioClip;
                // ObjectField runs during repaints too. The assignment helper compares old
                // and new values before recording Undo or marking any scene dirty.
                AssignClickSound(clickSound, clickSound.AudioSource, newClickSound);
                if (GUILayout.Button(new GUIContent("X", "Remove 'ButtonClickSound' component from button."), GUILayout.Width(20)))
                {
                    Undo.DestroyObjectImmediate(clickSound);
                    EditorSceneManager.MarkSceneDirty(candidate.scene);
                    SelectButton(candidate);
                    GUILayout.EndHorizontal();
                    // DestroyObjectImmediate invalidates the component now. End this row
                    // without reading its fields, and keep the GUILayout begin/end balanced.
                    return;
                }

                bool hasErrors = clickSound.AudioSource == null || clickSound.ClickSound == null;
                if (hasErrors)
                {
                    if (GUILayout.Button("Fix", GUILayout.Width(50)))
                    {
                        // Fill missing bindings without replacing a valid per-button override.
                        AssignClickSound(clickSound,
                            clickSound.AudioSource != null ? clickSound.AudioSource : _audioSource,
                            clickSound.ClickSound != null ? clickSound.ClickSound : _clickSound);
                    }
                }
                else
                {
                    // Preview playback changes no serialized fields and creates no Undo entry.
                    if (GUILayout.Button(new GUIContent("Play", "Test assigned AudioClip."), GUILayout.Width(50)))
                    {
                        clickSound.AudioSource.PlayOneShot(clickSound.ClickSound);
                        SelectButton(candidate);
                    }
                }
            }
             
            GUILayout.EndHorizontal();
        }

        private void MarkSelectedCandidate(GameObject candidate)
        {
            GUIStyle labelStyle = EditorStyles.label;
            Color originalColor = labelStyle.normal.textColor;
            if (candidate == _selectedCandidate)
                labelStyle.normal.textColor = new Color(0f, 0.5f, 0.5f);

            if (GUILayout.Button(candidate.name, labelStyle, GUILayout.Width(125)))
                SelectButton(candidate);

            labelStyle.normal.textColor = originalColor;
        }

        private void DrawButtonSettingsNotificationLine(GameObject candidate)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(135f);
            ButtonClickSound clickSound = candidate.GetComponent<ButtonClickSound>();
            if (clickSound != null)
            {
                if (clickSound.AudioSource == null)
                {
                    DrawTip("Audio Source is not assigned!");
                }
                else if (clickSound.ClickSound == null)
                {
                    DrawTip("Click Sound is not assigned!");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void SelectButton(GameObject candidate)
        {
            Selection.activeObject = candidate;
            _selectedCandidate = candidate;
        }

        /// <summary>Adds an undoable component and applies the window's current defaults.</summary>
        private void AddButtonClickSound(GameObject candidate)
        {
            ButtonClickSound buttonClickSound = Undo.AddComponent<ButtonClickSound>(candidate);
            AssignClickSound(buttonClickSound);
            EditorSceneManager.MarkSceneDirty(candidate.scene);
        }

        /// <summary>Applies both defaults for Add and the explicit bulk assignment action.</summary>
        private void AssignClickSound(ButtonClickSound buttonClickSound)
        {
            AssignClickSound(buttonClickSound, _audioSource, _clickSound);
        }

        /// <summary>
        /// Records a real property change once, including its prefab-instance override and scene.
        /// Returning early for equal values is essential because this also runs from OnGUI.
        /// </summary>
        private void AssignClickSound(ButtonClickSound buttonClickSound, AudioSource audioSource, AudioClip clickSound)
        {
            if (buttonClickSound.AudioSource == audioSource && buttonClickSound.ClickSound == clickSound)
                return;

            // RecordObject captures the previous values before mutation. Recording prefab
            // modifications afterwards preserves overrides when an instance is saved.
            Undo.RecordObject(buttonClickSound, "Assign button click sound");
            buttonClickSound.AudioSource = audioSource;
            buttonClickSound.ClickSound = clickSound;
            PrefabUtility.RecordPrefabInstancePropertyModifications(buttonClickSound);
            EditorSceneManager.MarkSceneDirty(buttonClickSound.gameObject.scene);
        }

        private void DrawSelectedButtonInfoPanel()
        {
            if (_selectedCandidate != null)
            {
                GUILayout.BeginVertical(GUILayout.Width(300));

                Image image = _selectedCandidate.GetComponent<Image>();
                if (image != null && image.sprite != null)
                    GUILayout.Box(image.sprite.texture);

                Text textComponent = _selectedCandidate.GetComponentInChildren<Text>();
                if (textComponent != null)
                    GUILayout.Label(string.Format("Text: '{0}'", textComponent.text));

                GUILayout.Label("Path:" + GetTransformPath(_selectedCandidate.transform), EditorStyles.wordWrappedLabel, GUILayout.Width(300));

                GUILayout.EndVertical();
            }
        }

        #endregion

        #region Bottom panel

        /// <summary>Runs explicit bulk edits over the deduplicated candidate collection.</summary>
        private void DrawBottomPanel()
        {
            GUILayout.BeginHorizontal("Box");

            DrawAuthorLink();

            GUILayout.Label("Version 1.1");

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(_clickSound == null);

            if (GUILayout.Button("Add click sound to ALL"))
            {
                foreach (GameObject candidate in _candidates)
                {
                    ButtonClickSound buttonClickSound = candidate.GetComponent<ButtonClickSound>();
                    if(buttonClickSound == null)
                        AddButtonClickSound(candidate);
                    else
                        AssignClickSound(buttonClickSound);
                }
            }

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Clear ALL"))
            {
                foreach (GameObject candidate in _candidates)
                {
                    ButtonClickSound buttonClickSound = candidate.GetComponent<ButtonClickSound>();
                    if (buttonClickSound != null)
                    {
                        Undo.DestroyObjectImmediate(buttonClickSound);
                        EditorSceneManager.MarkSceneDirty(candidate.scene);
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawTip(string message)
        {
            GUI.skin.label.normal.textColor = Color.red;
            GUILayout.Label(message);
            GUI.skin.label.normal.textColor = Color.black;
        }

        private void DrawAuthorLink()
        {
            if(GUILayout.Button("How To Use"))
            {
                Application.OpenURL("https://nubick.ru/button-sounds-editor-for-unity/?ref=editor");
            }
        }

        #endregion

        private enum ButtonSoundsEditorFilter
        {
            Buttons,
            EventTriggers
        }
    }
}
