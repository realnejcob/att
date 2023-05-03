﻿// File: DevConsoleMono.cs
// Purpose: Implementation of the developer console as an internal component
// Created by: DavidFDev

// Hide the dev console objects in the hierarchy and inspector
#define HIDE_FROM_EDITOR

#if INPUT_SYSTEM_INSTALLED && ENABLE_INPUT_SYSTEM
#define USE_NEW_INPUT_SYSTEM
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Runtime.CompilerServices;
using System.Collections;
using Mono.CSharp;
using Enum = System.Enum;
#if INPUT_SYSTEM_INSTALLED
using UnityEngine.InputSystem;
#endif

using InputKey =
#if USE_NEW_INPUT_SYSTEM
    UnityEngine.InputSystem.Key;
#else
    UnityEngine.KeyCode;
#endif

namespace DavidFDev.DevConsole
{
#if HIDE_FROM_EDITOR
    [AddComponentMenu("")]
#endif
    internal sealed class DevConsoleMono : MonoBehaviour
    {
        #region Static fields and constants

        private const string ErrorColour = "#E99497";
        private const string WarningColour = "#DEBF1F";
        private const string SuccessColour = "#B3E283";
        private const string ClearLogText = "Type <b>devconsole</b> for instructions on how to use the developer console.";
        private const int MaximumTextVertices = 64000;
        private const float MinConsoleWidth = 650;
        private const float MaxConsoleWidth = 1600;
        private const float MinConsoleHeight = 200;
        private const float MaxConsoleHeight = 900;
        private const int MinLogTextSize = 14;
        private const int MaxLogTextSize = 35;
        private const int CommandHistoryLength = 10;
        private const int MaxCachedEnumTypes = 6;
        private const float FpsUpdateRate = 4f;
        private const float StatUpdateRate = 0.1f;
        private const int StatDefaultFontSize = 18;
        private const string MonoNotSupportedText = "C# expression evaluation is not supported on this platform.";

        #region Input constants

        private const InputKey DefaultToggleKey =
#if USE_NEW_INPUT_SYSTEM
            InputKey.Backquote;
#else
            InputKey.BackQuote;
#endif
        private const InputKey UpArrowKey = InputKey.UpArrow;
        private const InputKey DownArrowKey = InputKey.DownArrow;
        private const InputKey BackspaceKey = InputKey.Backspace;
        private const InputKey LeftControlKey =
#if USE_NEW_INPUT_SYSTEM
            InputKey.LeftCtrl;
#else
            InputKey.LeftControl;
#endif
        private const string InputSystemPrefabPath = "Prefabs/" +
#if USE_NEW_INPUT_SYSTEM
            "FAB_DevConsole.NewEventSystem";
#else
            "FAB_DevConsole.OldEventSystem";
#endif

        #endregion

        #region File data constants

        private const string PrefConsoleToggleKey =
#if USE_NEW_INPUT_SYSTEM
            "DevConsole.newConsoleToggleKey";
#else
            "DevConsole.legacyConsoleToggleKey";
#endif
        private const string PrefBindings =
#if USE_NEW_INPUT_SYSTEM
            "DevConsole.newBindings";
#else
            "DevConsole.legacyBindings";
#endif
        private const string PrefDisplayUnityLogs = "DevConsole.displayUnityLogs";
        private const string PrefDisplayUnityErrors = "DevConsole.displayUnityErrors";
        private const string PrefDisplayUnityExceptions = "DevConsole.displayUnityExceptions";
        private const string PrefDisplayUnityWarnings = "DevConsole.displayUnityWarnings";
        private const string PrefShowFps = "DevConsole.displayFps";
        private const string PrefLogTextSize = "DevConsole.logTextSize";
        private const string PrefIncludedUsings = "DevConsole.includedUsings";
        private const string PrefShowStats = "DevConsole.displayStats";
        private const string PrefStats = "DevConsole.stats";
        private const string PrefHiddenStats = "DevConsole.hiddenStats";
        private const string PrefStatsFontSize = "DevConsole.statsFontSize";
        private const string PrefPersistStats = "DevConsole.persistStats";

        #endregion

        private static readonly Version _version = new Version(1, 0, 5);
        private static readonly string[] _permanentCommands =
        {
            "devconsole", "commands", "help", "print", "clear", "reset", "bind", "unbind", "bindings"
        };

        #endregion

        #region Fields

        #region Serialised fields

        /// <summary>
        ///     Reference to the canvas group that the dev console window is a part of.
        /// </summary>
        [SerializeField]
        private CanvasGroup _canvasGroup = null;

        /// <summary>
        ///     Reference to the text that displays the deev console's version number.
        /// </summary>
        [SerializeField]
        private Text _versionText = null;

        /// <summary>
        ///     Reference to the input field for entering commands.
        /// </summary>
        [Header("Input")]
        [SerializeField]
        private InputField _inputField = null;

        /// <summary>
        ///     Reference to the text that shows command and parameter suggestions (within the input field).
        /// </summary>
        [SerializeField]
        private Text _suggestionText = null;

        /// <summary>
        ///     Reference to the game object to instantiate for new log fields (when vertices reach their limit).
        /// </summary>
        [Header("Logs")]
        [SerializeField]
        private GameObject _logFieldPrefab = null;

        /// <summary>
        ///     Reference to the transform that new log fields should be parented to.
        /// </summary>
        [SerializeField]
        private RectTransform _logContentTransform = null;

        /// <summary>
        ///     Reference to the scrollrect for the logs.
        /// </summary>
        [SerializeField]
        private ScrollRect _logScrollView = null;

        /// <summary>
        ///     Reference to the rect transform that controls the position and size of the dev console window.
        /// </summary>
        [Header("Window")]
        [SerializeField]
        private RectTransform _dynamicTransform = null;

        /// <summary>
        ///     Reference to the image component on the resize button.
        /// </summary>
        [SerializeField]
        private Image _resizeButtonImage = null;

        /// <summary>
        ///     The colour effect to apply to the hover button when resizing the dev console window.
        /// </summary>
        [SerializeField]
        private Color _resizeButtonHoverColour = default;

        #endregion

        /// <summary>
        ///     When true, the developer console is performing initialisation code (at startup).
        /// </summary>
        private bool _init = false;

        #region Input fields

        /// <summary>
        ///     When true, the input field will forcefully be focused at the beginning of the next frame.
        /// </summary>
        private bool _focusInputField = false;

        /// <summary>
        ///     The previous state of whether the input field was focused - used to invoke an event.
        /// </summary>
        private bool _oldFocusInputField = false;

        /// <summary>
        ///     Dictionary of key bindings for commands.
        /// </summary>
        private Dictionary<InputKey, string> _bindings = new Dictionary<InputKey, string>();

        /// <summary>
        ///     Whether the CTRL + Backspace input should be ignored, used to prevent multiple occurences from one input.
        /// </summary>
        private bool _ignoreCtrlBackspace = false;

        #endregion

        #region Log fields

        /// <summary>
        ///     List of the instantiated log fields.
        /// </summary>
        private readonly List<InputField> _logFields = new List<InputField>();

        /// <summary>
        ///     Log text that is going to be displayed next frame (use the thread-safe property instead).
        /// </summary>
        private string _logTextStore = "";

        /// <summary>
        ///     Used to determine the number of vertices a string requires to render.
        /// </summary>
        private readonly TextGenerator _textGenerator = new TextGenerator();

        /// <summary>
        ///     Keeps track of how many vertices are being used by the most recent instantiated log field.
        /// </summary>
        private int _vertexCount = 0;

        /// <summary>
        /// The initial font size of the text in the log field.
        /// </summary>
        private int _initLogTextSize = 0;

        /// <summary>
        ///     Used when the console is first enabled, allowing it to scroll to the bottom even though it's not at the bottom.
        /// </summary>
        private bool _pretendScrollAtBottom = false;

        /// <summary>
        ///     When true, the log scroll view will forcefully scroll to the bottom (0.0) at the beginning of next frame.
        /// </summary>
        private bool _scrollToBottomNextFrame = false;

        #endregion

        #region Window fields

        /// <summary>
        ///     True if the dev console window is being repositioned by the user.
        /// </summary>
        private bool _repositioning = false;

        /// <summary>
        ///     The initial position of the dev console window.
        /// </summary>
        private Vector2 _initPosition = default;

        /// <summary>
        ///     Used for calculating the position of the dev console window when repositioning.
        /// </summary>
        private Vector2 _repositionOffset = default;

        /// <summary>
        ///     True if the dev console window is being resized by the user.
        /// </summary>
        private bool _resizing = false;

        /// <summary>
        ///     The initial size of the dev console window.
        /// </summary>
        private Vector2 _initSize = default;

        /// <summary>
        ///     The initial resize button colour (when unpressed).
        /// </summary>
        private Color _resizeButtonColour = default;

        /// <summary>
        ///     The initial width of the log field prefab.
        /// </summary>
        private float _initLogFieldWidth = 0f;

        /// <summary>
        ///     The current width of the log field instances.
        /// </summary>
        private float _currentLogFieldWidth = 0f;

        /// <summary>
        ///     Used to compare to the current screen size for changes - which will reset the dev console window.
        /// </summary>
        private Vector2Int _screenSize = default;

        #endregion

        #region Command fields

        /// <summary>
        ///     Dictionary of all the active commands by command name.
        /// </summary>
        private readonly Dictionary<string, Command> _commands = new Dictionary<string, Command>();

        /// <summary>
        ///     Dictionary of all the parameter parser functions by type.
        /// </summary>
        private readonly Dictionary<Type, Func<string, object>> _parameterParseFuncs = new Dictionary<Type, Func<string, object>>();

        /// <summary>
        ///     List that contains the previously entered commands, which the user can cycle through.
        /// </summary>
        private readonly List<string> _commandHistory = new List<string>(CommandHistoryLength);

        /// <summary>
        ///     The name of the currently executing command.
        /// </summary>
        private string _currentCommand = string.Empty;

        /// <summary>
        ///     The name of the most previous entered command.
        /// </summary>
        private string _previousCommand = string.Empty;

        /// <summary>
        ///     Index of which command from the history is being displayed to the user (-1 means there is none).
        /// </summary>
        private int _commandHistoryIndex = -1;

        /// <summary>
        ///     Whether Unity logs should be displayed in the dev console.
        /// </summary>
        private bool _displayUnityLogs = true;

        /// <summary>
        ///     Whether Unity errors should be displayed in the dev console.
        /// </summary>
        private bool _displayUnityErrors = true;

        /// <summary>
        ///     Whether Unity exceptions should be displayed in the dev console.
        /// </summary>
        private bool _displayUnityExceptions = true;

        /// <summary>
        ///     Whether Unity warnings should be displayed in the dev console.
        /// </summary>
        private bool _displayUnityWarnings = true;

        /// <summary>
        ///     Array of command suggestions based off the current user input.
        /// </summary>
        private string[] _commandStringSuggestions = null;

        /// <summary>
        ///     Array of commands that are suggested based off the current user input.
        /// </summary>
        private Command[] _commandSuggestions = null;

        /// <summary>
        ///     Index of which command from the suggestions is being displayed to the user.
        /// </summary>
        private int _commandSuggestionIndex = 0;

        /// <summary>
        ///     List of cached enum types, used by the "enum" command to reduce the need to use reflection.
        /// </summary>
        private readonly List<Type> _cacheEnumTypes = new List<Type>(MaxCachedEnumTypes);

        /// <summary>
        ///     Evaluator used by "cs_evaluate" and "cs_run" to execute C# expressions or statements.
        /// </summary>
        private Evaluator _monoEvaluator = null;

        /// <summary>
        ///     List of using statements that are included by default with the evaluator.
        /// </summary>
        private List<string> _includedUsings = new List<string>();

        #endregion

        #region Fps fields

        private bool _isDisplayingFps;
        private float _fpsDeltaTime;
        private int _fps;
        private float _fpsMs;
        private float _fpsElapsed;
        private GUIStyle _fpsStyle;
        private Vector2 _fpsLabelSize;
        private Color _fpsTextColour;

        #endregion

        #region Stat display fields

        private bool _isDisplayingStats;
        private GUIStyle _statStyle;
        private readonly Dictionary<string, StatBase> _stats = new Dictionary<string, StatBase>();
        private HashSet<string> _hiddenStats = new HashSet<string>();
        private readonly Dictionary<string, object> _cachedStats = new Dictionary<string, object>();
        private List<string> _persistStats = new List<string>();
        private float _statUpdateTime;
        private int _statFontSize = StatDefaultFontSize;

        #endregion

        #endregion

        #region Properties

        /// <summary>
        ///     The key used to toggle the developer console window (NULL, if none).
        /// </summary>
        internal InputKey? ConsoleToggleKey { get; set; } = DefaultToggleKey;

        /// <summary>
        ///     Whether the console is enabled.
        /// </summary>
        internal bool ConsoleIsEnabled { get; private set; }

        /// <summary>
        ///     Whether the console is showing.
        /// </summary>
        internal bool ConsoleIsShowing { get; private set; }

        /// <summary>
        ///     Whether the console is showing and the input field is focused.
        /// </summary>
        internal bool ConsoleIsShowingAndFocused => ConsoleIsShowing && _inputField.isFocused;

        /// <summary>
        ///     Whether key bindings are enabled for commands.
        /// </summary>
        internal bool BindingsIsEnabled { get; set; } = true;

        /// <summary>
        ///     Average fps. -1 if fps is not being calcualted.
        /// </summary>
        internal int AverageFps => _isDisplayingFps ? _fps : -1;

        /// <summary>
        ///     Average ms per frame. -1 if fps is not being calculated.
        /// </summary>
        internal float AverageMs => _isDisplayingFps ? _fpsMs : -1f;

        /// <summary>
        ///     The text entered by the user in the input field.
        /// </summary>
        private string InputText
        {
            get => _inputField.text;
            set => _inputField.text = value;
        }

        /// <summary>
        ///     The index of the input caret in the input field.
        /// </summary>
        private int InputCaretPosition
        {
            get => _inputField.caretPosition;
            set => _inputField.caretPosition = value;
        }

        /// <summary>
        ///     Log text that is going to be displayed next frame (thread-safe).
        /// </summary>
        private string StoredLogText
        {
            get
            {
                lock(_logTextStore)
                {
                    return _logTextStore;
                }
            }
            set
            {
                lock(_logTextStore)
                {
                    _logTextStore = value;
                }
            }
        }

        /// <summary>
        ///     The font size of the text in the log.
        /// </summary>
        private int LogTextSize
        {
            get => _logFieldPrefab.GetComponent<InputField>().textComponent.fontSize;
            set
            {
                Text text = _logFieldPrefab.GetComponent<InputField>().textComponent;
                if (text.fontSize == value)
                {
                    return;
                }

                text.fontSize = value;

                if (_logFields?.Count > 0)
                {
                    _logFields.ForEach(x => x.textComponent.fontSize = value);
                    RefreshLogFieldsSize();
                }
            }
        }

        #endregion

        #region Methods

        #region Console methods

        /// <summary>
        ///     Enable the dev console.
        /// </summary>
        internal void EnableConsole()
        {
            if (!_init && ConsoleIsEnabled)
            {
                return;
            }

            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            ClearConsole();
            InputText = string.Empty;
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            ConsoleIsEnabled = true;
            enabled = true;

            DevConsole.InvokeOnConsoleEnabled();
        }

        /// <summary>
        ///     Disable the dev console.
        /// </summary>
        internal void DisableConsole()
        {
            if (!_init && !ConsoleIsEnabled)
            {
                return;
            }

            if (ConsoleIsShowing)
            {
                CloseConsole();
            }
            _dynamicTransform.anchoredPosition = _initPosition;
            _dynamicTransform.sizeDelta = _initSize;
            _commandHistory.Clear();
            _cacheEnumTypes.Clear();
            ClearConsole();
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            ConsoleIsEnabled = false;
            enabled = false;

            DevConsole.InvokeOnConsoleDisabled();
        }

        /// <summary>
        ///     Open the dev console - making it visible.
        /// </summary>
        internal void OpenConsole()
        {
            if (!_init && (!ConsoleIsEnabled || ConsoleIsShowing))
            {
                return;
            }

            // Create a new event system if none exists
            if (EventSystem.current == null)
            {
                GameObject obj = Instantiate(Resources.Load<GameObject>(InputSystemPrefabPath));
                EventSystem.current = obj.GetComponent<EventSystem>();
                obj.name = "EventSystem";
            }

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            ConsoleIsShowing = true;
            _focusInputField = true;

            DevConsole.InvokeOnConsoleOpened();
        }

        /// <summary>
        ///     Close the dev console - hiding it in the background.
        /// </summary>
        internal void CloseConsole()
        {
            if (!_init && (!ConsoleIsEnabled || !ConsoleIsShowing))
            {
                return;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            ConsoleIsShowing = false;
            _repositioning = false;
            _resizing = false;
            _focusInputField = false;
            if (_inputField.isFocused && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            DevConsole.InvokeOnConsoleClosed();
        }

        /// <summary>
        ///     Toggle the dev console visibility.
        /// </summary>
        internal void ToggleConsole()
        {
            if (ConsoleIsShowing)
            {
                CloseConsole();
                return;
            }

            OpenConsole();
        }

        /// <summary>
        ///     Clear the contents of the dev console log.
        /// </summary>
        internal void ClearConsole()
        {
            ClearLogFields();
            _vertexCount = 0;
            StoredLogText = ClearLogText;
            _pretendScrollAtBottom = true;
        }

        /// <summary>
        ///     Reset the position and size of the dev console window.
        /// </summary>
        internal void ResetConsole()
        {
            _dynamicTransform.anchoredPosition = _initPosition;
            _dynamicTransform.sizeDelta = _initSize;
            _currentLogFieldWidth = _initLogFieldWidth;
            RefreshLogFieldsSize();
        }

        /// <summary>
        ///     Submit the current user input and attempt to evaluate the command.
        /// </summary>
        internal void SubmitInput()
        {
            if (!string.IsNullOrWhiteSpace(InputText) && RunCommand(InputText))
            {
                _scrollToBottomNextFrame = true;
            }

            InputText = string.Empty;
        }

        /// <summary>
        ///     Run a command, given the raw input by the user.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        internal bool RunCommand(string rawInput)
        {
            // Get the input as an array
            // First element is the command name
            // Remainder are raw parameters
            string[] input = GetInput(rawInput);

            // Find the command
            Command command = GetCommand(input[0]);

            // Add the input to the command history, even if it isn't a valid command
            AddToCommandHistory(input[0], rawInput);

            if (command == null)
            {
                // No command found, try to evaluate as C# expression
                string expressionResult = TryEvaluateInput(rawInput);
                if (!string.IsNullOrEmpty(expressionResult))
                {
                    Log(expressionResult);
                    return false;
                }

                LogError($"Could not find the specified command: \"{input[0]}\".");
                return false;
            }

            // Determine the actual parameters now that we know the expected parameters
            input = ConvertInput(input, command.Parameters.Length);

            // Try to execute the default callback if the command has no parameters specified
            if (input.Length == 1 && command.DefaultCallback != null)
            {
                try
                {
                    command.DefaultCallback();
                }
                catch (Exception e)
                {
                    LogError($"Command default callback threw an exception.");
                    Debug.LogException(e);
                    return false;
                }
                return true;
            }

            if (command.Parameters.Length != input.Length - 1)
            {
                LogError($"Invalid number of parameters: {command.ToFormattedString()}.");
                return false;
            }

            // Iterate through the parameters and convert to the appropriate type
            object[] parameters = new object[command.Parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                string parameter = input[i + 1];

                try
                {
                    // Try to convert the parameter input into the appropriate type
                    parameters[i] = ParseParameter(parameter, command.Parameters[i].Type);
                }
                catch (Exception)
                {
                    LogError($"Invalid parameter type: \"{parameter}\". Expected {command.GetFormattedParameter(i)}.");
                    return false;
                }
            }

            // Execute the command callback with the parameters, if any
            try
            {
                command.Callback(parameters.Length == 0 ? null : parameters);
            }
            catch (Exception e)
            {
                LogError($"Command callback threw an exception.");
                Debug.LogException(e);
                return false;
            }
            return true;
        }

        /// <summary>
        ///     Add a command to the dev console.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="onlyInDevBuild"></param>
        /// <param name="isCustomCommand"></param>
        /// <returns></returns>
        internal bool AddCommand(Command command, bool onlyInDevBuild = false, bool isCustomCommand = false)
        {
            if (onlyInDevBuild && !Debug.isDebugBuild)
            {
                return false;
            }

            // Try to fix the command name, removing any whitespace and converting it to lowercase
            command.FixName();

            // Try to fix the aliases in the same manner
            command.FixAliases();

            // Try to add the command, making sure it doesn't conflict with any other commands
            if (!string.IsNullOrEmpty(command.Name) && !_commands.ContainsKey(command.Name) && !_commands.Values.Select(c => c.Aliases).Any(a => command.HasAlias(a)))
            {
                _commands.Add(command.Name, command);

                if (isCustomCommand)
                {
                    command.SetAsCustomCommand();
                }

                return true;
            }
            return false;
        }

        /// <summary>
        ///     Remove a command from the dev console.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal bool RemoveCommand(string name)
        {
            Command command = GetCommand(name);

            if (command == null)
            {
                return true;
            }

            if (_permanentCommands.Contains(command.Name))
            {
                return false;
            }

            return _commands.Remove(command.Name);
        }

        /// <summary>
        ///     Get a command instance by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal Command GetCommand(string name)
        {
            return _commands.TryGetValue(name.ToLower(), out Command command) ? command : _commands.Values.FirstOrDefault(c => c.HasAlias(name));
        }

        /// <summary>
        ///     Get a command instance by name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        internal bool GetCommand(string name, out Command command)
        {
            return _commands.TryGetValue(name.ToLower(), out command) || ((command = _commands.Values.FirstOrDefault(c => c.HasAlias(name))) != null);
        }

        /// <summary>
        ///     Add a parameter type parser function.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="parseFunc"></param>
        /// <returns></returns>
        internal bool AddParameterType(Type type, Func<string, object> parseFunc)
        {
            // Try to add the parameter type, if one doesn't already exist for this type
            if (!_parameterParseFuncs.ContainsKey(type))
            {
                _parameterParseFuncs.Add(type, parseFunc);
                return true;
            }
            return false;
        }

        #endregion

        #region Log methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Log(object message)
        {
            StoredLogText += $"\n{message}";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Log(object message, string htmlColour)
        {
            Log($"<color={htmlColour}>{message}</color>");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogVariable(string variableName, object value, string suffix = "")
        {
            Log($"{variableName}: {value}{suffix}.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogException(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            Log($"({DateTime.Now:HH:mm:ss}) <color={ErrorColour}><b>Exception:</b> </color>{exception.Message}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogError(object message)
        {
            Log(message, ErrorColour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogWarning(object message)
        {
            Log(message, WarningColour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogSuccess(object message)
        {
            Log(message, SuccessColour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogSeperator(object message = null)
        {
            if (message == null)
            {
                Log("-");
                return;
            }

            Log($"- <b>{message}</b> -");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogCollection<T>(in IReadOnlyCollection<T> collection, Func<T, string> toString = null, string prefix = "", string suffix = "")
        {
            if (collection == null || collection.Count == 0)
            {
                return;
            }

            Log(string.Join("\n",
                collection.Select(x => $"{prefix}{toString?.Invoke(x) ?? x.ToString()}{suffix}"))
                );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogCommand()
        {
            LogCommand(_currentCommand);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogCommand(string name)
        {
            if (!GetCommand(name, out Command command))
            {
                return;
            }

            Log($">> {command.ToFormattedString()}.");
        }

        #endregion

        #region Unity events

        /// <summary>
        ///     Invoked when the input field text is changed.
        /// </summary>
        /// <param name="_"></param>
        internal void OnInputValueChanged(string _)
        {
            // Check if CTRL + Backspace was pressed, and remove a word before the caret position
            if (!_ignoreCtrlBackspace && GetKey(LeftControlKey) && GetKeyDown(BackspaceKey))
            {
                string tilCaret = InputText.Substring(0, InputCaretPosition);
                string afterCaret = InputText.Substring(InputCaretPosition, InputText.Length - InputCaretPosition);
                string[] split = tilCaret.Split(' ');
                int length = 0;
                for (int i = 0; i < split.Length - 1; ++i)
                {
                    length += split[i].Length + 1;
                }

                _ignoreCtrlBackspace = true;
                InputText = InputText.Substring(0, length) + afterCaret;
                _ignoreCtrlBackspace = false;
                InputCaretPosition = length;
            }

            RefreshCommandSuggestions();
            RefreshCommandParameterSuggestions();
        }

        /// <summary>
        ///     Invoked when a character is going to be added to the input field.
        /// </summary>
        /// <param name="_"></param>
        /// <param name="__"></param>
        /// <param name="addedChar">Character that was inputted.</param>
        /// <returns>The character to add to the input text.</returns>
        internal char OnValidateInput(string _, int __, char addedChar)
        {
            const char EmptyChar = '\0';

            // Ignore inputs if the dev console window is closed
            if (!ConsoleIsShowing)
            {
                return EmptyChar;
            }

            // If a new line character is entered, submit the command
            if (addedChar == '\n')
            {
                addedChar = EmptyChar;
                SubmitInput();
            }

            // If a TAB character is entered, autocomplete the suggested command
            else if (addedChar == '\t')
            {
                addedChar = EmptyChar;
                AutoComplete();
            }

            // If the toggle key is pressed and the input field text is empty - ignore the character
            else if (InputText.Length == 0 && ConsoleToggleKey.HasValue && GetKeyDown(ConsoleToggleKey.Value))
            {
                addedChar = EmptyChar;
            }

            return addedChar;
        }

        /// <summary>
        ///     Invoked when the mouse is pressed down on the reposition button.
        /// </summary>
        /// <param name="eventData"></param>
        internal void OnRepositionButtonPointerDown(BaseEventData eventData)
        {
            _repositioning = true;
            _repositionOffset = ((PointerEventData)eventData).position - (Vector2)_dynamicTransform.position;
        }

        /// <summary>
        ///     Invoked when the mouse is released after pressing the reposition button.
        /// </summary>
        /// <param name="_"></param>
        internal void OnRepositionButtonPointerUp(BaseEventData _)
        {
            _repositioning = false;
        }

        /// <summary>
        ///     Invoked when the mouse button is pressed down on the resize button.
        /// </summary>
        /// <param name="_"></param>
        internal void OnResizeButtonPointerDown(BaseEventData _)
        {
            _resizing = true;
        }

        /// <summary>
        ///     Invoked when the mouse is released after pressing the resize button.
        /// </summary>
        /// <param name="_"></param>
        internal void OnResizeButtonPointerUp(BaseEventData _)
        {
            _resizing = false;
            _resizeButtonImage.color = _resizeButtonColour;
            RefreshLogFieldsSize();
        }

        /// <summary>
        ///     Invoked when the mouse cursor enters the resize button rect.
        /// </summary>
        /// <param name="_"></param>
        internal void OnResizeButtonPointerEnter(BaseEventData _)
        {
            _resizeButtonImage.color = _resizeButtonColour * _resizeButtonHoverColour;
        }

        /// <summary>
        ///     Invoked when the mouse cursor exits the resize button rect.
        /// </summary>
        /// <param name="_"></param>
        internal void OnResizeButtonPointerExit(BaseEventData _)
        {
            if (!_resizing)
            {
                _resizeButtonImage.color = _resizeButtonColour;
            }
        }

        /// <summary>
        ///     Invoked when the author button is pressed.
        /// </summary>
        internal void OnAuthorButtonPressed()
        {
#if !UNITY_ANDROID && !UNITY_IOS
            Application.OpenURL(@"https://www.davidfdev.com");
#endif
        }

        /// <summary>
        ///     Invoked when the increase text size button is pressed.
        /// </summary>
        internal void OnIncreaseTextSizeButtonPressed()
        {
            LogTextSize = Math.Min(MaxLogTextSize, LogTextSize + 4);
        }

        /// <summary>
        ///     Invoked when the decrease text size button is pressed.
        /// </summary>
        internal void OnDecreaseTextSizeButtonPressed()
        {
            LogTextSize = Math.Max(MinLogTextSize, LogTextSize - 4);
        }

        /// <summary>
        ///     Invoked when a Unity log message is received.
        /// </summary>
        /// <param name="logString"></param>
        /// <param name="_"></param>
        /// <param name="type"></param>
        private void OnLogMessageReceived(string logString, string _, LogType type)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            switch (type)
            {
                case LogType.Log:
                    if (!_displayUnityLogs)
                    {
                        return;
                    }
                    Log($"({time}) <b>Log:</b> {logString}");
                    break;
                case LogType.Error:
                    if (!_displayUnityErrors)
                    {
                        return;
                    }
                    Log($"({time}) <color={ErrorColour}><b>Error:</b> </color>{logString}");
                    break;
                case LogType.Exception:
                    if (!_displayUnityExceptions)
                    {
                        return;
                    }
                    Log($"({time}) <color={ErrorColour}><b>Exception:</b> </color>{logString}");
                    break;
                case LogType.Warning:
                    if (!_displayUnityWarnings)
                    {
                        return;
                    }
                    Log($"({time}) <color={WarningColour}><b>Warning:</b> </color>{logString}");
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Unity methods

        private void Awake()
        {
            _init = true;

            // Set up the game object
            gameObject.name = "DevConsoleInstance";
            DontDestroyOnLoad(gameObject);

#if HIDE_FROM_EDITOR
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
#endif

            _versionText.text = $"v{_version}";
            _initPosition = _dynamicTransform.anchoredPosition;
            _initSize = _dynamicTransform.sizeDelta;
            _initLogFieldWidth = _logFieldPrefab.GetComponent<RectTransform>().sizeDelta.x;
            _initLogTextSize = _logFieldPrefab.GetComponent<InputField>().textComponent.fontSize;
            _currentLogFieldWidth = _initLogFieldWidth;
            _resizeButtonColour = _resizeButtonImage.color;
            _logFieldPrefab.SetActive(false);
            _inputField.onValueChanged.AddListener(x => OnInputValueChanged(x));
            _inputField.onValidateInput += OnValidateInput;

            LoadPreferences();
            InitBuiltInCommands();
            InitBuiltInParsers();
            InitAttributes();
            InitMonoEvaluator();
            _persistStats.Clear();

            // Enable the console by default if in editor or a development build
            if (Debug.isDebugBuild)
            {
                EnableConsole();
            }
            else
            {
                DisableConsole();
            }

            ClearConsole();
            CloseConsole();

            if (_monoEvaluator == null)
            {
                LogWarning($"Some features may not be available: {MonoNotSupportedText}");
            }

            _init = false;
        }

        private void Update()
        {
            if (!ConsoleIsEnabled || !ConsoleIsShowing)
            {
                return;
            }

            // Scroll the view to the bottom
            if (_scrollToBottomNextFrame)
            {
                _logScrollView.verticalNormalizedPosition = 0.0f;
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_logScrollView.transform);
                if (_pretendScrollAtBottom && (_logScrollView.verticalNormalizedPosition < 0f || Mathf.Approximately(_logScrollView.verticalNormalizedPosition, 0f)))
                {
                    _pretendScrollAtBottom = false;
                }
                _scrollToBottomNextFrame = false;
            }

            // Check if the resolution has changed and the window should be rebuilt / reset
            if (_screenSize.x != Screen.width || _screenSize.y != Screen.height)
            {
                _screenSize = new Vector2Int(Screen.width, Screen.height);
                ResetConsole();
            }

            // Force the input field to be focused by the event system
            if (_focusInputField)
            {
                EventSystem.current.SetSelectedGameObject(_inputField.gameObject, null);
                _focusInputField = false;
            }

            // Check if the input field focus changed and invoke the event
            if (_inputField.isFocused != _oldFocusInputField)
            {
                if (_inputField.isFocused)
                {
                    DevConsole.InvokeOnConsoleFocused();
                }
                else
                {
                    DevConsole.InvokeOnConsoleFocusLost();
                }
                _oldFocusInputField = _inputField.isFocused;
            }

            // Move the developer console using the mouse position
            if (_repositioning)
            {
                Vector2 mousePosition = GetMousePosition();
                _dynamicTransform.position = new Vector3(
                    mousePosition.x - _repositionOffset.x,
                    mousePosition.y - _repositionOffset.y,
                    _dynamicTransform.position.z);
            }

            // Resize the developer console using the mouse position
            if (_resizing)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_dynamicTransform, GetMousePosition(), null, out Vector2 localPoint);
                localPoint.x = Mathf.Clamp(Mathf.Abs(localPoint.x), MinConsoleWidth, MaxConsoleWidth);
                localPoint.y = Mathf.Clamp(Mathf.Abs(localPoint.y), MinConsoleHeight, MaxConsoleHeight);
                _dynamicTransform.sizeDelta = localPoint;

                // Resize the log field too, because Unity refuses to do it automatically
                _currentLogFieldWidth = _initLogFieldWidth * (_dynamicTransform.sizeDelta.x / _initSize.x);
            }
        }

        private void LateUpdate()
        {
            if (!ConsoleIsEnabled)
            {
                return;
            }

            // Update fps display
            if (_isDisplayingFps)
            {
                _fpsDeltaTime += (Time.unscaledDeltaTime - _fpsDeltaTime) * 0.1f;
                _fpsElapsed += Time.deltaTime;
                if (_fpsElapsed > 1.0f / FpsUpdateRate)
                {
                    // Calculate fps values
                    _fpsMs = _fpsDeltaTime * 1000f;
                    _fps = Mathf.RoundToInt(1.0f / _fpsDeltaTime);
                    _fpsElapsed -= 1.0f / FpsUpdateRate;

                    // Determine colour
                    _fpsTextColour = Color.white;
                    if (Application.targetFrameRate == -1 && _fps >= 60 || Application.targetFrameRate != -1 && _fps >= Application.targetFrameRate)
                    {
                        _fpsTextColour = Color.green;
                    }
                    else if (_fps < 10)
                    {
                        _fpsTextColour = Color.red;
                    }
                    else if (_fps < 30 && (Application.targetFrameRate > 30 || Application.targetFrameRate == -1))
                    {
                        _fpsTextColour = Color.yellow;
                    }
                }
            }

            // Check bindings (as long as the input field or any other object isn't focused!)
            if (BindingsIsEnabled && !_inputField.isFocused && (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null))
            {
                try
                {
                    foreach (InputKey key in _bindings.Keys)
                    {
                        if (GetKeyDown(key))
                        {
                            RunCommand(_bindings[key]);
                        }
                    }
                }
                catch (Exception e)
                {
                    LogError($"Checking bindings failed with an exception: {e.Message}");
                }
            }

            // Process the stored logs, displaying them to the console
            if (StoredLogText != string.Empty)
            {
                // Check if should scroll to the bottom
                const float scrollPerc = 0.001f;
                if (_pretendScrollAtBottom || _logScrollView.verticalNormalizedPosition < scrollPerc || Mathf.Approximately(_logScrollView.verticalNormalizedPosition, scrollPerc))
                {
                    _scrollToBottomNextFrame = true;
                }

                string logText = string.Copy(StoredLogText);
                StoredLogText = string.Empty;
                ProcessLogText(logText);
                RebuildLayout();
            }

            // Check if the developer console toggle key was pressed
            if (ConsoleToggleKey.HasValue && (!ConsoleIsShowing || (!_inputField.isFocused || InputText.Length <= 1)) && GetKeyDown(ConsoleToggleKey.Value))
            {
                ToggleConsole();
                return;
            }

            if (!ConsoleIsShowing)
            {
                return;
            }

            if (_inputField.isFocused)
            {
                // Allow cycling through command suggestions using the UP and DOWN arrows
                if (_commandStringSuggestions != null && _commandStringSuggestions.Length > 0)
                {
                    if (GetKeyDown(UpArrowKey))
                    {
                        CycleCommandSuggestions(1);
                    }
                    else if (GetKeyDown(DownArrowKey))
                    {
                        CycleCommandSuggestions(-1);
                    }
                }

                // Allow cycling through command history using the UP and DOWN arrows
                else
                {
                    // Reset the command history index if the input text is blank
                    if (string.IsNullOrEmpty(InputText) && _commandHistoryIndex != -1)
                    {
                        _commandHistoryIndex = -1;
                    }

                    if (GetKeyDown(UpArrowKey))
                    {
                        CycleCommandHistory(1);
                    }
                    else if (GetKeyDown(DownArrowKey))
                    {
                        CycleCommandHistory(-1);
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!ConsoleIsEnabled)
            {
                return;
            }

            if (_isDisplayingFps)
            {
                if (_fpsStyle == null || _statFontSize != _fpsStyle.fontSize)
                {
                    // Create the style
                    _fpsStyle = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = _statFontSize,
                        normal = { textColor = Color.white, background = Texture2D.whiteTexture }
                    };

                    _fpsLabelSize = _fpsStyle.CalcSize(new GUIContent("0.00 ms (000 fps)"));
                }

                Color oldBackgroundColour = GUI.backgroundColor;
                Color oldContentColour = GUI.contentColor;

                // Set colours
                GUI.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
                GUI.contentColor = _fpsTextColour;

                // Create label
                GUI.Box(
                    new Rect(10f, 10f, _fpsLabelSize.x + 10f, _fpsLabelSize.y + 10f),
                    $"{_fpsMs:0.00} ms ({_fps:0.} fps)",
                    _fpsStyle);

                GUI.backgroundColor = oldBackgroundColour;
                GUI.contentColor = oldContentColour;
            }

            if (_isDisplayingStats && _stats.Any())
            {
                if (_statStyle == null || _statFontSize != _statStyle.fontSize)
                {
                    // Create the style
                    _statStyle = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = _statFontSize,
                        normal = { textColor = Color.white, background = Texture2D.whiteTexture }
                    };
                }

                // Initialise
                Color oldBackgroundColour = GUI.backgroundColor;
                Color oldContentColour = GUI.contentColor;
                GUI.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
                const float padding = 5f;
                float startY = (padding * 2f) + (_isDisplayingFps ? (_fpsLabelSize.y + 10f) : 0f);
                int num = 0;

                // Check if should update the cached stats
                bool updateCache = Time.time > _statUpdateTime;
                if (updateCache)
                {
                    _statUpdateTime = Time.time + StatUpdateRate;
                }

                // Create a label for each stat
                foreach (KeyValuePair<string, StatBase> stat in _stats)
                {
                    // Ignore if hidden
                    if (_hiddenStats.Contains(stat.Key))
                    {
                        continue;
                    }

                    // Determine stat result
                    object result;
                    if (stat.Value is EvaluatedStat && !updateCache && _cachedStats.ContainsKey(stat.Key))
                    {
                        result = _cachedStats[stat.Key];
                    }
                    else
                    {
                        try
                        {
                            result = stat.Value.GetResult(_monoEvaluator);
                        }
                        catch (Exception)
                        {
                            result = "ERROR";
                        }

                        // Cache the stat if it should be cached
                        if (stat.Value is EvaluatedStat)
                        {
                            _cachedStats[stat.Key] = result;
                        }
                    }

                    // Set content
                    string content = $"{stat.Key}: {result ?? "NULL"}";
                    GUI.contentColor = result == null ? Color.yellow : ((result.Equals("ERROR") || result.Equals(MonoNotSupportedText)) ? Color.red : Color.white);

                    // Determine label size
                    Vector2 size = _statStyle.CalcSize(new GUIContent(content));

                    // Create the label
                    GUI.Box(
                        new Rect(10, startY + ((num * (size.y + 10f + padding)) + padding), size.x + 10f, size.y + 10f),
                        content,
                        _statStyle);

                    ++num;
                }

                // Reset colours
                GUI.backgroundColor = oldBackgroundColour;
                GUI.contentColor = oldContentColour;
            }
        }

        private void OnDestroy()
        {
            SavePreferences();
        }

        #endregion

        #region Init methods

        /// <summary>
        ///     Add all the built-in commands.
        /// </summary>
        private void InitBuiltInCommands()
        {
            #region Console commands

            AddCommand(Command.Create(
                "devconsole",
                "",
                "Display instructions on how to use the developer console",
                () =>
                {
                    LogSeperator($"Developer console (v{_version})");
                    Log("Use <b>commands</b> to display a list of available commands.");
                    Log($"Use {GetCommand("help").ToFormattedString()} to display information about a specific command.");
                    Log("Use UP / DOWN to cycle through command history or suggested commands.");
                    Log("Use TAB to autocomplete a suggested command.\n");
#if UNITY_EDITOR
                    Log("Please note that the developer console is disabled by default in release builds.");
                    Log("Enable it manually via script: <b>DevConsole.EnableConsole()</b>.\n");
#endif
                    Log("Created by @DavidF_Dev.");
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create<string>(
                "print",
                "echo",
                "Display a message in the developer console",
                Parameter.Create("message", "Message to display"),
                s => Log(s)
            ));

            AddCommand(Command.Create(
                "clear",
                "",
                "Clear the developer console",
                () => ClearConsole()
            ));

            AddCommand(Command.Create(
                "reset",
                "",
                "Reset the position and size of the developer console",
                () => ResetConsole()
            ));

            AddCommand(Command.Create(
                "closeconsole",
                "hideconsole",
                "Close the developer console window",
                () => CloseConsole()
            ));

            AddCommand(Command.Create<string>(
                "help",
                "info",
                "Display information about a specified command",
                Parameter.Create(
                    "commandName",
                    "Name of the command to get information about"),
                s =>
                {
                    Command command = GetCommand(s);

                    if (command == null)
                    {
                        LogError($"Unknown command name specified: \"{s}\". Use <b>list</b> for a list of all commands.");
                        return;
                    }

                    LogSeperator(command.Name);

                    if (!string.IsNullOrEmpty(command.HelpText))
                    {
                        Log($"{command.HelpText}.");
                    }

                    if (command.Aliases?.Length > 0 && command.Aliases.Any(a => !string.IsNullOrEmpty(a)))
                    {
                        string[] formattedAliases = command.Aliases.Select(alias => $"<i>{alias}</i>").ToArray();
                        Log($"Aliases: {string.Join(", ", formattedAliases)}.");
                    }

                    if (command.Parameters.Length > 0)
                    {
                        Log($"Syntax: {command.ToFormattedString()}.");
                    }

                    foreach (Parameter parameter in command.Parameters)
                    {
                        if (!string.IsNullOrEmpty(parameter.HelpText))
                        {
                            Log($" <b>{parameter.Name}</b>: {parameter.HelpText}.");
                        }
                    }

                    LogSeperator();
                },
                () =>
                {
                    if (string.IsNullOrWhiteSpace(_previousCommand) || _previousCommand.ToLower().Equals("help"))
                    {
                        RunCommand($"help help");
                        return;
                    }

                    RunCommand($"help {_previousCommand}");
                }
            ));

            AddCommand(Command.Create<string>(
                "enum",
                "",
                "Display information about a specified enum",
                Parameter.Create("enumName", "Name of the enum to get information about (case-sensitive)"),
                s =>
                {
                    // Check if the enum type was cached
                    Type enumType = _cacheEnumTypes.FirstOrDefault(t => t.Name.Equals(s));

#if INPUT_SYSTEM_INSTALLED
                    // Special case for Key
                    if (s.Equals("Key"))
                    {
                        enumType = typeof(Key);
                    }
#endif

                    if (enumType == null)
                    {
                        List<Type> options = new List<Type>();

                        // Search all loaded assemblies for the enum
                        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            enumType = assembly.GetTypes()
                                .SelectMany(t => t.GetMembers())
                                .Union(assembly.GetTypes())
                                .FirstOrDefault(t => t.ReflectedType != null && t.ReflectedType.IsEnum && (t.ReflectedType.Name.Equals(s) || s.Equals($"{t.ReflectedType.Namespace}.{t.ReflectedType.Name}")))
                                ?.ReflectedType;

                            if (enumType != null)
                            {
                                if (s.Equals($"{enumType.Namespace}.{enumType.Name}"))
                                {
                                    options = new List<Type>() { enumType };
                                    break;
                                }

                                options.Add(enumType);
                            }
                        }

                        if (options.Count > 1)
                        {
                            LogError($"Multiple results found: {string.Join(", ", options.Select(x => $"{x.Namespace}.{x.Name}"))}.");
                            return;
                        }

                        else if (options.Count == 1)
                        {
                            // Select the first type
                            enumType = options.FirstOrDefault();

                            // Cache the type
                            _cacheEnumTypes.Add(enumType);
                            if (_cacheEnumTypes.Count > MaxCachedEnumTypes)
                            {
                                _cacheEnumTypes.RemoveAt(0);
                            }
                        }

                        else
                        {
                            LogError($"Could not find enum type with the specified name: \"{s}\"");
                            return;
                        }
                    }

                    LogSeperator($"{enumType.Namespace}.{enumType.Name} ({enumType.GetEnumUnderlyingType().Name}){(enumType.GetCustomAttribute(typeof(FlagsAttribute)) == null ? "" : " [Flags]")}");

                    FieldInfo[] values = enumType.GetFields();
                    string formattedValues = string.Empty;
                    bool first = true;
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (values[i].Name.Equals("value__"))
                        {
                            continue;
                        }

                        formattedValues += $"{(first ? "" : "\n")}{values[i].Name} = {values[i].GetRawConstantValue()}";
                        first = false;
                    }
                    Log(formattedValues);

                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "commands",
                "",
                "Display a sorted list of all available commands",
                () =>
                {
                    LogSeperator("Commands");
                    Log(string.Join(", ", _commands.Keys.OrderBy(s => s)));
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "customcommands",
                "",
                "Display a sorted list of all available custom commands",
                () =>
                {
                    IList<string> customCommands = _commands.Keys.Where(s => _commands[s].IsCustomCommand).ToList();

                    if (customCommands?.Count == 0)
                    {
                        Log("There are no custom commands defined.");
                        return;
                    }

                    LogSeperator("Custom commands");
                    Log(string.Join(", ", customCommands.OrderBy(s => s)));
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "consoleversion",
                "",
                "Display the developer console version",
                () => Log($"Developer console version: {_version}.")
            ));

            AddCommand(Command.Create<InputKey, string>(
                "bind",
                "addbind",
                "Add a key binding for a command",
                Parameter.Create("Key", "Key to bind the command to"),
                Parameter.Create("Command", "Command to execute when the key bind is pressed"),
                (key, command) =>
                {
                    if (_bindings.ContainsKey(key))
                    {
                        LogError($"A key binding already exists for <i>{key}</i>. Use {GetCommand("unbind").ToFormattedString()} to remove the key binding.");
                        return;
                    }

                    _bindings[key] = command;
                    LogSuccess($"Successfully added a key binding for <i>{key}</i>.");
                }
            ));

            AddCommand(Command.Create<InputKey>(
                "unbind",
                "removebind",
                "Remove a key binding",
                Parameter.Create("Key", "Key binding to remove"),
                key =>
                {
                    if (!_bindings.ContainsKey(key))
                    {
                        LogError($"A key binding doesn't exist for <i>{key}</i>.");
                        return;
                    }

                    _bindings.Remove(key);
                    LogSuccess($"Successfully removed a key binding for <i>{key}</i>.");
                }
            ));

            AddCommand(Command.Create(
                "binds",
                "",
                "List all the key bindings",
                () =>
                {
                    if (_bindings.Count == 0)
                    {
                        Log($"There are no key bindings. Use {GetCommand("bind").GetFormattedName()} to add a key binding.");
                        return;
                    }

                    LogSeperator($"Key bindings ({_bindings.Count})");
                    Log(string.Join("\n", _bindings.Keys.Select(x => $"<i>{x}</i>: \"{_bindings[x]}\"")));
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create<int>(
                "log_size",
                "",
                "Query or set the font size used in the developer console log",
                Parameter.Create("fontSize", ""),
                fontSize =>
                {
                    if (fontSize < MinLogTextSize || fontSize > MaxLogTextSize)
                    {
                        LogError($"Invalid font size specified: {fontSize}. Must be between {MinLogTextSize} and {MaxLogTextSize}.");
                        return;
                    }

                    int oldTextSize = LogTextSize;
                    LogTextSize = fontSize;
                    LogSuccess($"Successfully changed the log font size to {fontSize} (was {oldTextSize}).");
                },
                () => LogVariable("Log font size", _logFields.First().textComponent.fontSize, $" (Default: {_initLogTextSize})")
                ));

            #endregion

            #region Player commands

            AddCommand(Command.Create(
                "quit",
                "exit",
                "Exit the player application",
                () =>
                {
                    if (Application.isEditor)
                    {
                        LogError("Cannot quit the player application when running in the Editor.");
                        return;
                    }

                    Application.Quit();
                }
            ));

            AddCommand(Command.Create(
                "appversion",
                "",
                "Display the application version",
                () => LogVariable("App version", Application.version)
            ));

            AddCommand(Command.Create(
                "unityversion",
                "",
                "Display the engine version",
                () => LogVariable("Engine version", Application.unityVersion)
            ));

            AddCommand(Command.Create(
                "unityinput",
                "",
                "Display the input system being used by the developer console",
                () =>
                {
#if USE_NEW_INPUT_SYSTEM
                    Log("The new input system package is currently being used.");
#else
                    Log("The legacy input system is currently being used.");
#endif
                }
            ));

            AddCommand(Command.Create(
                "path",
                "",
                "Display the path to the application executable",
                () => LogVariable("Application path", AppDomain.CurrentDomain.BaseDirectory)
            ));

            #endregion

            #region Screen commands

            AddCommand(Command.Create<bool?>(
                "fullscreen",
                "",
                "Query or set whether the window is full screen",
                Parameter.Create("enabled", "Whether the window is full screen (use \"NULL\" to toggle)"),
                b =>
                {
                    if (!b.HasValue)
                    {
                        b = !Screen.fullScreen;
                    }

                    Screen.fullScreen = b.Value;
                    LogSuccess($"{(b.Value ? "Enabled" : "Disabled")} fullscreen mode.");
                },
                () => LogVariable("Full screen", Screen.fullScreen)
            ));

            AddCommand(Command.Create<FullScreenMode>(
                "fullscreen_mode",
                "",
                $"Query or set the full screen mode",
                Parameter.Create("mode", ""),
                m =>
                {
                    Screen.fullScreenMode = m;
                    LogSuccess($"Full screen mode set to {m}.");
                },
                () => LogVariable("Full screen mode", Screen.fullScreenMode)
            ));

            AddCommand(Command.Create<int>(
                "vsync",
                "",
                "Query or set whether VSync is enabled",
                Parameter.Create("vSyncCount", "The number of VSyncs that should pass between each frame (0, 1, 2, 3, or 4)"),
                i =>
                {
                    if (i < 0 || i > 4)
                    {
                        LogError($"Provided VSyncCount is not an accepted value: \"{i}\".");
                        return;
                    }

                    QualitySettings.vSyncCount = i;
                    LogSuccess($"VSyncCount set to {i}.");
                },
                () => LogVariable("VSync count", QualitySettings.vSyncCount)
            ));

            AddCommand(Command.Create(
                "monitor_size",
                "monitor_resolution",
                "Display the current monitor resolution",
                () => LogVariable("Monitor size", Screen.currentResolution)
            ));

            AddCommand(Command.Create(
                "window_size",
                "window_resolution",
                "Display the current window resolution",
                () => LogVariable("Window size", $"{Screen.width} x {Screen.height}")
            ));

            AddCommand(Command.Create<int>(
                "targetfps",
                "",
                "Query or set the target frame rate",
                Parameter.Create("targetFrameRate", "Frame rate the application will try to render at."),
                i =>
                {
                    Application.targetFrameRate = i;
                    LogSuccess($"Target frame rate set to {i}.");
                },
                () => LogVariable("Target frame rate", Application.targetFrameRate)
            ));

            #endregion

            #region Camera commands

            AddCommand(Command.Create<bool?>(
                "cam_ortho",
                "",
                "Query or set whether the main camera is orthographic",
                Parameter.Create("enabled", "Whether the main camera is orthographic (use \"NULL\" to toggle)"),
                b =>
                {
                    if (Camera.main == null)
                    {
                        LogError("Could not find the main camera.");
                        return;
                    }

                    if (!b.HasValue)
                    {
                        b = !Camera.main.orthographic;
                    }

                    Camera.main.orthographic = b.Value;
                    LogSuccess($"{(b.Value ? "Enabled" : "Disabled")} orthographic mode on the main camera.");
                },
                () =>
                {
                    if (Camera.main == null)
                    {
                        LogError("Could not find the main camera.");
                        return;
                    }

                    LogVariable("Orthographic", Camera.main.orthographic);
                }
            ));

            AddCommand(Command.Create<int>(
                "cam_fov",
                "",
                "Query or set the main camera field of view",
                Parameter.Create("fieldOfView", "Field of view"),
                f =>
                {
                    if (Camera.main == null)
                    {
                        LogError("Could not find the main camera.");
                        return;
                    }

                    Camera.main.fieldOfView = f;
                    LogSuccess($"Main camera's field of view set to {f}.");
                },
                () =>
                {
                    if (Camera.main == null)
                    {
                        LogError("Could not find the main camera.");
                        return;
                    }

                    LogVariable("Field of view", Camera.main.fieldOfView);
                }
            ));

            #endregion

            #region Scene commands

            AddCommand(Command.Create<int>(
                "scene_load",
                "",
                "Load the scene at the specified build index",
                Parameter.Create(
                    "buildIndex",
                    "Build index of the scene to load, specified in the Unity build settings"
                    ),
                i =>
                {
                    if (i >= SceneManager.sceneCountInBuildSettings)
                    {
                        LogError($"Invalid build index specified: \"{i}\". Check the Unity build settings.");
                        return;
                    }

                    SceneManager.LoadScene(i);
                    LogSuccess($"Loaded scene at build index {i}.");
                }
            ), true);

            AddCommand(Command.Create<int>(
                "scene_info",
                "",
                "Display information about the current scene",
                Parameter.Create("sceneIndex", "Index of the scene in the currently loaded scenes"),
                i =>
                {
                    if (i >= SceneManager.sceneCount)
                    {
                        LogError($"Could not find active scene at index: {i}.");
                        return;
                    }

                    Scene scene = SceneManager.GetSceneAt(i);
                    LogSeperator(scene.name);
                    Log($"Scene index: {i}.");
                    Log($"Build index: {scene.buildIndex}.");
                    Log($"Path: {scene.path}.");
                    LogSeperator();
                },
                () =>
                {
                    if (SceneManager.sceneCount == 0)
                    {
                        Log("Could not find any active scenes.");
                        return;
                    }

                    LogSeperator("Active scenes");
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        Scene scene = SceneManager.GetSceneAt(i);
                        Log($" {i}) {scene.name}, build index: {scene.buildIndex}.");
                    }
                    LogCommand();
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create<string>(
                "obj_info",
                "",
                "Display information about a game object in the scene",
                Parameter.Create("name", "Name of the game object"),
                s =>
                {
                    GameObject obj = GameObject.Find(s);

                    if (obj == null)
                    {
                        LogError($"Could not find game object: \"{s}\".");
                        return;
                    }

                    LogSeperator($"{obj.name} ({(obj.activeInHierarchy ? "enabled" : " disabled")})");
                    if (obj.TryGetComponent(out RectTransform rect))
                    {
                        Log("RectTransform:");
                        LogVariable(" Anchored position", rect.anchoredPosition);
                        LogVariable(" Size", rect.sizeDelta);
                        LogVariable(" Pivot", rect.pivot);
                    }
                    else
                    {
                        Log("Transform:");
                        LogVariable(" Position", obj.transform.position);
                        LogVariable(" Rotation", obj.transform.rotation);
                        LogVariable(" Scale", obj.transform.localScale);
                    }
                    LogVariable("Tag", obj.tag);
                    LogVariable("Physics layer", LayerMask.LayerToName(obj.layer));

                    Component[] components = obj.GetComponents(typeof(Component));
                    if (components.Length > 1)
                    {
                        Log("Components:");
                        for (int i = 1; i < components.Length; i++)
                        {
                            if (components[i] is MonoBehaviour mono)
                            {
                                Log($" {i}: {mono.GetType().Name} ({(mono.enabled ? "enabled" : "disabled")}).");
                            }
                            else
                            {
                                Log($" {i}: {components[i].GetType().Name}.");
                            }
                        }
                    }

                    if (obj.transform.childCount > 0)
                    {
                        Log("Children:");
                        Transform child;
                        for (int i = 0; i < obj.transform.childCount; i++)
                        {
                            child = obj.transform.GetChild(i);
                            Log($" {i}: {child.gameObject.name} ({(child.gameObject.activeInHierarchy ? "enabled" : "disabled")}).");
                        }
                    }

                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "obj_list",
                "",
                "Display a hierarchical list of all game objects in the scene",
                () =>
                {
                    GameObject[] root = SceneManager.GetActiveScene().GetRootGameObjects();
                    Transform t;
                    string logResult = string.Empty;
                    const int space = 2;

                    string getTabbed(int tabAmount)
                    {
                        string tabbed = string.Empty;
                        for (int i = 0; i < tabAmount; i++)
                        {
                            tabbed += (i % space == 0) ? '|' : ' ';
                        }
                        return tabbed;
                    }

                    void logChildren(GameObject obj, int tabAmount)
                    {
                        string tabbed = getTabbed(tabAmount);
                        for (int i = 0; i < obj.transform.childCount; i++)
                        {
                            t = obj.transform.GetChild(i);
                            logResult += $"{tabbed}{t.gameObject.name}.\n";
                            logChildren(t.gameObject, tabAmount + 2);
                        }
                    }

                    foreach (GameObject rootObj in root)
                    {
                        logResult += $"{rootObj.gameObject.name}.\n";
                        logChildren(rootObj, space);
                    }

                    LogSeperator($"Hierarchy ({SceneManager.GetActiveScene().name})");
                    Log(logResult.TrimEnd('\n'));
                    LogSeperator();
                }
            ));

            #endregion

            #region Log commands

            AddCommand(Command.Create<bool?>(
                "log_logs",
                "",
                "Query, enable or disable displaying Unity logs in the developer console",
                Parameter.Create("enabled", "Whether Unity logs should be displayed in the developer console (use \"NULL\" to toggle)"),
                b =>
                {
                    if (!b.HasValue)
                    {
                        b = !_displayUnityLogs;
                    }

                    _displayUnityLogs = b.Value;
                    LogSuccess($"{(b.Value ? "Enabled" : "Disabled")} displaying Unity logs in the developer console.");
                },
                () =>
                {
                    LogVariable("Log unity logs", _displayUnityLogs);
                }
            ));

            AddCommand(Command.Create<bool?>(
                "log_errors",
                "",
                "Query, enable or disable displaying Unity errors in the developer console",
                Parameter.Create("enabled", "Whether Unity errors should be displayed in the developer console (use \"NULL\" to toggle)"),
                b =>
                {
                    if (!b.HasValue)
                    {
                        b = !_displayUnityErrors;
                    }

                    _displayUnityErrors = b.Value;
                    LogSuccess($"{(b.Value ? "Enabled" : "Disabled")} displaying Unity errors in the developer console.");
                },
                () =>
                {
                    LogVariable("Log unity errors", _displayUnityErrors);
                }
            ));

            AddCommand(Command.Create<bool?>(
                "log_exceptions",
                "",
                "Query, enable or disable displaying Unity exceptions in the developer console",
                Parameter.Create("enabled", "Whether Unity exceptions should be displayed in the developer console (use \"NULL\" to toggle)"),
                b =>
                {
                    if (!b.HasValue)
                    {
                        b = !_displayUnityExceptions;
                    }

                    _displayUnityExceptions = b.Value;
                    LogSuccess($"{(b.Value ? "Enabled" : "Disabled")} displaying Unity exceptions in the developer console.");
                },
                () =>
                {
                    LogVariable("Log unity exceptions", _displayUnityExceptions);
                }
            ));

            AddCommand(Command.Create<bool?>(
                "log_warnings",
                "",
                "Query, enable or disable displaying Unity warnings in the developer console",
                Parameter.Create("enabled", "Whether Unity warnings should be displayed in the developer console (use \"NULL\" to toggle)"),
                b =>
                {
                    if (!b.HasValue)
                    {
                        b = !_displayUnityWarnings;
                    }

                    _displayUnityWarnings = b.Value;
                    LogSuccess($"{(b.Value ? "Enabled" : "Disabled")} displaying Unity warnings in the developer console.");
                },
                () =>
                {
                    LogVariable("Log unity warnings", _displayUnityWarnings);
                }
            ));

            #endregion

            #region Reflection commands

            AddCommand(Command.Create<string>(
                "cs_evaluate",
                "cs_eval,evaluate,eval",
                "Evaluate a C# expression or statement and display the result",
                Parameter.Create("expression", "The expression to evaluate"),
                input =>
                {
                    if (_monoEvaluator == null)
                    {
                        DevConsole.LogError(MonoNotSupportedText);
                        return;
                    }

                    try
                    {
                        if (!input.EndsWith(";"))
                        {
                            input += ";";
                        }

                        object result = _monoEvaluator.Evaluate(input);

                        if (result == null)
                        {
                            Log($"Null.");
                            return;
                        }

                        if (result.GetType() != typeof(string) && typeof(IEnumerable).IsAssignableFrom(result.GetType()))
                        {
                            Log($"{{ {string.Join(", ", ((IEnumerable)result).Cast<object>())} }}");
                            return;
                        }

                        Log($"{result}.");
                    }
                    catch (Exception e)
                    {
                        LogError($"An exception was thrown whilst evaluating the C# expression or statement: {e.Message}.");
                    }
                }
            ));

            AddCommand(Command.Create<string>(
                "cs_run",
                "run",
                "Execute a C# expression or statement",
                Parameter.Create("statement", "The statement to execute"),
                input =>
                {
                    if (_monoEvaluator == null)
                    {
                        DevConsole.LogError(MonoNotSupportedText);
                        return;
                    }

                    try
                    {
                        if (!input.EndsWith(";"))
                        {
                            input += ";";
                        }

                        if (_monoEvaluator.Run(input))
                        {
                            LogSuccess("Successfully executed the C# expression or statement.");
                        }
                        else
                        {
                            LogError("Failed to parse the C# expression or statement.");
                        }
                    }
                    catch (Exception e)
                    {
                        LogError($"An exception was thrown whilst executing the C# expression or statement: {e.Message}.");
                    }
                }
            ));

            AddCommand(Command.Create(
                "cs_usings",
                "",
                "Display a list of all active using statements",
                () =>
                {
                    if (_monoEvaluator == null)
                    {
                        DevConsole.LogError(MonoNotSupportedText);
                        return;
                    }

                    string usings = _monoEvaluator.GetUsing();

                    if (string.IsNullOrEmpty(usings))
                    {
                        Log("There are no active using statements.");
                        return;
                    }

                    LogSeperator("Usings");
                    Log(usings.TrimEnd('\n'));
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "cs_variables",
                "cs_vars",
                "Display a list of all local variables defined",
                () =>
                {
                    if (_monoEvaluator == null)
                    {
                        DevConsole.LogError(MonoNotSupportedText);
                        return;
                    }

                    string vars = _monoEvaluator.GetVars();

                    if (string.IsNullOrEmpty(vars))
                    {
                        Log("There are no local variables defined.");
                        return;
                    }

                    LogSeperator("Local variables");
                    Log(vars.TrimEnd('\n'));
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create<string, bool>(
                "cs_setusing",
                "",
                "Set whether a using statement is included automatically when starting the developer console",
                Parameter.Create("namespace", "Namespace to use as the using statement (e.g. \"System.Collections\""),
                Parameter.Create("enabled", "Whether the using statement is automatically included upon starting the developer console"),
                (usingName, enabled) =>
                {
                    if (enabled)
                    {
                        if (_includedUsings.Contains(usingName))
                        {
                            LogError($"The specifed using statement is already enabled: \"{usingName}\".");
                            return;
                        }

                        _includedUsings.Add(usingName);
                        LogSuccess($"Enabled \"{usingName}\" as an automatically included using statement.");
                    }
                    else
                    {
                        if (!_includedUsings.Contains(usingName))
                        {
                            LogError($"The specified using statement is already disabled: \"{usingName}\".");
                            return;
                        }

                        _includedUsings.Remove(usingName);
                        LogSuccess($"Disabled \"{usingName}\" as an automatically included using statement.");
                    }
                }
            ));

            AddCommand(Command.Create(
                "cs_autousings",
                "",
                "Display a list of all user-defined using statements that are included automatically when starting the developer console",
                () =>
                {
                    if (_includedUsings.Count == 0)
                    {
                        Log("There are no user-defined using statements.");
                        return;
                    }

                    LogSeperator("User-defined usings");
                    LogCollection(_includedUsings);
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "cs_help",
                "",
                "Display information about the reflection commands",
                () =>
                {
                    Command evaluateCmd = GetCommand("cs_evaluate");
                    Command runCmd = GetCommand("cs_run");
                    Command usingsCmd = GetCommand("cs_usings");
                    Command varsCmd = GetCommand("cs_vars");
                    Command setUsingCmd = GetCommand("cs_setusing");
                    Command autoUsingsCmd = GetCommand("cs_autousings");

                    LogSeperator("Reflection commands help");
                    LogVariable(evaluateCmd.ToFormattedString(), evaluateCmd.HelpText);
                    LogVariable(runCmd.ToFormattedString(), runCmd.HelpText);
                    LogVariable(usingsCmd.ToFormattedString(), usingsCmd.HelpText);
                    LogVariable(varsCmd.ToFormattedString(), varsCmd.HelpText);
                    LogVariable(setUsingCmd.ToFormattedString(), setUsingCmd.HelpText);
                    LogVariable(autoUsingsCmd.ToFormattedString(), autoUsingsCmd.HelpText);
                    LogSeperator();
                }
            ));

            #endregion

            #region Stat display commands

            AddCommand(Command.Create(
                "stats_help",
                "",
                "Display information about the developer console stats feature",
                () =>
                {
                    DevConsole.LogSeperator("Developer console tracked stats display");
                    DevConsole.Log("When enabled, stats such as variables can be displayed on-screen during gameplay.");
                    DevConsole.Log("There are three ways to add tracked stats:");
                    DevConsole.Log($"1. Using commands ({GetCommand("stats_set").GetFormattedName()}) to add a stat that is evaluated as a C# expression;");
                    DevConsole.Log($"2. Declaring a <i>{typeof(DevConsoleTrackedStatAttribute).Name}</i> attribute on a declared static field or property; or");
                    DevConsole.Log($"3. Calling <i>DevConsole.SetTrackedStat()</i> to add a stat this is retrieved via a provided lambda function.");
                    DevConsole.LogSeperator();
                }
                ));

            AddCommand(Command.Create<bool?>(
                "stats_showfps",
                "stats_displayfps,showfps,displayfps",
                "Query or set whether the fps is being displayed on-screen",
                Parameter.Create("enabled", "Whether the fps is being displayed on-screen (use \"NULL\" to toggle)"),
                b =>
                {
                    if (!b.HasValue)
                    {
                        b = !_isDisplayingFps;
                    }

                    if (b != _isDisplayingFps)
                    {
                        _isDisplayingFps = !_isDisplayingFps;

                        if (_isDisplayingFps)
                        {
                            _fps = 0;
                            _fpsMs = 0f;
                            _fpsDeltaTime = 0f;
                            _fpsElapsed = 0f;
                            _fpsStyle = null;
                        }
                    }

                    LogSuccess($"{(b.Value ? "Enabled" : "Disabled")} the on-screen fps.");
                },
                () => LogVariable("Show fps", _isDisplayingFps)
                ));

            AddCommand(Command.Create(
                "stats_list",
                "",
                "Display a list of the tracked developer console stats that can be displayed on-screen",
                () =>
                {
                    if (!_stats.Any())
                    {
                        DevConsole.Log($"There are no tracked developer console stats. Use {GetCommand("stats_set").GetFormattedName()} to set one up.");
                        return;
                    }

                    LogSeperator("Tracked developer console stats");
                    LogCollection(_stats, x => $"<b>{x.Key}:</b> {x.Value} ({x.Value.Desc}){(_hiddenStats.Contains(x.Key) ? " <i>[Disabled]</i>" : "")}.");
                    LogSeperator();
                }
                ));

            AddCommand(Command.Create<bool?>(
                "stats_display",
                "",
                "Set or query whether the developer console stats are being displayed on-screen",
                Parameter.Create("display", ""),
                b =>
                {
                    if (!b.HasValue)
                    {
                        b = !_isDisplayingStats;
                    }

                    _isDisplayingStats = b.Value;
                    DevConsole.LogSuccess($"{(_isDisplayingStats ? "Enabled" : "Disabled")} the on-screen developer console stats display.");
                },
                () => DevConsole.LogVariable("Display on-screen developer console stats", _isDisplayingStats)
                ));

            AddCommand(Command.Create<string, string>(
                "stats_set",
                "",
                "Set a stat on the developer console which can be displayed on-screen",
                Parameter.Create("name", "Name of the stat"),
                Parameter.Create("expression", "The C# expression to evaluate"),
                (name, expression) =>
                {
                    if (name == null || expression == null)
                    {
                        DevConsole.LogError("Parameters cannot be null.");
                        return;
                    }

                    if (!expression.EndsWith(";"))
                    {
                        expression += ";";
                    }

                    _stats[name] = new EvaluatedStat(expression);
                    DevConsole.LogSuccess($"Successfully set {name} as a developer console stat.");
                }
                ));

            AddCommand(Command.Create<string>(
                "stats_evaluate",
                "stats_eval",
                "Evaluate one of the developer console stats",
                Parameter.Create("name", "Name of the stat"),
                name =>
                {
                    if (!_stats.ContainsKey(name))
                    {
                        DevConsole.LogError($"Could not find {name}. Use {GetCommand("stats_display").GetFormattedName()} to display a list of all developer console stats.");
                        return;
                    }

                    try
                    {
                        DevConsole.LogVariable(name, _stats[name].GetResult(_monoEvaluator) ?? "NULL");
                    }
                    catch (Exception e)
                    {
                        DevConsole.LogException(e);
                    }
                }
                ));

            AddCommand(Command.Create<string>(
                "stats_remove",
                "",
                "Remove a stat from the developer console",
                Parameter.Create("name", "Name of the stat"),
                name =>
                {
                    if (!_stats.Remove(name))
                    {
                        DevConsole.LogError($"Could not remove {name}. Use {GetCommand("stats_display").GetFormattedName()} to display a list of all developer console stats.");
                        return;
                    }

                    _hiddenStats.Remove(name);
                    _cachedStats.Remove(name);
                    DevConsole.LogSuccess($"Successfully removed {name} from the developer console stats.");
                }
                ));

            AddCommand(Command.Create<string, bool?>(
                "stats_toggle",
                "",
                "Set whether a particular developer console stat should be displayed or not",
                Parameter.Create("name", "Name of the stat"),
                Parameter.Create("display", ""),
                (name, b) =>
                {
                    if (!_stats.ContainsKey(name))
                    {
                        DevConsole.LogError($"Could not find {name}. Use {GetCommand("stats_display").GetFormattedName()} to display a list of all developer console stats.");
                        return;
                    }

                    // Flip
                    if (!b.HasValue)
                    {
                        b = _hiddenStats.Contains(name);
                    }

                    if (b.Value)
                    {
                        _hiddenStats.Remove(name);
                    }
                    else
                    {
                        _hiddenStats.Add(name);
                    }

                    DevConsole.LogSuccess($"{(b.Value ? "Enabled" : "Disabled")} the on-screen developer console stats display for {name}.");
                }
                ));

            AddCommand(Command.Create<int>(
                "stats_fontsize",
                "",
                "Query or set the font size of the tracked developer console stats & fps display",
                Parameter.Create("fontSize", $"Size of the font (default: {StatDefaultFontSize})"),
                f =>
                {
                    if (f <= 0)
                    {
                        LogError("Font size must be non-zero and positive.");
                        return;
                    }

                    int oldStatFontSize = _statFontSize;
                    _statFontSize = f;
                    LogSuccess($"Set the stats font size to {_statFontSize} (was {oldStatFontSize}).");
                },
                () => LogVariable("Stats font size", _statFontSize)
                ));

            #endregion

            #region Misc commands

            AddCommand(Command.Create(
                "time",
                "",
                "Display the current time",
                () => Log($"Current time: {DateTime.Now}.")
            ));

            AddCommand(Command.Create(
                "sys_info",
                "",
                "Display system information",
                () =>
                {
                    LogSeperator("System information");

                    LogVariable("Name", SystemInfo.deviceName);
                    LogVariable("Model", SystemInfo.deviceModel);
                    LogVariable("Type", SystemInfo.deviceType, SystemInfo.operatingSystemFamily == OperatingSystemFamily.Other ? "" : $" ({SystemInfo.operatingSystemFamily})");
                    LogVariable("OS", SystemInfo.operatingSystem);
                    if (SystemInfo.batteryLevel != -1)
                    {
                        Log($"Battery status: {SystemInfo.batteryStatus} ({SystemInfo.batteryLevel * 100f}%).");
                    }

                    Log("");

                    LogVariable("CPU", SystemInfo.processorType);
                    LogVariable(" Memory size", SystemInfo.systemMemorySize, " megabytes");
                    LogVariable(" Processors", SystemInfo.processorCount);
                    LogVariable(" Frequency", SystemInfo.processorFrequency, " MHz");

                    Log("");

                    LogVariable("GPU", SystemInfo.graphicsDeviceName);
                    LogVariable(" Type", SystemInfo.graphicsDeviceType);
                    LogVariable(" Vendor", SystemInfo.graphicsDeviceVendor);
                    LogVariable(" Version", SystemInfo.graphicsDeviceVersion);
                    LogVariable(" Memory size", SystemInfo.graphicsMemorySize, " megabytes");
                    LogVariable(" Multi threaded", SystemInfo.graphicsMultiThreaded);

                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "datapath",
                "",
                "Display information about where data is stored by Unity and the developer console",
                () =>
                {
                    LogSeperator("Data paths");
                    LogVariable("Data path", Application.dataPath);
                    LogVariable("Persistent data path", Application.persistentDataPath);
                    LogVariable("Developer console data path", DevConsoleData.FilePath);
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create<Color>(
                "colour",
                "color",
                "Display a colour in the developer console",
                Parameter.Create("colour", "Colour to display. Formats: #RRGGBBAA (hex), #RRGGBB (hex), name (red,yellow,etc.), R.R,G.G,B.B (0.0-1.0), RRR,GGG,BBB (0-255)"),
                colour => LogVariable($"<color=#{ColorUtility.ToHtmlStringRGBA(colour)}>Colour</color>", colour),
                () => Log("Supported formats: #RRGGBBAA (hex), #RRGGBB (hex), name (red,yellow,etc.), R.R,G.G,B.B (0.0-1.0), RRR,GGG,BBB (0-255).")
                ));

            #endregion
        }

        /// <summary>
        ///     Add all the built-in parameter type parser functions.
        /// </summary>
        private void InitBuiltInParsers()
        {
            AddParameterType(typeof(bool),
                s =>
                {
                    // Allow bools to be in the form of "0" and "1"
                    if (int.TryParse(s, out int result))
                    {
                        if (result == 0)
                        {
                            return false;
                        }
                        else if (result == 1)
                        {
                            return true;
                        }
                    }

                    return Convert.ChangeType(s, typeof(bool));
                });

            AddParameterType(typeof(bool?),
                s =>
                {
                    // Allow null value, representing a toggle
                    if (s.ToLower() == "null" || s == "~")
                    {
                        return null;
                    }

                    return ParseParameter(s, typeof(bool));
                });

            AddParameterType(typeof(Color),
                s =>
                {
                    if (ColorUtility.TryParseHtmlString(s, out Color colour))
                    {
                        return colour;
                    }

                    string[] components = s.Split(',');
                    int length = Math.Min(4, components.Length);

                    try
                    {
                        colour = Color.black;
                        for (int i = 0; i < length; ++i)
                        {
                            colour[i] = Mathf.RoundToInt(int.Parse(components[i]) / 255f);
                        }
                    }
                    catch
                    {
                        colour = Color.black;
                        for (int i = 0; i < length; ++i)
                        {
                            colour[i] = float.Parse(components[i]);
                        }
                    }

                    return colour;
                });
        }

        /// <summary>
        ///     Search for, and add all the attribute commands & other custom attributes.
        /// </summary>
        private void InitAttributes()
        {
            // https://github.com/yasirkula/UnityIngameDebugConsole/blob/master/Plugins/IngameDebugConsole/Scripts/DebugLogConsole.cs
            // Implementation of finding attributes sourced from yasirkula's code

#if UNITY_EDITOR || !NETFX_CORE
            string[] ignoredAssemblies = new string[]
            {
                "Unity",
                "System",
                "Mono.",
                "mscorlib",
                "netstandard",
                "TextMeshPro",
                "Microsoft.GeneratedCode",
                "I18N",
                "Boo.",
                "UnityScript.",
                "ICSharpCode.",
                "ExCSS.Unity",
#if UNITY_EDITOR
				"Assembly-CSharp-Editor",
                "Assembly-UnityScript-Editor",
                "nunit.",
                "SyntaxTree.",
                "AssetStoreTools"
#endif
            };
#endif

#if UNITY_EDITOR || !NETFX_CORE
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
#else
            foreach (Assembly assembly in new Assembly[] { typeof(DebugConsoleMono).Assembly })
#endif
            {
#if (NET_4_6 || NET_STANDARD_2_0) && (UNITY_EDITOR || !NETFX_CORE)
                if (assembly.IsDynamic)
                    continue;
#endif

                string assemblyName = assembly.GetName().Name;

#if UNITY_EDITOR || !NETFX_CORE
                if (ignoredAssemblies.Any(a => assemblyName.ToLower().StartsWith(a.ToLower())))
                {
                    continue;
                }
#endif

                try
                {
                    foreach (Type type in assembly.GetExportedTypes())
                    {
                        foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            foreach (object attribute in method.GetCustomAttributes(typeof(DevConsoleCommandAttribute), false))
                            {
                                DevConsoleCommandAttribute commandAttribute = (DevConsoleCommandAttribute)attribute;
                                if (commandAttribute != null)
                                {
                                    AddCommand(Command.Create(commandAttribute, method), commandAttribute.OnlyInDevBuild, true);
                                }
                            }
                        }

                        foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            DevConsoleTrackedStatAttribute attribute = field.GetCustomAttribute<DevConsoleTrackedStatAttribute>();
                            if (attribute == null)
                            {
                                continue;
                            }

                            string name = attribute.Name ?? field.Name;
                            _stats[name] = new ReflectedStat(field);
                            if (!attribute.StartEnabled && !_persistStats.Contains(name))
                            {
                                _hiddenStats.Add(name);
                            }
                        }

                        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            DevConsoleTrackedStatAttribute attribute = property.GetCustomAttribute<DevConsoleTrackedStatAttribute>();
                            if (attribute == null)
                            {
                                continue;
                            }

                            string name = attribute.Name ?? property.Name;
                            _stats[name] = new ReflectedStat(property);
                            if (!attribute.StartEnabled && !_persistStats.Contains(name))
                            {
                                _hiddenStats.Add(name);
                            }
                        }
                    }
                }
                catch (NotSupportedException) { }
                catch (System.IO.FileNotFoundException) { }
                catch (Exception e)
                {
                    Debug.LogError("Error whilst searching for developer console attributes in assembly(" + assemblyName + "): " + e.Message + ".");
                }
            }
        }

        #endregion

        #region Command methods

        /// <summary>
        ///     Convert the raw input string into an array.
        ///     First element is the command name.
        ///     Remaining elements are the provided parameters (which may not correlate to the command parameters).
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        private string[] GetInput(string rawInput)
        {
            string[] split = rawInput.Split(' ');
            if (split.Length <= 1)
            {
                return split;
            }

            List<string> parameters = new List<string>()
            {
                split[0]        // command name (e.g. "print")
            };
            bool buildingParameter = false;
            string parameter = "";
            for (int i = 1; i < split.Length; i++)
            {
                if (!buildingParameter)
                {
                    if (split[i].StartsWith("\"") && i != split.Length - 1)
                    {
                        if (!split[i].EndsWith("\""))
                        {
                            buildingParameter = true;
                            parameter = split[i].TrimStart('"');
                        }
                        else
                        {
                            parameters.Add(split[i].Trim('"'));
                        }
                    }
                    else
                    {
                        parameters.Add(split[i]);
                    }
                }
                else
                {
                    if (split[i].EndsWith("\"") || i == split.Length - 1)
                    {
                        buildingParameter = false;
                        parameter += " " + split[i].TrimEnd('\"');
                        parameters.Add(parameter);
                    }
                    else
                    {
                        parameter += " " + split[i];
                    }
                }
            }

            return parameters.ToArray();
        }

        /// <summary>
        ///     Convert the input into a converted array of parameters based on the parameter count.
        ///     Extra parameters will be aggregated together.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="parameterCount"></param>
        /// <returns></returns>
        private string[] ConvertInput(string[] input, int parameterCount)
        {
            if (input.Length - 1 <= parameterCount)
            {
                return input;
            }

            string[] newInput = new string[parameterCount + 1];
            newInput[0] = input[0];
            string aggregatedFinalParameter = "";
            for (int i = 1; i < input.Length; i++)
            {
                if (i - 1 < parameterCount - 1)
                {
                    newInput[i] = input[i];
                }
                else if (i - 1 == parameterCount - 1)
                {
                    aggregatedFinalParameter = input[i];
                }
                else
                {
                    aggregatedFinalParameter += " " + input[i];
                }
            }
            newInput[newInput.Length - 1] = aggregatedFinalParameter;
            return newInput;
        }

        /// <summary>
        ///     Parse a parameter based on the user input.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private object ParseParameter(string input, Type type)
        {
            // Special case if the type is nullable or a class
            if ((Nullable.GetUnderlyingType(type) != null || type.IsClass) && (input.ToLower() == "null" || input == "~"))
            {
                return null;
            }

            // Special case if the type is a struct
            if (type.IsValueType && (input.ToLower() == "default" || input == "~"))
            {
                return Activator.CreateInstance(type);
            }

            // Check if a parse function exists for the type
            if (_parameterParseFuncs.TryGetValue(type, out Func<string, object> parseFunc))
            {
                return parseFunc(input);
            }

            // Special case if the type is an enum (and it isn't handled specifically by a parser function)
            if (type.IsEnum)
            {
                object enumParameter;
                if ((enumParameter = Enum.Parse(type, input, true)) != null || (int.TryParse(input, out int enumValue) && (enumParameter = Enum.ToObject(type, enumValue)) != null))
                {
                    return enumParameter;
                }
            }

            // Try to convert as an IConvertible
            return Convert.ChangeType(input, type);
        }

        /// <summary>
        ///     Add a command to the command history given the name and user input.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="input"></param>
        private void AddToCommandHistory(string name, string input)
        {
            _previousCommand = _currentCommand;
            _currentCommand = name;
            _commandHistory.Insert(0, input);
            if (_commandHistory.Count == CommandHistoryLength)
            {
                _commandHistory.RemoveAt(_commandHistory.Count - 1);
            }
            _commandHistoryIndex = -1;
        }

        /// <summary>
        ///     Cycle the command history in the given direction and display to the user.
        /// </summary>
        /// <param name="direction"></param>
        private void CycleCommandHistory(int direction)
        {
            if (_commandHistory.Count == 0 ||
                (_commandHistoryIndex == _commandHistory.Count - 1 && direction == 1) ||
                (_commandHistoryIndex == -1 && direction == -1))
            {
                return;
            }

            if (_commandHistoryIndex == 0 && direction == -1)
            {
                _commandHistoryIndex = -1;
                InputText = string.Empty;
                return;
            }

            _commandHistoryIndex += direction;
            InputText = _commandHistory[_commandHistoryIndex];
            InputCaretPosition = InputText.Length;
        }

        #endregion

        #region Suggestion methods

        /// <summary>
        ///     Refresh the command suggestions based on the user input.
        /// </summary>
        private void RefreshCommandSuggestions()
        {
            // Do not show if there is no command or the parameters are being specified
            if (InputText.Length == 0 || InputText.StartsWith(" ") || InputText.Split(' ').Length > 1 || _commandHistoryIndex != -1)
            {
                _suggestionText.text = string.Empty;
                _commandStringSuggestions = null;
                _commandSuggestions = null;
                _commandSuggestionIndex = 0;
                return;
            }

            // Get a collection of command suggestions and show the first result
            _commandStringSuggestions = GetCommandSuggestions(InputText, out _commandSuggestions);
            _commandSuggestionIndex = 0;
            _suggestionText.text = _commandStringSuggestions.ElementAtOrDefault(_commandSuggestionIndex) ?? string.Empty;
        }

        /// <summary>
        ///     Refresh the parameter suggestions based on the user input.
        /// </summary>
        private void RefreshCommandParameterSuggestions()
        {
            if (_commandHistoryIndex != -1)
            {
                return;
            }

            string suffix = "";

            // If there is a current command suggestion, use it for the parameter suggestions
            if (_commandStringSuggestions?.Length > 0)
            {
                Command command = _commandSuggestions.ElementAtOrDefault(_commandSuggestionIndex);
                if (command == null)
                {
                    return;
                }
                suffix = $" {string.Join(" ", command.Parameters.Select(x => x.ToString()))}";
            }

            // Otherwise determine the current command from the input
            else
            {
                // Split the input
                string[] splitInput = InputText.Split(' ');
                if (splitInput.Length == 0)
                {
                    return;
                }

                // Get the command
                Command command = GetCommand(splitInput[0]);
                if (command == null)
                {
                    return;
                }

                // Gather the remaining parameters
                List<string> parameters = new List<string>();
                for (int i = splitInput.Length - 2; i < command.Parameters.Length; ++i)
                {
                    // If the current parameter is empty, then still show the parameter suggestion
                    if (i + 1 > 0 && i + 1 < splitInput.Length && !string.IsNullOrEmpty(splitInput[i + 1]))
                    {
                        continue;
                    }

                    parameters.Add(command.Parameters[i].ToString());
                }

                if (parameters.Count == 0)
                {
                    return;
                }

                suffix = $"{new string(' ', string.Join(" ", splitInput.Where(x => !string.IsNullOrEmpty(x))).Length)} {string.Join(" ", parameters)}";
            }

            _suggestionText.text += suffix;
        }

        /// <summary>
        ///     Get the command suggestions for the provided text.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="commands">Array of commands.</param>
        /// <returns>Array of command suggestion substrings.</returns>
        private string[] GetCommandSuggestions(string text, out Command[] commands)
        {
            // Get a list of command names that could fill in the missing text
            // Store alias suggestions separately and add on end later (so the result contains real commands before aliases)
            List<string> suggestions = new List<string>();
            List<string> aliasSuggestions = new List<string>();
            List<Command> cmds = new List<Command>();
            List<Command> aliasCmds = new List<Command>();
            string textToLower = text.ToLower();

            foreach (Command command in _commands.Values)
            {
                // Check if the command name matches the text
                if (command.Name.StartsWith(textToLower))
                {
                    // Combine current input with suggestion so capitalisation remains
                    // Add to suggestions list
                    suggestions.Add(text + command.Name.Substring(text.Length));
                    cmds.Add(command);
                }

                // Iterate over the command aliases
                foreach (string alias in command.Aliases)
                {
                    // Check if this command alias matches the text
                    if (alias.StartsWith(textToLower))
                    {
                        // Combine current input with suggestion so capitalisation remains
                        // Add to alias suggestions list
                        aliasSuggestions.Add(text + alias.Substring(text.Length));
                        aliasCmds.Add(command);
                    }
                }
            }
            suggestions.AddRange(aliasSuggestions);
            cmds.AddRange(aliasCmds);
            commands = cmds.ToArray();
            return suggestions.ToArray();
        }

        /// <summary>
        ///     Attempt to autocomplete based on the current command suggestion.
        /// </summary>
        private void AutoComplete()
        {
            if (_commandStringSuggestions == null || _commandStringSuggestions.Length == 0)
            {
                return;
            }

            // Complete the input text with the current command suggestion
            InputText = _commandStringSuggestions[_commandSuggestionIndex];
            InputCaretPosition = InputText.Length;
        }

        /// <summary>
        ///     Cycle the command suggestions in the given direction, and display to the user.
        /// </summary>
        /// <param name="direction"></param>
        private void CycleCommandSuggestions(int direction)
        {
            if (_commandStringSuggestions == null || _commandStringSuggestions.Length == 0)
            {
                return;
            }

            // Cycle the command suggestion in the given direction
            _commandSuggestionIndex += direction;
            if (_commandSuggestionIndex < 0)
            {
                _commandSuggestionIndex = _commandStringSuggestions.Length - 1;
            }
            else if (_commandSuggestionIndex == _commandStringSuggestions.Length)
            {
                _commandSuggestionIndex = 0;
            }
            _suggestionText.text = _commandStringSuggestions[_commandSuggestionIndex];
            RefreshCommandParameterSuggestions();
            InputCaretPosition = InputText.Length;
        }

        #endregion

        #region Log content methods

        /// <summary>
        ///     Process the provided log text, displaying it in the dev console log.
        /// </summary>
        /// <param name="logText"></param>
        private void ProcessLogText(in string logText)
        {
            // Determine number of vertices needed to render the log text
            int vertexCountStored = GetVertexCount(logText);

            // Check if the log text exceeds the maximum vertex count
            if (vertexCountStored > MaximumTextVertices)
            {
                // Split into two halves and recursively call this same method

                // Attempt to split into two halves at the closest new line character from the middle
                int length = logText.IndexOf('\n', logText.Length / 2);
                if (length == -1)
                {
                    // Otherwise just split straight in the middle (may format weirdly in the console)
                    length = logText.Length / 2;
                }

                // Process the first half
                ProcessLogText(logText.Substring(0, length));

                // Process the second half
                ProcessLogText(logText.Substring(length, logText.Length - length));
                return;
            }

            // Check if the log text appended to the current logs exceeds the maximum vertex count
            else if (_vertexCount + vertexCountStored > MaximumTextVertices)
            {
                // Split once
                AddLogField();
                _logFields.Last().text = logText.TrimStart('\n');
                _vertexCount = vertexCountStored;
            }

            // Otherwise, simply append the log text to the current logs
            else
            {
                _logFields.Last().text += logText;
                _vertexCount += vertexCountStored;
            }
        }

        /// <summary>
        ///     Get the vertex count required to render the provided rich text.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private int GetVertexCount(string text)
        {
            // Determine the number of vertices required to render the provided rich text
            Text logText = _logFields.Last().textComponent;
            _textGenerator.Populate(text, logText.GetGenerationSettings(logText.rectTransform.rect.size));
            return _textGenerator.vertexCount;
        }

        /// <summary>
        ///     Instantiate a new log field instance and add to the list.
        /// </summary>
        private void AddLogField()
        {
            // Instantiate a new log field and set it up with default values
            GameObject obj = Instantiate(_logFieldPrefab, _logContentTransform);
            InputField logField = obj.GetComponent<InputField>();
            logField.text = string.Empty;
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(_currentLogFieldWidth, rect.sizeDelta.y);
            _logFields.Add(logField);
            obj.SetActive(true);
        }

        /// <summary>
        ///     Clear all the existing log field instances, and then create one default.
        /// </summary>
        private void ClearLogFields()
        {
            // Clear log fields
            foreach (InputField logField in _logFields)
            {
                Destroy(logField.gameObject);
            }
            _logFields.Clear();
            AddLogField();
        }

        /// <summary>
        ///     Refresh the size of all the log field instances.
        /// </summary>
        private void RefreshLogFieldsSize()
        {
            // Refresh the width of the log fields to the current width (determined by dev console window width)
            RectTransform rect;
            foreach (InputField logField in _logFields)
            {
                rect = logField.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(_currentLogFieldWidth, rect.sizeDelta.y);
            }
            RebuildLayout();
        }

        /// <summary>
        ///     Forcefully rebuild the layout, otherwise transforms are positioned incorrectly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RebuildLayout()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_logContentTransform);
        }

        #endregion

        #region Physical input methods

        /// <summary>
        ///     Check if the specified key was pressed this frame, using the correct input system.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetKeyDown(InputKey key)
        {
#if USE_NEW_INPUT_SYSTEM
            return Keyboard.current[key].wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        /// <summary>
        ///     Check if the specified key is pressed, using the correct input system.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetKey(InputKey key)
        {
#if USE_NEW_INPUT_SYSTEM
            return Keyboard.current[key].isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        /// <summary>
        ///     Get the current mouse position, using the correct input system.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 GetMousePosition()
        {
#if USE_NEW_INPUT_SYSTEM
            return Mouse.current.position.ReadValue();
#else
            return Input.mousePosition;
#endif
        }

        #endregion

        #region Pref methods

        /// <summary>
        ///     Save all the preferences to file.
        /// </summary>
        private void SavePreferences()
        {
            DevConsoleData.SetObject(PrefConsoleToggleKey, ConsoleToggleKey);
            DevConsoleData.SetObject(PrefBindings, _bindings);
            DevConsoleData.SetObject(PrefDisplayUnityErrors, _displayUnityErrors);
            DevConsoleData.SetObject(PrefDisplayUnityExceptions, _displayUnityExceptions);
            DevConsoleData.SetObject(PrefDisplayUnityWarnings, _displayUnityWarnings);
            DevConsoleData.SetObject(PrefShowFps, _isDisplayingFps);
            DevConsoleData.SetObject(PrefLogTextSize, LogTextSize);
            DevConsoleData.SetObject(PrefIncludedUsings, _includedUsings);
            DevConsoleData.SetObject(PrefShowStats, _isDisplayingStats);
            DevConsoleData.SetObject(PrefStats, _stats.Where(x => x.Value is EvaluatedStat).ToDictionary(x => x.Key, x => ((EvaluatedStat)x.Value).Expression));
            DevConsoleData.SetObject(PrefHiddenStats, new HashSet<string>(_hiddenStats.Where(x => _stats.Keys.Contains(x))));
            DevConsoleData.SetObject(PrefStatsFontSize, _statFontSize);
            DevConsoleData.SetObject(PrefPersistStats, _stats.Where(x => !(x.Value is EvaluatedStat) && !_hiddenStats.Contains(x.Key)).Select(x => x.Key).ToList());

            DevConsoleData.Save();
        }

        /// <summary>
        ///     Load all the preferences from file.
        /// </summary>
        private void LoadPreferences()
        {
            DevConsoleData.Load();

            ConsoleToggleKey = DevConsoleData.GetObject(PrefConsoleToggleKey, (InputKey?)DefaultToggleKey);
            _bindings = DevConsoleData.GetObject(PrefBindings, new Dictionary<InputKey, string>());
            _displayUnityLogs = DevConsoleData.GetObject(PrefDisplayUnityLogs, true);
            _displayUnityErrors = DevConsoleData.GetObject(PrefDisplayUnityErrors, true);
            _displayUnityExceptions = DevConsoleData.GetObject(PrefDisplayUnityExceptions, true);
            _displayUnityWarnings = DevConsoleData.GetObject(PrefDisplayUnityWarnings, true);
            _isDisplayingFps = DevConsoleData.GetObject(PrefShowFps, false);
            LogTextSize = DevConsoleData.GetObject(PrefLogTextSize, _initLogTextSize);
            _includedUsings = DevConsoleData.GetObject(PrefIncludedUsings, new List<string>()
            {
                "System", "System.Linq", "UnityEngine", "UnityEngine.SceneManagement", "UnityEngine.UI"
            });
            _isDisplayingStats = DevConsoleData.GetObject(PrefShowStats, true);
            foreach (KeyValuePair<string, string> stat in DevConsoleData.GetObject(PrefStats, new Dictionary<string, string>()))
            {
                _stats.Add(stat.Key, new EvaluatedStat(stat.Value));
            }
            _hiddenStats = DevConsoleData.GetObject(PrefHiddenStats, new HashSet<string>());
            _statFontSize = DevConsoleData.GetObject(PrefStatsFontSize, StatDefaultFontSize);
            _persistStats = DevConsoleData.GetObject(PrefPersistStats, new List<string>());

            DevConsoleData.Clear();
        }

        #endregion

        #region Reflection methods

        /// <summary>
        ///     Initialsie the evaluator used for excuting C# expressions or statements.
        /// </summary>
        private void InitMonoEvaluator()
        {
            if (_monoEvaluator != null)
            {
                return;
            }

            try
            {
                CompilerSettings settings = new CompilerSettings();

                // Add assembly references to the settings
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly == null)
                    {
                        continue;
                    }

                    settings.AssemblyReferences.Add(assembly.FullName);
                }

                CompilerContext context = new CompilerContext(settings, new ConsoleReportPrinter());
                _monoEvaluator = new Evaluator(context);

                // Add the included using statements
                foreach (string includedUsing in _includedUsings)
                {
                    _monoEvaluator.Run($"using {includedUsing};");
                }
            }
            catch (Exception)
            {
                _monoEvaluator = null;
            }
        }

        /// <summary>
        ///     Attempt to evaluate user input as an evaluation.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        private string TryEvaluateInput(string rawInput)
        {
            if (_monoEvaluator == null)
            {
                return null;
            }

            try
            {
                return _monoEvaluator.Evaluate(rawInput + ";")?.ToString() ?? null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

        #region Stat methods

        /// <summary>
        ///     Set a tracked developer console stat to be displayed on-screen.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="func"></param>
        /// <param name="startEnabled"></param>
        internal void SetTrackedStat(string name, Func<object> func, bool startEnabled)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            _stats[name] = new LambdaStat(func);

            if (!startEnabled)
            {
                _hiddenStats.Add(name);
            }
        }

        /// <summary>
        ///     Remove a tracked developer console stat.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal bool RemoveTrackedStat(string name)
        {
            if (!_stats.Remove(name))
            {
                return false;
            }

            _hiddenStats.Remove(name);
            _cachedStats.Remove(name);
            return true;
        }

        #endregion

        #endregion

        #region Nested types

        /// <summary>
        ///     Abstract class for tracked developer console stats.
        /// </summary>
        private abstract class StatBase
        {
            #region Properties

            /// <summary>
            ///     Short name describing the type of stat (e.g. Evaluated, Reflected, Lambda).
            /// </summary>
            public abstract string Desc { get; }

            #endregion

            #region Methods

            /// <summary>
            ///     Get the stat result object.
            /// </summary>
            /// <param name="evaluator"></param>
            /// <returns></returns>
            public abstract object GetResult(Evaluator evaluator);

            /// <summary>
            ///     String representation that is used when displaying a list of all the tracked developer console stats.
            /// </summary>
            /// <returns></returns>
            public abstract override string ToString();

            #endregion
        }

        /// <summary>
        ///     Stat that evaluates a C# expression to determine the result.
        /// </summary>
        private sealed class EvaluatedStat : StatBase
        {
            #region Constructors

            public EvaluatedStat(string expression)
            {
                Expression = expression;
            }

            #endregion

            #region Properties

            public override string Desc => "Evaluated";

            /// <summary>
            ///     C# expression to be evaluated for the stat result.
            /// </summary>
            public string Expression { get; }

            #endregion

            #region Methods

            public override object GetResult(Evaluator evaluator)
            {
                if (evaluator == null)
                {
                    return MonoNotSupportedText;
                }

                return evaluator?.Evaluate(Expression) ?? null;
            }

            public override string ToString()
            {
                return Expression;
            }

            #endregion
        }

        /// <summary>
        ///     Stat that uses a user-defined lambda to retrieve the result.
        /// </summary>
        private sealed class LambdaStat : StatBase
        {
            #region Fields

            private readonly Func<object> _func;

            #endregion

            #region Constructors

            public LambdaStat(Func<object> func)
            {
                _func = func;
            }

            #endregion

            #region Properties

            public override string Desc => "Lambda";

            #endregion

            #region Methods

            public override object GetResult(Evaluator _ = null)
            {
                return _func?.Invoke() ?? null;
            }

            public override string ToString()
            {
                return "Cannot be viewed";
            }

            #endregion
        }

        /// <summary>
        ///     Stat that retrieves the result from a static field or property.
        /// </summary>
        private sealed class ReflectedStat : StatBase
        {
            #region Fields

            private readonly FieldInfo _field;

            private readonly PropertyInfo _property;

            #endregion

            #region Constructors

            public ReflectedStat(FieldInfo field)
            {
                _field = field;
            }

            public ReflectedStat(PropertyInfo property)
            {
                _property = property;
            }

            #endregion

            #region Properties

            public override string Desc => "Reflected";

            #endregion

            #region Methods

            public override object GetResult(Evaluator _ = null)
            {
                return _field?.GetValue(null) ?? _property?.GetValue(null);
            }

            public override string ToString()
            {
                return _field?.FieldType.Name ?? _property.PropertyType.Name;
            }

            #endregion
        }

        #endregion
    }
}