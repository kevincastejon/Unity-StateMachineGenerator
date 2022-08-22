﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace KevinCastejon.StateMachineGenerator
{
    public enum InitialStateMode
    {
        NONE,
        SETSTATE,
        TRANSITION
    }
    public class StateMachineGenerator : EditorWindow
    {
        private string _enumName;
        private List<string> _states;
        private ReorderableList _list;
        private int _defaultState = 0;
        private bool _publicTransitionMethod = false;
        private bool _useRegions = false;
        private bool _spaceRegions = false;
        private bool _groupByPhase = false;
        private InitialStateMode _initialStateMode;
        private bool _codeStyleOpen = false;
        private bool _initialStateOpen = false;
        private Vector2 _scrollPos;
        private float _currentScrollViewHeight;
        private bool _resize;
        private static GUIStyle _ErrorStyle;
        private static GUIStyle _BoldStyle;
        private static TextInfo _TInfo;

        [MenuItem("Window/StateMachineGenerator")]
        public static void ShowWindow()
        {
            EditorWindow window = GetWindow(typeof(StateMachineGenerator));
            window.minSize = new Vector2(550, 400);
            window.titleContent = new GUIContent("StateMachine Generator");
        }

        private void OnEnable()
        {
            _currentScrollViewHeight = this.position.height * 0.5f;
            if (_states == null)
            {
                _states = new List<string>();
            }
            if (_list == null)
            {
                InitializeList();
            }
        }

        private void InitializeList()
        {
            _list = new ReorderableList(_states, typeof(string), true, true, false, false);
            _list.drawElementCallback = DrawElementCallback;
            _list.drawHeaderCallback = DrawHeaderCallback;
        }

        private void DrawHeaderCallback(Rect rect)
        {
            EditorGUI.LabelField(rect, "States", EditorStyles.boldLabel);
        }

        private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.DelayedTextField(new Rect(rect.x, rect.y, rect.width * 0.7f, rect.height), _states[index]).Trim().ToUpper().Replace(" ", "_");
            if (EditorGUI.EndChangeCheck() && newName != _states[index])
            {
                _states[index] = MakeNameUnique(newName);
            }
            EditorGUI.BeginDisabledGroup(_initialStateMode == InitialStateMode.NONE);
            bool isDefault = EditorGUI.ToggleLeft(new Rect(rect.width * 0.8f, rect.y, rect.width * 0.3f, rect.height), _defaultState == index ? "Default" : "", _defaultState == index);
            EditorGUI.EndDisabledGroup();
            if (isDefault)
            {
                _defaultState = index;
            }
        }

        private void OnRemoveCallback()
        {
            _states.RemoveAt(_list.index);
            if (_list.count > 0)
            {
                _list.index = _list.index - 1 >= 0 ? _list.index - 1 : 0;
            }
            else
            {
                _list.index = -1;
            }
        }

        private void OnAddCallback()
        {
            int index = _list.count == 0 ? 0 : _list.index + 1;
            _states.Insert(index, MakeNameUnique("NEW_STATE"));
            _list.Select(index);
        }

        void OnGUI()
        {
            _ErrorStyle = new GUIStyle(EditorStyles.label);
            _BoldStyle = new GUIStyle(EditorStyles.label);
            _TInfo = new CultureInfo("en-US", false).TextInfo;
            _ErrorStyle.normal.textColor = Color.red;
            _BoldStyle.fontStyle = FontStyle.Bold;
            _enumName = EditorGUILayout.TextField(new GUIContent("Enum name", "Enter a name for the Enum holding your state machine states"), "MyEnum");
            //EditorGUILayout.BeginHorizontal();
            //int newCount = Mathf.Max(0, EditorGUILayout.DelayedIntField("Number of states", _states.Count));
            //if (GUILayout.Button("-"))
            //{
            //    newCount--;
            //}
            //if (GUILayout.Button("+"))
            //{
            //    newCount++;
            //}
            //EditorGUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(_currentScrollViewHeight - 40f));
            Rect topRect = EditorGUILayout.GetControlRect(false, _list.GetHeight() - 22f);
            _list.DoList(topRect);
            //while (newCount < _states.Count)
            //    _states.RemoveAt(_states.Count - 1);
            //while (newCount > _states.Count)
            //    _states.Add("    STATE_NAME_" + _states.Count);
            //for (int i = 0; i < _states.Count; i++)
            //{
            //    EditorGUILayout.BeginHorizontal();
            //    _states[i] = EditorGUILayout.TextField("  State " + i, _states[i]).Trim().ToUpper().Replace(" ", "_");
            //    EditorGUI.BeginDisabledGroup(_initialStateMode == InitialStateMode.NONE);
            //    bool isDefault = EditorGUILayout.ToggleLeft(_defaultState == i ? "Default" : "", _defaultState == i);
            //    EditorGUI.EndDisabledGroup();
            //    if (isDefault)
            //    {
            //        _defaultState = i;
            //    }
            //    EditorGUILayout.EndHorizontal();
            //}
            GUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(_list.count == 0);
            if (GUILayout.Button("-"))
            {
                OnRemoveCallback();
                Repaint();
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("+"))
            {
                OnAddCallback();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            ResizeScrollView();
            if (_defaultState >= _states.Count)
            {
                _defaultState = 0;
            }
            if (_states.Count > 0)
            {
                _publicTransitionMethod = EditorGUILayout.Toggle(new GUIContent("Public transition method", "Check it if you want the TransitionToState method to be public"), _publicTransitionMethod);
                _initialStateOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_initialStateOpen, "Initial state parameters");
                if (_initialStateOpen)
                {
                    bool noInitial;
                    bool initState;
                    bool initTrans;
                    EditorGUI.BeginChangeCheck();
                    noInitial = EditorGUILayout.Toggle(new GUIContent("None", "The default state will be the first state"), _initialStateMode == InitialStateMode.NONE);
                    if (EditorGUI.EndChangeCheck() && noInitial)
                    {
                        _initialStateMode = InitialStateMode.NONE;
                    }
                    EditorGUI.BeginChangeCheck();
                    initState = EditorGUILayout.Toggle(new GUIContent("Initial state", "The default state will be the one that is checked \"default\""), _initialStateMode == InitialStateMode.SETSTATE);
                    if (EditorGUI.EndChangeCheck() && initState)
                    {
                        _initialStateMode = InitialStateMode.SETSTATE;
                    }
                    EditorGUI.BeginChangeCheck();
                    initTrans = EditorGUILayout.Toggle(new GUIContent("Initial transition", "A proper transition to the state that is checked \"default\" will happen at start"), _initialStateMode == InitialStateMode.TRANSITION);
                    if (EditorGUI.EndChangeCheck() && initTrans)
                    {
                        _initialStateMode = InitialStateMode.TRANSITION;
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                _codeStyleOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_codeStyleOpen, "Code styling parameters");
                if (_codeStyleOpen)
                {
                    EditorGUILayout.BeginHorizontal();
                    _useRegions = EditorGUILayout.Toggle("Use regions", _useRegions);
                    if (_useRegions)
                    {
                        _spaceRegions = EditorGUILayout.Toggle("Space regions", _spaceRegions);
                    }
                    else
                    {
                        _spaceRegions = false;
                    }
                    EditorGUILayout.EndHorizontal();
                    _groupByPhase = !EditorGUILayout.ToggleLeft(new GUIContent("Group by state", "Methods will be grouped by state"), !_groupByPhase);
                    _groupByPhase = EditorGUILayout.ToggleLeft(new GUIContent("Group by phase", "Methods will be grouped by phase (enter/update/exit)"), _groupByPhase);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                if (GUILayout.Button("Generate"))
                {
                    Generate();
                }
            }
        }

        private void ResizeScrollView()
        {
            _currentScrollViewHeight = Mathf.Clamp(_currentScrollViewHeight, Mathf.Min(190f, 45f + _list.GetHeight() - 22f), Mathf.Min(45f + _list.GetHeight() - 22f, position.height - 110f - (_codeStyleOpen ? 60f : 0f) - (_initialStateOpen ? 60f : 0f)));
            if (_states.Count > 5)
            {
                Rect orect = EditorGUILayout.GetControlRect(false, 20f);
                float handlesWidth = 15f;
                Rect cursorChangeRect = new Rect(orect.x, _currentScrollViewHeight + 10f, orect.width, 5f);
                Rect cursorChangeTopRect = new Rect(orect.width * 0.5f - handlesWidth * 0.5f + 5f, _currentScrollViewHeight + 10f, handlesWidth, 1f);
                Rect cursorChangeBottomRect = new Rect(orect.width * 0.5f - handlesWidth * 0.5f + 5f, _currentScrollViewHeight + 10f + 4f, handlesWidth, 1f);
                Texture2D handleBar = Texture2D.grayTexture;
                Texture2D handles = Texture2D.whiteTexture;
                EditorGUIUtility.AddCursorRect(cursorChangeRect, MouseCursor.ResizeVertical);
                if (Event.current.type == EventType.MouseDown && cursorChangeRect.Contains(Event.current.mousePosition))
                {
                    _resize = true;
                }
                if (_resize)
                {
                    _currentScrollViewHeight = Mathf.Clamp(Event.current.mousePosition.y - 10f, Mathf.Min(190f, 45f + _list.GetHeight() - 22f), Mathf.Min(45f + _list.GetHeight() - 22f, position.height - 110f - (_codeStyleOpen ? 60f : 0f) - (_initialStateOpen ? 60f : 0f)));
                }
                GUI.DrawTexture(cursorChangeRect, handleBar);
                GUI.DrawTexture(cursorChangeTopRect, handles);
                GUI.DrawTexture(cursorChangeBottomRect, handles);
                if (Event.current.type == EventType.MouseUp)
                {
                    _resize = false;
                }
            }
            Repaint();
        }

        void Generate()
        {
            if (_states.Count == 0)
            {
                return;
            }
            string copyPath = EditorUtility.SaveFilePanel("Save the state machine script", Application.dataPath, "MyStateMachine", "cs");
            if (!string.IsNullOrEmpty(copyPath))
            {
                string className = Path.GetFileNameWithoutExtension(copyPath);
                using (StreamWriter outfile = new StreamWriter(copyPath))
                {
                    outfile.WriteLine($"using UnityEngine;");
                    outfile.WriteLine($"");
                    if (_useRegions) outfile.WriteLine($"#region States");
                    outfile.WriteLine($"public enum {_enumName}");
                    outfile.WriteLine($"{{");
                    foreach (string state in _states)
                    {
                        outfile.WriteLine($"    {state},");
                    }
                    outfile.WriteLine($"}}");
                    outfile.WriteLine($"");
                    if (_useRegions) outfile.WriteLine($"#endregion");
                    outfile.WriteLine($"");
                    outfile.WriteLine($"public class {className} : MonoBehaviour");
                    outfile.WriteLine($"{{");
                    outfile.WriteLine($"");
                    outfile.WriteLine($"    private {_enumName} _currentState;");
                    outfile.WriteLine($"");
                    if (_useRegions) outfile.WriteLine($"    #region Public properties");
                    outfile.WriteLine($"    public {_enumName} CurrentState {{ get => _currentState; private set => _currentState = value; }}");
                    if (_useRegions) outfile.WriteLine($"    #endregion");
                    if (_spaceRegions) outfile.WriteLine($"");
                    if (_useRegions) outfile.WriteLine($"    #region Init");
                    outfile.WriteLine($"    private void Start()");
                    outfile.WriteLine($"    {{");
                    if (_initialStateMode == InitialStateMode.SETSTATE)
                    {
                        outfile.WriteLine($"        CurrentState = {_enumName}.{_states[_defaultState]};");
                    }
                    else if (_initialStateMode == InitialStateMode.TRANSITION)
                    {
                        string methodName = _TInfo.ToTitleCase(_states[_defaultState].Replace("_", " ").ToLower()).Replace(" ", "");
                        outfile.WriteLine($"        CurrentState = {_enumName}.{_states[_defaultState]};");
                        outfile.WriteLine($"        OnEnter{methodName}();");
                    }
                    outfile.WriteLine($"    }}");
                    if (_useRegions) outfile.WriteLine($"    #endregion");
                    if (_spaceRegions) outfile.WriteLine($"");
                    if (_useRegions) outfile.WriteLine($"    #region Update");
                    outfile.WriteLine($"    private void Update()");
                    outfile.WriteLine($"    {{");
                    outfile.WriteLine($"        OnStateUpdate(CurrentState);");
                    outfile.WriteLine($"    }}");
                    if (_useRegions) outfile.WriteLine($"    #endregion");
                    if (_spaceRegions) outfile.WriteLine($"");
                    if (_useRegions) outfile.WriteLine($"    #region State Machine");
                    outfile.WriteLine($"    private void OnStateEnter({_enumName} state)");
                    outfile.WriteLine($"    {{");
                    outfile.WriteLine($"        switch (state)");
                    outfile.WriteLine($"        {{");
                    foreach (string state in _states)
                    {
                        string stateMethod = _TInfo.ToTitleCase(state.Replace("_", " ").ToLower()).Replace(" ", "");
                        outfile.WriteLine($"            case {_enumName}.{state}:");
                        outfile.WriteLine($"                OnEnter{stateMethod}();");
                        outfile.WriteLine($"                break;");
                    }
                    outfile.WriteLine($"            default:");
                    outfile.WriteLine($"                Debug.LogError(\"OnStateEnter: Invalid state \" + state.ToString());");
                    outfile.WriteLine($"                break;");
                    outfile.WriteLine($"        }}");
                    outfile.WriteLine($"    }}");

                    outfile.WriteLine($"    private void OnStateUpdate({_enumName} state)");
                    outfile.WriteLine($"    {{");
                    outfile.WriteLine($"        switch (state)");
                    outfile.WriteLine($"        {{");
                    foreach (string state in _states)
                    {
                        string stateMethod = _TInfo.ToTitleCase(state.Replace("_", " ").ToLower()).Replace(" ", "");
                        outfile.WriteLine($"            case {_enumName}.{state}:");
                        outfile.WriteLine($"                OnUpdate{stateMethod}();");
                        outfile.WriteLine($"                break;");
                    }
                    outfile.WriteLine($"            default:");
                    outfile.WriteLine($"                Debug.LogError(\"OnStateUpdate: Invalid state \" + state.ToString());");
                    outfile.WriteLine($"                break;");
                    outfile.WriteLine($"        }}");
                    outfile.WriteLine($"    }}");
                    outfile.WriteLine($"    private void OnStateExit({_enumName} state)");
                    outfile.WriteLine($"    {{");
                    outfile.WriteLine($"        switch (state)");
                    outfile.WriteLine($"        {{");
                    foreach (string state in _states)
                    {
                        string stateMethod = _TInfo.ToTitleCase(state.Replace("_", " ").ToLower()).Replace(" ", "");
                        outfile.WriteLine($"            case {_enumName}.{state}:");
                        outfile.WriteLine($"                OnExit{stateMethod}();");
                        outfile.WriteLine($"                break;");
                    }
                    outfile.WriteLine($"            default:");
                    outfile.WriteLine($"                Debug.LogError(\"OnStateExit: Invalid state \" + state.ToString());");
                    outfile.WriteLine($"                break;");
                    outfile.WriteLine($"        }}");
                    outfile.WriteLine($"    }}");
                    string accessibility = _publicTransitionMethod ? "public" : "private";
                    outfile.WriteLine($"    {accessibility} void TransitionToState({_enumName} toState)");
                    outfile.WriteLine($"    {{");
                    outfile.WriteLine($"        OnStateExit(CurrentState);");
                    outfile.WriteLine($"        CurrentState = toState;");
                    outfile.WriteLine($"        OnStateEnter(toState);");
                    outfile.WriteLine($"    }}");
                    if (_useRegions) outfile.WriteLine($"    #endregion");
                    if (_spaceRegions) outfile.WriteLine($"");
                    if (!_groupByPhase)
                    {
                        foreach (string state in _states)
                        {
                            string stateMethod = _TInfo.ToTitleCase(state.Replace("_", " ").ToLower()).Replace(" ", "");
                            if (_useRegions) outfile.WriteLine($"    #region State {state}");
                            outfile.WriteLine($"    private void OnEnter{stateMethod}()");
                            outfile.WriteLine($"    {{");
                            outfile.WriteLine($"    }}");
                            outfile.WriteLine($"    private void OnUpdate{stateMethod}()");
                            outfile.WriteLine($"    {{");
                            outfile.WriteLine($"    }}");
                            outfile.WriteLine($"    private void OnExit{stateMethod}()");
                            outfile.WriteLine($"    {{");
                            outfile.WriteLine($"    }}");
                            if (_useRegions) outfile.WriteLine($"    #endregion");
                            if (_spaceRegions) outfile.WriteLine($"");
                        }
                    }
                    else
                    {
                        if (_useRegions) outfile.WriteLine($"    #region EnterState");
                        foreach (string state in _states)
                        {
                            string stateMethod = _TInfo.ToTitleCase(state.Replace("_", " ").ToLower()).Replace(" ", "");
                            outfile.WriteLine($"    private void OnEnter{stateMethod}()");
                            outfile.WriteLine($"    {{");
                            outfile.WriteLine($"    }}");
                        }
                        if (_useRegions) outfile.WriteLine($"    #region UpdateState");
                        foreach (string state in _states)
                        {
                            string stateMethod = _TInfo.ToTitleCase(state.Replace("_", " ").ToLower()).Replace(" ", "");
                            outfile.WriteLine($"    private void OnUpdate{stateMethod}()");
                            outfile.WriteLine($"    {{");
                            outfile.WriteLine($"    }}");
                        }
                        if (_useRegions) outfile.WriteLine($"    #region ExitState");
                        foreach (string state in _states)
                        {
                            string stateMethod = _TInfo.ToTitleCase(state.Replace("_", " ").ToLower()).Replace(" ", "");
                            outfile.WriteLine($"    private void OnExit{stateMethod}()");
                            outfile.WriteLine($"    {{");
                            outfile.WriteLine($"    }}");
                        }
                    }
                    outfile.WriteLine($"}}");
                }
            }
            AssetDatabase.Refresh();
        }

        private string MakeNameUnique(string newName)
        {
            while (!IsNameUnique(newName))
            {
                newName += "_";
            }
            return newName;
        }

        private bool IsNameUnique(string newName)
        {
            return !_states.Contains(newName);
        }
    }
}