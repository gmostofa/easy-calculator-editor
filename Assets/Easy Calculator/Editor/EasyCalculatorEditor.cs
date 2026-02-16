using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Playnoob.Tools
{
    public class EasyCalculatorEditor : EditorWindow
    {
        private string input = "";
        private string displayResult = "0";
        private string errorMessage = "";

        private Vector2 historyScrollPos;
        private List<string> history = new List<string>();
        private const int MaxHistory = 150;

        private bool justCalculated;
        private bool useDegrees = true;
        private double memory = 0;

        private SerializedProperty targetProperty;

        private enum Mode { Simple, Scientific, Unity }
        private Mode currentMode = Mode.Simple;

        // Parser state
        private List<string> currentTokens = new List<string>();
        private int currentPos;

        [MenuItem("Window/Tools/Easy Calculator %#c", false, 2000)]
        public static void Open() => GetWindow<EasyCalculatorEditor>("Easy Calc");

        private void OnEnable()
        {
            minSize = new Vector2(480, 680);
            LoadHistory();
            EditorApplication.contextualPropertyMenu += OnContextMenu;
        }

        private void OnDisable()
        {
            SaveHistory();
            EditorApplication.contextualPropertyMenu -= OnContextMenu;
        }

        // ===================================================================
        // Context menu integration
        // ===================================================================
        private void OnContextMenu(GenericMenu menu, SerializedProperty prop)
        {
            if (!IsSupportedType(prop.propertyType)) return;

            menu.AddItem(new GUIContent("Calculate with Easy Calc"), false, () =>
            {
                Show();
                input = PropertyToExpression(prop);
                targetProperty = prop.Copy();
                justCalculated = false;
                currentMode = Mode.Unity;
                UpdateLivePreview();
            });
        }

        private static bool IsSupportedType(SerializedPropertyType t) =>
            t is SerializedPropertyType.Float or SerializedPropertyType.Integer or
            SerializedPropertyType.Vector2 or SerializedPropertyType.Vector3 or
            SerializedPropertyType.Vector4 or SerializedPropertyType.Quaternion or
            SerializedPropertyType.Color;

        private string PropertyToExpression(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Float => p.floatValue.ToString("G"),
            SerializedPropertyType.Integer => p.intValue.ToString(),
            SerializedPropertyType.Vector2 => $"vec2({p.vector2Value.x:G},{p.vector2Value.y:G})",
            SerializedPropertyType.Vector3 => $"vec3({p.vector3Value.x:G},{p.vector3Value.y:G},{p.vector3Value.z:G})",
            SerializedPropertyType.Vector4 => $"vec4({p.vector4Value.x:G},{p.vector4Value.y:G},{p.vector4Value.z:G},{p.vector4Value.w:G})",
            SerializedPropertyType.Quaternion => $"quat({p.quaternionValue.x:G},{p.quaternionValue.y:G},{p.quaternionValue.z:G},{p.quaternionValue.w:G})",
            SerializedPropertyType.Color => $"color({p.colorValue.r:G},{p.colorValue.g:G},{p.colorValue.b:G},{p.colorValue.a:G})",
            _ => ""
        };

        private void ApplyToProperty(object result)
        {
            if (targetProperty == null) return;

            Undo.RecordObject(targetProperty.serializedObject.targetObject, "Easy Calc Apply");

            try
            {
                switch (targetProperty.propertyType)
                {
                    case SerializedPropertyType.Float:
                        targetProperty.floatValue = Convert.ToSingle(result);
                        break;
                    case SerializedPropertyType.Integer:
                        targetProperty.intValue = Convert.ToInt32(result);
                        break;
                    case SerializedPropertyType.Vector2 when result is Vector2 v2:
                        targetProperty.vector2Value = v2;
                        break;
                    case SerializedPropertyType.Vector3 when result is Vector3 v3:
                        targetProperty.vector3Value = v3;
                        break;
                    case SerializedPropertyType.Vector4 when result is Vector4 v4:
                        targetProperty.vector4Value = v4;
                        break;
                    case SerializedPropertyType.Quaternion when result is Quaternion q:
                        targetProperty.quaternionValue = q;
                        break;
                    case SerializedPropertyType.Color when result is Color c:
                        targetProperty.colorValue = c;
                        break;
                }
            }
            catch { /* mismatch - do nothing */ }

            targetProperty.serializedObject.ApplyModifiedProperties();
            targetProperty = null;
        }

        // ===================================================================
        // History
        // ===================================================================
        private const string PREF_KEY = "EasyCalc_History";

        private void LoadHistory()
        {
            string json = EditorPrefs.GetString(PREF_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { history = JsonUtility.FromJson<HistoryWrapper>(json).items; }
                catch { history.Clear(); }
            }
        }

        private void SaveHistory()
        {
            EditorPrefs.SetString(PREF_KEY, JsonUtility.ToJson(new HistoryWrapper { items = history }));
        }

        [Serializable]
        private class HistoryWrapper { public List<string> items = new(); }

        // ===================================================================
        // GUI
        // ===================================================================
        private void OnGUI()
        {
            GUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope("HelpBox"))
            {
                EditorGUILayout.LabelField("Easy Calculator", EditorStyles.boldLabel);
                GUILayout.Space(4);

                currentMode = (Mode)GUILayout.Toolbar((int)currentMode, new[] { "Simple", "Scientific", "Unity" });

                GUILayout.Space(6);

                if (currentMode == Mode.Scientific)
                {
                    useDegrees = EditorGUILayout.ToggleLeft("Degrees (else Radians)", useDegrees);
                    GUILayout.Space(4);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(110)))
                    {
                        GUILayout.Label("Memory", EditorStyles.miniBoldLabel);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("MC", GUILayout.MinWidth(50))) memory = 0;
                        if (GUILayout.Button("MR", GUILayout.MinWidth(50))) Append(memory.ToString("G6"));
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("M+", GUILayout.MinWidth(50))) MemoryAdd();
                        if (GUILayout.Button("M−", GUILayout.MinWidth(50))) MemorySub();
                        GUILayout.EndHorizontal();
                        GUILayout.Label($" {memory:G}", EditorStyles.miniLabel);
                    }

                    GUILayout.Space(12);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        historyScrollPos = EditorGUILayout.BeginScrollView(historyScrollPos, GUILayout.Height(110));
                        for (int i = history.Count - 1; i >= 0; i--)
                        {
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button(history[i], EditorStyles.label, GUILayout.ExpandWidth(true)))
                            {
                                int eq = history[i].IndexOf(" = ", StringComparison.Ordinal);
                                if (eq > 0)
                                {
                                    input = history[i].Substring(0, eq).Trim();
                                    justCalculated = false;
                                    UpdateLivePreview();
                                }
                            }
                            if (GUILayout.Button("×", GUILayout.Width(22))) { history.RemoveAt(i); Repaint(); }
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndScrollView();
                    }
                }

                GUILayout.Space(10);

                var resStyle = new GUIStyle(EditorStyles.largeLabel)
                {
                    fontSize = 34,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = displayResult == "Error" ? Color.red : GUI.contentColor }
                };
                EditorGUILayout.LabelField(displayResult, resStyle, GUILayout.Height(48));

                if (!string.IsNullOrEmpty(errorMessage))
                    EditorGUILayout.HelpBox(errorMessage, MessageType.Error);

                GUILayout.Space(6);

                var inStyle = new GUIStyle(EditorStyles.textField)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleRight,
                    padding = new RectOffset(10, 10, 8, 8)
                };
                string newInput = EditorGUILayout.TextField(input, inStyle, GUILayout.Height(40));
                if (newInput != input)
                {
                    input = newInput;
                    justCalculated = false;
                    UpdateLivePreview();
                }

                GUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Paste", GUILayout.Height(30))) TrySmartPaste();
                    if (targetProperty != null && GUILayout.Button("Apply to Field", GUILayout.Height(30)))
                        if (displayResult != "Error") ApplyToProperty(EvaluateLastResult());
                }

                GUILayout.Space(8);

                DrawButtonGrid();

                GUILayout.Space(8);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.color = new Color(0.25f, 0.8f, 0.25f);
                    if (GUILayout.Button("=", GUILayout.Height(54), GUILayout.Width(90))) PerformCalculation();
                    GUI.color = Color.white;

                    if (GUILayout.Button("Copy", GUILayout.Height(54))) CopyResult();
                    if (GUILayout.Button("C#", GUILayout.Height(54))) CopyAsCSharp();
                }

                GUILayout.Space(6);
            }

            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter))
            {
                PerformCalculation();
                e.Use();
            }
        }

        private void DrawButtonGrid()
        {
            void Row(params (string label, Action action)[] buttons)
            {
                using (new EditorGUILayout.HorizontalScope())
                    foreach (var (txt, act) in buttons)
                    {
                        if (string.IsNullOrEmpty(txt)) { GUILayout.FlexibleSpace(); continue; }
                        if (GUILayout.Button(txt, GUILayout.MinHeight(48), GUILayout.MinWidth(68))) act?.Invoke();
                    }
            }

            Row(("C", ClearAll), ("⌫", Backspace), ("(", AppendParenOpen), (")", AppendParenClose), (",", AppendComma));

            Row(("7", Append7), ("8", Append8), ("9", Append9), ("/", AppendDiv));

            Row(("4", Append4), ("5", Append5), ("6", Append6), ("*", AppendMul));

            Row(("1", Append1), ("2", Append2), ("3", Append3), ("-", AppendSub));

            Row(("0", Append0), (".", AppendDot), ("π", AppendPi), ("+", AppendAdd));

            if (currentMode == Mode.Scientific)
            {
                Row(("sin", AppendSin), ("cos", AppendCos), ("tan", AppendTan), ("√", AppendSqrt));
                Row(("asin", AppendAsin), ("acos", AppendAcos), ("atan", AppendAtan), ("^", AppendPow));
                Row(("exp", AppendExp), ("log", AppendLog), ("ln", AppendLn), ("abs", AppendAbs));
                Row(("floor", AppendFloor), ("ceil", AppendCeil), ("round", AppendRound), ("", null));
            }
            else if (currentMode == Mode.Unity)
            {
                Row(("vec2(", AppendVec2), ("vec3(", AppendVec3), ("vec4(", AppendVec4), ("color(", AppendColor));
                Row(("quat(", AppendQuat), (".mag", AppendMagnitude), (".norm", AppendNormalized), ("dot(", AppendDotFunc));
                Row(("cross(", AppendCross), ("dist(", AppendDistance), ("lerp(", AppendLerp), ("slerp(", AppendSlerp));
                Row(("angle(", AppendAngle), ("proj(", AppendProject), ("reflect(", AppendReflect), ("euler(", AppendEuler));
            }
        }

        // ===================================================================
        // Input helpers
        // ===================================================================
        private void Append(string s)
        {
            if (justCalculated)
            {
                bool continueOnResult = s.StartsWith(".") || "+-*/^".Contains(s);
                if (!continueOnResult) input = "";
                justCalculated = false;
            }
            input += s;
            UpdateLivePreview();
        }

        private void Append7() => Append("7"); private void Append8() => Append("8"); private void Append9() => Append("9");
        private void Append4() => Append("4"); private void Append5() => Append("5"); private void Append6() => Append("6");
        private void Append1() => Append("1"); private void Append2() => Append("2"); private void Append3() => Append("3");
        private void Append0() => Append("0"); private void AppendDot() => Append("."); private void AppendPi() => Append("pi");
        private void AppendAdd() => Append("+"); private void AppendSub() => Append("-");
        private void AppendMul() => Append("*"); private void AppendDiv() => Append("/");
        private void AppendPow() => Append("^");
        private void AppendParenOpen() => Append("("); private void AppendParenClose() => Append(")"); private void AppendComma() => Append(",");

        private void AppendSin() => Append("sin("); private void AppendCos() => Append("cos("); private void AppendTan() => Append("tan(");
        private void AppendSqrt() => Append("sqrt(");
        private void AppendAsin() => Append("asin("); private void AppendAcos() => Append("acos("); private void AppendAtan() => Append("atan(");
        private void AppendExp() => Append("exp("); private void AppendLog() => Append("log("); private void AppendLn() => Append("ln(");
        private void AppendAbs() => Append("abs(");
        private void AppendFloor() => Append("floor("); private void AppendCeil() => Append("ceil("); private void AppendRound() => Append("round(");

        private void AppendVec2() => Append("vec2("); private void AppendVec3() => Append("vec3("); private void AppendVec4() => Append("vec4(");
        private void AppendColor() => Append("color("); private void AppendQuat() => Append("quat(");
        private void AppendMagnitude() => Append(".magnitude"); private void AppendNormalized() => Append(".normalized");
        private void AppendDotFunc() => Append("dot("); private void AppendCross() => Append("cross(");
        private void AppendDistance() => Append("distance("); private void AppendLerp() => Append("lerp("); private void AppendSlerp() => Append("slerp(");
        private void AppendAngle() => Append("angle("); private void AppendProject() => Append("project("); private void AppendReflect() => Append("reflect(");
        private void AppendEuler() => Append("euler(");

        private void Backspace()
        {
            if (input.Length > 0)
            {
                input = input.Substring(0, input.Length - 1);
                UpdateLivePreview();
            }
        }

        private void ClearAll()
        {
            input = "";
            displayResult = "0";
            errorMessage = "";
            justCalculated = false;
            targetProperty = null;
            UpdateLivePreview();
        }

        private void MemoryAdd()
        {
            if (double.TryParse(displayResult, out double v)) memory += v;
        }

        private void MemorySub()
        {
            if (double.TryParse(displayResult, out double v)) memory -= v;
        }

        // ===================================================================
        // Evaluation
        // ===================================================================
        private void UpdateLivePreview()
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                displayResult = "0";
                errorMessage = "";
                return;
            }

            try
            {
                object result = EvaluateExpression(input);
                displayResult = FormatResult(result);
                errorMessage = "";
            }
            catch (Exception ex)
            {
                displayResult = "Error";
                errorMessage = ex.Message;
            }
        }

        private void PerformCalculation()
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            string original = input.Trim();

            try
            {
                object result = EvaluateExpression(original);
                string resultStr = FormatResult(result);

                displayResult = resultStr;
                errorMessage = "";

                history.Insert(0, $"{original} = {resultStr}");
                if (history.Count > MaxHistory) history.RemoveAt(history.Count - 1);

                justCalculated = true;
                input = resultStr;
            }
            catch (Exception ex)
            {
                displayResult = "Error";
                errorMessage = ex.Message;
            }

            Repaint();
        }

        private object EvaluateLastResult() => EvaluateExpression(input);

        private string Preprocess(string s) => s.ToLowerInvariant()
            .Replace("×", "*").Replace("÷", "/");

        private string FormatResult(object v) => v switch
        {
            double d => d.ToString("G10"),
            Vector2 v2 => $"vec2({v2.x:G}, {v2.y:G})",
            Vector3 v3 => $"vec3({v3.x:G}, {v3.y:G}, {v3.z:G})",
            Vector4 v4 => $"vec4({v4.x:G}, {v4.y:G}, {v4.z:G}, {v4.w:G})",
            Quaternion q => $"quat({q.x:G}, {q.y:G}, {q.z:G}, {q.w:G})",
            Color c => $"color({c.r:G}, {c.g:G}, {c.b:G}, {c.a:G})",
            _ => v?.ToString() ?? "?"
        };

        private object EvaluateExpression(string expression)
        {
            string processed = Preprocess(expression);
            currentTokens = Tokenize(processed);
            currentPos = 0;

            object result = ParseExpression();

            if (currentPos < currentTokens.Count)
                throw new Exception("Unexpected tokens after expression");

            return result;
        }

        // ===================================================================
        // Tokenizer
        // ===================================================================
        private List<string> Tokenize(string expression)
        {
            var tokens = new List<string>();
            int i = 0;

            while (i < expression.Length)
            {
                char c = expression[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                // Number (including scientific notation like 1.23e-4)
                if (char.IsDigit(c) || (c == '.' && i + 1 < expression.Length && char.IsDigit(expression[i + 1])))
                {
                    int start = i;
                    while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.')) i++;
                    if (i < expression.Length && (expression[i] == 'e' || expression[i] == 'E'))
                    {
                        i++;
                        if (i < expression.Length && (expression[i] == '+' || expression[i] == '-')) i++;
                        while (i < expression.Length && char.IsDigit(expression[i])) i++;
                    }
                    tokens.Add(expression.Substring(start, i - start));
                    continue;
                }

                // Operators and single characters
                if ("+-*/^().,".IndexOf(c) >= 0)
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                // Identifiers / functions / constructors (vec2, vec3, vec4, color, quat, sin, dot, etc.)
                if (char.IsLetter(c))
                {
                    int start = i;
                    while (i < expression.Length && char.IsLetter(expression[i])) i++;

                    string word = expression.Substring(start, i - start);

                    // Special case: vec2 / vec3 / vec4 → consume following digit if present
                    if (word == "vec" && i < expression.Length && char.IsDigit(expression[i]))
                    {
                        word += expression[i];
                        i++;
                    }

                    tokens.Add(word.ToLowerInvariant());  // normalize to lower case
                    continue;
                }

                throw new Exception($"Unknown character '{c}' at position {i}");
            }

            return tokens;
        }

        // ===================================================================
        // Recursive Descent Parser
        // ===================================================================
        private object ParseExpression() => ParseAdditive();

        private object ParseAdditive()
        {
            object left = ParseMultiplicative();
            while (currentPos < currentTokens.Count)
            {
                string op = currentTokens[currentPos];
                if (op != "+" && op != "-") break;
                currentPos++;
                object right = ParseMultiplicative();
                left = ApplyBinaryOperator(op, left, right);
            }
            return left;
        }

        private object ParseMultiplicative()
        {
            object left = ParsePower();
            while (currentPos < currentTokens.Count)
            {
                string op = currentTokens[currentPos];
                if (op != "*" && op != "/") break;
                currentPos++;
                object right = ParsePower();
                left = ApplyBinaryOperator(op, left, right);
            }
            return left;
        }

        private object ParsePower()
        {
            object left = ParseUnary();
            if (currentPos < currentTokens.Count && currentTokens[currentPos] == "^")
            {
                currentPos++;
                object right = ParsePower(); // right-associative
                left = ApplyBinaryOperator("^", left, right);
            }
            return left;
        }

        private object ParseUnary()
        {
            if (currentPos < currentTokens.Count && currentTokens[currentPos] == "-")
            {
                currentPos++;
                return ApplyUnaryMinus(ParseUnary());
            }
            if (currentPos < currentTokens.Count && currentTokens[currentPos] == "+")
            {
                currentPos++;
                return ParseUnary();
            }
            return ParsePrimary();
        }

        private object ParsePrimary()
        {
            if (currentPos >= currentTokens.Count) throw new Exception("Unexpected end of expression");

            string token = currentTokens[currentPos];
            object value;

            // Number
            if (double.TryParse(token, out double num))
            {
                currentPos++;
                value = num;
            }
            // pi
            else if (token == "pi")
            {
                currentPos++;
                value = Math.PI;
            }
            // Parentheses
            else if (token == "(")
            {
                currentPos++;
                value = ParseExpression();
                Expect(")");
            }
            // Functions / constructors / vector methods / euler
            else if (IsFunction(token) || IsConstructor(token) || IsVectorMethod(token) || token == "euler")
            {
                string func = currentTokens[currentPos++];
                Expect("(");
                var args = ParseArguments();
                Expect(")");

                if (IsConstructor(func))
                    value = ApplyConstructor(func, args);
                else if (func == "euler")
                    value = ApplyEuler(args);
                else if (IsFunction(func))
                    value = ApplyMathFunction(func, args);
                else if (IsVectorMethod(func))
                    value = ApplyVectorFunction(func, args);
                else
                    throw new Exception($"Unknown function: {func}");
            }
            else
                throw new Exception($"Unexpected token: {token}");

            // Postfix properties (.magnitude, .normalized, .x, .y, ...)
            while (currentPos < currentTokens.Count && currentTokens[currentPos] == ".")
            {
                currentPos++;
                if (currentPos >= currentTokens.Count || !char.IsLetter(currentTokens[currentPos][0]))
                    throw new Exception("Expected property name after '.'");

                string prop = currentTokens[currentPos++].ToLowerInvariant();
                value = ApplyPostfixProperty(value, prop);
            }

            return value;
        }

        private List<object> ParseArguments()
        {
            var args = new List<object>();
            if (currentPos < currentTokens.Count && currentTokens[currentPos] != ")")
            {
                args.Add(ParseExpression());
                while (currentPos < currentTokens.Count && currentTokens[currentPos] == ",")
                {
                    currentPos++;
                    args.Add(ParseExpression());
                }
            }
            return args;
        }

        private void Expect(string expected)
        {
            if (currentPos >= currentTokens.Count || currentTokens[currentPos] != expected)
                throw new Exception($"Expected '{expected}', got {(currentPos < currentTokens.Count ? currentTokens[currentPos] : "end of input")}");
            currentPos++;
        }

        // ===================================================================
        // Type checks
        // ===================================================================
        private static bool IsFunction(string s) =>
            s is "sin" or "cos" or "tan" or "asin" or "acos" or "atan" or
            "sqrt" or "exp" or "log" or "ln" or "abs" or "floor" or "ceil" or "round";

        private static bool IsConstructor(string s) =>
            s is "vec2" or "vec3" or "vec4" or "color" or "quat";

        private static bool IsVectorMethod(string s) =>
            s is "dot" or "cross" or "distance" or "lerp" or "slerp" or "angle" or "project" or "reflect";

        // ===================================================================
        // Apply functions
        // ===================================================================
        private static double ToRadians(double deg) => deg * Math.PI / 180.0;
        private static double ToDegrees(double rad) => rad * 180.0 / Math.PI;

        private double GetDouble(object o) => o is double d ? d : throw new Exception("Expected a number");

        private object ApplyMathFunction(string f, List<object> args)
        {
            if (args.Count != 1) throw new Exception($"{f} takes 1 argument");
            double arg = GetDouble(args[0]);

            bool degMode = currentMode == Mode.Scientific && useDegrees;
            if (degMode && (f == "sin" || f == "cos" || f == "tan")) arg = ToRadians(arg);

            return f switch
            {
                "sin" => Math.Sin(arg),
                "cos" => Math.Cos(arg),
                "tan" => Math.Tan(arg),
                "asin" => degMode ? ToDegrees(Math.Asin(arg)) : Math.Asin(arg),
                "acos" => degMode ? ToDegrees(Math.Acos(arg)) : Math.Acos(arg),
                "atan" => degMode ? ToDegrees(Math.Atan(arg)) : Math.Atan(arg),
                "sqrt" => Math.Sqrt(arg),
                "exp" => Math.Exp(arg),
                "log" => Math.Log10(arg),
                "ln" => Math.Log(arg),
                "abs" => Math.Abs(arg),
                "floor" => Math.Floor(arg),
                "ceil" => Math.Ceiling(arg),
                "round" => Math.Round(arg),
                _ => throw new Exception($"Unknown math function {f}")
            };
        }

        private object ApplyConstructor(string ctor, List<object> args)
        {
            return ctor switch
            {
                "vec2" => args.Count == 2 ? new Vector2((float)GetDouble(args[0]), (float)GetDouble(args[1])) : throw new Exception("vec2 needs 2 args"),
                "vec3" => args.Count == 3 ? new Vector3((float)GetDouble(args[0]), (float)GetDouble(args[1]), (float)GetDouble(args[2])) : throw new Exception("vec3 needs 3 args"),
                "vec4" => args.Count == 4 ? new Vector4((float)GetDouble(args[0]), (float)GetDouble(args[1]), (float)GetDouble(args[2]), (float)GetDouble(args[3])) : throw new Exception("vec4 needs 4 args"),
                "color" => args.Count == 4 ? new Color((float)GetDouble(args[0]), (float)GetDouble(args[1]), (float)GetDouble(args[2]), (float)GetDouble(args[3])) : throw new Exception("color needs 4 args"),
                "quat" => args.Count == 4 ? new Quaternion((float)GetDouble(args[0]), (float)GetDouble(args[1]), (float)GetDouble(args[2]), (float)GetDouble(args[3])) : throw new Exception("quat needs 4 args"),
                _ => throw new Exception($"Unknown constructor {ctor}")
            };
        }

        private object ApplyEuler(List<object> args)
        {
            if (args.Count != 3) throw new Exception("euler needs 3 arguments (degrees)");
            return Quaternion.Euler((float)GetDouble(args[0]), (float)GetDouble(args[1]), (float)GetDouble(args[2]));
        }

        private object ApplyVectorFunction(string func, List<object> args)
        {
            switch (func)
            {
                case "dot": return args.Count == 2 ? VectorDot(args[0], args[1]) : throw new Exception("dot needs 2 vectors");
                case "cross": return args.Count == 2 ? Vector3.Cross(ToVector3(args[0]), ToVector3(args[1])) : throw new Exception("cross needs 2 Vector3");
                case "distance": return args.Count == 2 ? VectorDistance(args[0], args[1]) : throw new Exception("distance needs 2 vectors");
                case "lerp": return args.Count == 3 ? VectorLerp(args[0], args[1], (float)GetDouble(args[2])) : throw new Exception("lerp needs 3 args");
                case "slerp": return args.Count == 3 ? Quaternion.Slerp(ToQuaternion(args[0]), ToQuaternion(args[1]), (float)GetDouble(args[2])) : throw new Exception("slerp needs 3 args");
                case "angle": return args.Count == 2 ? VectorAngle(args[0], args[1]) : throw new Exception("angle needs 2 vectors");
                case "project": return args.Count == 2 ? Vector3.Project(ToVector3(args[0]), ToVector3(args[1])) : throw new Exception("project needs 2 Vector3");
                case "reflect": return args.Count == 2 ? VectorReflect(args[0], args[1]) : throw new Exception("reflect needs 2 vectors");
                default: throw new Exception($"Unknown vector function {func}");
            }
        }

        private object ApplyPostfixProperty(object value, string prop)
        {
            return prop switch
            {
                "magnitude" => value switch
                {
                    Vector2 v => v.magnitude,
                    Vector3 v => v.magnitude,
                    Vector4 v => v.magnitude,
                    _ => throw new Exception(".magnitude only on vectors")
                },
                "normalized" => value switch
                {
                    Vector2 v => v.normalized,
                    Vector3 v => v.normalized,
                    Vector4 v => v.normalized,
                    _ => throw new Exception(".normalized only on vectors")
                },
                _ when prop.Length == 1 => GetComponent(value, prop[0]),
                _ => throw new Exception($"Unknown property .{prop}")
            };
        }

        private object GetComponent(object obj, char c)
        {
            return obj switch
            {
                Vector2 v2 => c switch { 'x' or 'r' => v2.x, 'y' or 'g' => v2.y, _ => throw new Exception("Invalid component") },
                Vector3 v3 => c switch { 'x' or 'r' => v3.x, 'y' or 'g' => v3.y, 'z' or 'b' => v3.z, _ => throw new Exception("Invalid component") },
                Vector4 v4 => c switch { 'x' or 'r' => v4.x, 'y' or 'g' => v4.y, 'z' or 'b' => v4.z, 'w' or 'a' => v4.w, _ => throw new Exception("Invalid component") },
                Color col => c switch { 'r' => col.r, 'g' => col.g, 'b' => col.b, 'a' => col.a, _ => throw new Exception("Invalid component") },
                Quaternion q => c switch { 'x' => q.x, 'y' => q.y, 'z' => q.z, 'w' => q.w, _ => throw new Exception("Invalid component") },
                _ => throw new Exception("Component access not supported on this type")
            };
        }

        private object ApplyUnaryMinus(object o)
        {
            return o switch
            {
                double d => -d,
                Vector2 v => -v,
                Vector3 v => -v,
                Vector4 v => -v,
                Color c => new Color(-c.r, -c.g, -c.b, -c.a),
                Quaternion q => new Quaternion(-q.x, -q.y, -q.z, -q.w),
                _ => throw new Exception("Unary minus not supported on this type")
            };
        }

        private object ApplyBinaryOperator(string op, object a, object b)
        {
            // Both numbers
            if (a is double da && b is double db)
            {
                return op switch
                {
                    "+" => da + db,
                    "-" => da - db,
                    "*" => da * db,
                    "/" => db != 0 ? da / db : throw new Exception("Division by zero"),
                    "^" => Math.Pow(da, db),
                    _ => throw new Exception($"Unknown operator {op}")
                };
            }

            // Vector / Color / Quaternion operators
            if (op == "+" || op == "-")
            {
                if (a.GetType() == b.GetType())
                {
                    if (a is Vector2) return op == "+" ? (Vector2)a + (Vector2)b : (Vector2)a - (Vector2)b;
                    if (a is Vector3) return op == "+" ? (Vector3)a + (Vector3)b : (Vector3)a - (Vector3)b;
                    if (a is Vector4) return op == "+" ? (Vector4)a + (Vector4)b : (Vector4)a - (Vector4)b;
                    if (a is Color) return op == "+" ? (Color)a + (Color)b : (Color)a - (Color)b;
                }
            }

            if (op == "*" || op == "/")
            {
                // component-wise vector * vector   (or /)
                if (a.GetType() == b.GetType())
                {
                    if (a is Vector2 va2 && b is Vector2 vb2)
                    {
                        return op == "*" 
                            ? new Vector2(va2.x * vb2.x, va2.y * vb2.y)
                            : new Vector2(va2.x / vb2.x, va2.y / vb2.y);
                    }

                    if (a is Vector3 va3 && b is Vector3 vb3)
                    {
                        return op == "*" 
                            ? new Vector3(va3.x * vb3.x, va3.y * vb3.y, va3.z * vb3.z)
                            : new Vector3(va3.x / vb3.x, va3.y / vb3.y, va3.z / vb3.z);
                    }

                    if (a is Vector4 va4 && b is Vector4 vb4)
                    {
                        return op == "*" 
                            ? new Vector4(va4.x * vb4.x, va4.y * vb4.y, va4.z * vb4.z, va4.w * vb4.w)
                            : new Vector4(va4.x / vb4.x, va4.y / vb4.y, va4.z / vb4.z, va4.w / vb4.w);
                    }

                    if (a is Color ca1 && b is Color cb1)
                    {
                        return op == "*" 
                            ? new Color(ca1.r * cb1.r, ca1.g * cb1.g, ca1.b * cb1.b, ca1.a * cb1.a)
                            : new Color(ca1.r / cb1.r, ca1.g / cb1.g, ca1.b / cb1.b, ca1.a / cb1.a);
                    }
                }

                // scalar * vector
                if (op == "*" && a is double sa && IsVector(b)) return MultiplyVector(b, (float)sa);
                if (op == "*" && b is double sb && IsVector(a)) return MultiplyVector(a, (float)sb);
                if (op == "/" && IsVector(a) && b is double db2) return DivideVector(a, (float)db2);

                // Color * scalar
                if (op == "*" && a is Color ca && b is double db3) return ca * (float)db3;
                if (op == "*" && b is Color cb && a is double da2) return cb * (float)da2;
                if (op == "/" && a is Color ca2 && b is double db4) return ca2 / (float)db4;
            }

            // Quaternion multiplication
            if (op == "*" && a is Quaternion qa)
            {
                if (b is Quaternion qb) return qa * qb;
                if (b is Vector3 vb) return qa * vb;
            }

            throw new Exception($"Cannot apply '{op}' between {a?.GetType().Name} and {b?.GetType().Name}");
        }

        private static bool IsVector(object o) => o is Vector2 or Vector3 or Vector4;

        private static object MultiplyVector(object v, float s) => v switch
        {
            Vector2 v2 => v2 * s,
            Vector3 v3 => v3 * s,
            Vector4 v4 => v4 * s,
            _ => throw new Exception("MultiplyVector only on vectors")
        };

        private static object DivideVector(object v, float s) => v switch
        {
            Vector2 v2 => v2 / s,
            Vector3 v3 => v3 / s,
            Vector4 v4 => v4 / s,
            _ => throw new Exception("DivideVector only on vectors")
        };

        // Helper conversions for vector functions
        private static double VectorDot(object a, object b)
        {
            if (a is Vector2 va2 && b is Vector2 vb2) return Vector2.Dot(va2, vb2);
            if (a is Vector3 va3 && b is Vector3 vb3) return Vector3.Dot(va3, vb3);
            if (a is Vector4 va4 && b is Vector4 vb4) return Vector4.Dot(va4, vb4);
            throw new Exception("dot requires matching Vector2/3/4");
        }

        private static double VectorDistance(object a, object b)
        {
            if (a is Vector2 && b is Vector2) return Vector2.Distance((Vector2)a, (Vector2)b);
            if (a is Vector3 && b is Vector3) return Vector3.Distance((Vector3)a, (Vector3)b);
            if (a is Vector4 && b is Vector4) return Vector4.Distance((Vector4)a, (Vector4)b);
            throw new Exception("distance requires matching Vector2/3/4");
        }

        private static float VectorAngle(object a, object b)
        {
            if (a is Vector2 && b is Vector2) return Vector2.Angle((Vector2)a, (Vector2)b);
            if (a is Vector3 && b is Vector3) return Vector3.Angle((Vector3)a, (Vector3)b);
            throw new Exception("angle requires Vector2 or Vector3");
        }

        private static object VectorLerp(object a, object b, float t)
        {
            if (a is Vector2 && b is Vector2) return Vector2.Lerp((Vector2)a, (Vector2)b, t);
            if (a is Vector3 && b is Vector3) return Vector3.Lerp((Vector3)a, (Vector3)b, t);
            if (a is Vector4 && b is Vector4) return Vector4.Lerp((Vector4)a, (Vector4)b, t);
            throw new Exception("lerp requires matching vectors");
        }

        private static object VectorReflect(object a, object b)
        {
            if (a is Vector2 && b is Vector2) return Vector2.Reflect((Vector2)a, (Vector2)b);
            if (a is Vector3 && b is Vector3) return Vector3.Reflect((Vector3)a, (Vector3)b);
            throw new Exception("reflect requires Vector2 or Vector3");
        }

        private static Vector3 ToVector3(object o) => o is Vector3 v ? v : throw new Exception("Expected Vector3");
        private static Quaternion ToQuaternion(object o) => o is Quaternion q ? q : throw new Exception("Expected Quaternion");

        // ===================================================================
        // Copy / Paste
        // ===================================================================
        private void TrySmartPaste()
        {
            string text = EditorGUIUtility.systemCopyBuffer?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                input = text;
                justCalculated = false;
                UpdateLivePreview();
            }
        }

        private void CopyResult()
        {
            if (displayResult != "Error")
                EditorGUIUtility.systemCopyBuffer = displayResult;
        }

        private void CopyAsCSharp()
        {
            if (displayResult == "Error") return;

            string code = displayResult.Contains("vec") || displayResult.Contains("color") || displayResult.Contains("quat")
                ? $"var result = {displayResult};"
                : $"const float result = {displayResult}f;";

            EditorGUIUtility.systemCopyBuffer = code;
        }
    }
}