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

        private Vector2 historyScroll;
        private List<string> history = new List<string>();
        private const int MaxHistory = 150;

        private bool justCalculated;
        private bool useDegrees = true;
        private double memory = 0;

        private SerializedProperty targetProp;

        private enum CalcMode { Simple, Scientific, Unity }
        private CalcMode mode = CalcMode.Simple;

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

        private void OnContextMenu(GenericMenu menu, SerializedProperty prop)
        {
            if (!IsSupportedType(prop.propertyType)) return;
            menu.AddItem(new GUIContent("Calculate with Easy Calc"), false, () =>
            {
                Show();
                input = PropToExpr(prop);
                targetProp = prop.Copy();
                justCalculated = false;
                mode = CalcMode.Unity;
                UpdatePreview();
            });
        }

        private static bool IsSupportedType(SerializedPropertyType t) =>
            t is SerializedPropertyType.Float or SerializedPropertyType.Integer or
            SerializedPropertyType.Vector2 or SerializedPropertyType.Vector3 or
            SerializedPropertyType.Vector4 or SerializedPropertyType.Quaternion or
            SerializedPropertyType.Color;

        private string PropToExpr(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Float      => p.floatValue.ToString("G"),
            SerializedPropertyType.Integer    => p.intValue.ToString(),
            SerializedPropertyType.Vector2    => $"vec2({p.vector2Value.x:G},{p.vector2Value.y:G})",
            SerializedPropertyType.Vector3    => $"vec3({p.vector3Value.x:G},{p.vector3Value.y:G},{p.vector3Value.z:G})",
            SerializedPropertyType.Vector4    => $"vec4({p.vector4Value.x:G},{p.vector4Value.y:G},{p.vector4Value.z:G},{p.vector4Value.w:G})",
            SerializedPropertyType.Quaternion => $"quat({p.quaternionValue.x:G},{p.quaternionValue.y:G},{p.quaternionValue.z:G},{p.quaternionValue.w:G})",
            SerializedPropertyType.Color      => $"color({p.colorValue.r:G},{p.colorValue.g:G},{p.colorValue.b:G},{p.colorValue.a:G})",
            _ => ""
        };

        private void ApplyResult(object value)
        {
            if (targetProp == null) return;
            Undo.RecordObject(targetProp.serializedObject.targetObject, "Easy Calc Apply");

            switch (targetProp.propertyType)
            {
                case SerializedPropertyType.Float:      targetProp.floatValue     = Convert.ToSingle(value); break;
                case SerializedPropertyType.Integer:    targetProp.intValue       = Convert.ToInt32(value);   break;
                case SerializedPropertyType.Vector2    when value is Vector2 v:    targetProp.vector2Value   = v; break;
                case SerializedPropertyType.Vector3    when value is Vector3 v:    targetProp.vector3Value   = v; break;
                case SerializedPropertyType.Vector4    when value is Vector4 v:    targetProp.vector4Value   = v; break;
                case SerializedPropertyType.Quaternion when value is Quaternion q: targetProp.quaternionValue = q;  break;
                case SerializedPropertyType.Color      when value is Color c:      targetProp.colorValue     = c;   break;
            }
            targetProp.serializedObject.ApplyModifiedProperties();
            targetProp = null;
        }

        // History persistence
        private const string PREF_KEY = "EasyCalc_History_v3";

        private void LoadHistory()
        {
            string json = EditorPrefs.GetString(PREF_KEY, "");
            if (!string.IsNullOrEmpty(json))
                try { history = JsonUtility.FromJson<HistWrap>(json).entries ?? new List<string>(); }
                catch { history = new List<string>(); }
        }

        private void SaveHistory()
        {
            EditorPrefs.SetString(PREF_KEY, JsonUtility.ToJson(new HistWrap { entries = history }));
        }

        [Serializable]
        private class HistWrap { public List<string> entries = new(); }

        private void OnGUI()
        {
            GUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope("HelpBox"))
            {
                EditorGUILayout.LabelField("Easy Calculator", EditorStyles.boldLabel);
                GUILayout.Space(4);

                mode = (CalcMode)GUILayout.Toolbar((int)mode, new[] { "Simple", "Scientific", "Unity" });

                GUILayout.Space(6);

                if (mode == CalcMode.Scientific)
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
                        historyScroll = EditorGUILayout.BeginScrollView(historyScroll, GUILayout.Height(110));
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
                                    UpdatePreview();
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
                string ni = EditorGUILayout.TextField(input, inStyle, GUILayout.Height(40));
                if (ni != input)
                {
                    input = ni;
                    justCalculated = false;
                    UpdatePreview();
                }

                GUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Paste", GUILayout.Height(30))) TryPaste();
                    if (targetProp != null && GUILayout.Button("Apply to Field", GUILayout.Height(30)))
                        if (displayResult != "Error") ApplyResult(EvalLast());
                }

                GUILayout.Space(8);

                DrawModeGrid();

                GUILayout.Space(8);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.color = new Color(0.25f, 0.8f, 0.25f);
                    if (GUILayout.Button("=", GUILayout.Height(54), GUILayout.Width(90))) Calculate();
                    GUI.color = Color.white;

                    if (GUILayout.Button("Copy", GUILayout.Height(54))) CopyResult();
                    if (GUILayout.Button("C#", GUILayout.Height(54))) CopyCSharp();
                }

                GUILayout.Space(6);
            }

            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter))
            {
                Calculate();
                e.Use();
            }
        }

        private void DrawModeGrid()
        {
            void Row(params (string lbl, Action act)[] buttons)
            {
                using (new EditorGUILayout.HorizontalScope())
                    foreach (var (txt, act) in buttons)
                    {
                        if (string.IsNullOrEmpty(txt)) { GUILayout.FlexibleSpace(); continue; }
                        if (GUILayout.Button(txt, GUILayout.MinHeight(48), GUILayout.MinWidth(68))) act?.Invoke();
                    }
            }

            Row(("C", ClearAll), ("⌫", Backspace), ("(", () => Append("(")), (")", () => Append(")")), (",", () => Append(",")));

            Row(("7", () => Append("7")), ("8", () => Append("8")), ("9", () => Append("9")), ("/", () => Append("/")));
            Row(("4", () => Append("4")), ("5", () => Append("5")), ("6", () => Append("6")), ("*", () => Append("*")));
            Row(("1", () => Append("1")), ("2", () => Append("2")), ("3", () => Append("3")), ("-", () => Append("-")));
            Row(("0", () => Append("0")), (".", () => Append(".")), ("π", () => Append("pi")), ("+", () => Append("+")));

            if (mode == CalcMode.Scientific)
            {
                Row(("sin", () => Append("sin(")), ("cos", () => Append("cos(")), ("tan", () => Append("tan(")), ("√", () => Append("sqrt(")));
                Row(("asin", () => Append("asin(")), ("acos", () => Append("acos(")), ("atan", () => Append("atan(")), ("^", () => Append("^")));
                Row(("exp", () => Append("exp(")), ("log", () => Append("log(")), ("abs", () => Append("abs(")), ("", null));
            }
            else if (mode == CalcMode.Unity)
            {
                Row(("vec2(", () => Append("vec2(")), ("vec3(", () => Append("vec3(")), ("vec4(", () => Append("vec4(")), ("color(", () => Append("color(")));
                Row(("quat(", () => Append("quat(")), ("mag", () => Append(".magnitude")), ("norm", () => Append(".normalized")), ("dot(", () => Append("dot(")));
                Row(("cross(", () => Append("cross(")), ("dist(", () => Append("distance(")), ("lerp(", () => Append("lerp(")), ("", null));
            }
        }

        // ── Input helpers ──────────────────────────────────────────────────────
        private void Append(string s)
        {
            if (justCalculated) { input = ""; justCalculated = false; }
            input += s;
            UpdatePreview();
        }

        private void Backspace()
        {
            if (input.Length > 0) { input = input[..^1]; UpdatePreview(); }
        }

        private void ClearAll()
        {
            input = displayResult = "0"; errorMessage = ""; justCalculated = false; targetProp = null;
            UpdatePreview();
        }

        private void MemoryAdd()  { if (double.TryParse(displayResult, out double v)) memory += v; }
        private void MemorySub()  { if (double.TryParse(displayResult, out double v)) memory -= v; }

        private void UpdatePreview()
        {
            if (string.IsNullOrWhiteSpace(input)) { displayResult = "0"; errorMessage = ""; return; }

            try
            {
                string expr = Preprocess(input);
                object res = Evaluate(expr);
                displayResult = FormatResult(res);
                errorMessage = "";
            }
            catch (Exception ex)
            {
                displayResult = "Error";
                errorMessage = ex.Message;
            }
        }

        private void Calculate()
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            string orig = input.Trim();

            try
            {
                string expr = Preprocess(orig);
                object res = Evaluate(expr);
                string txt = FormatResult(res);

                displayResult = txt;
                errorMessage = "";
                history.Insert(0, $"{orig} = {txt}");
                if (history.Count > MaxHistory) history.RemoveAt(history.Count - 1);

                justCalculated = true;
                input = txt;
            }
            catch (Exception ex)
            {
                displayResult = "Error";
                errorMessage = ex.Message;
            }

            Repaint();
        }

        private object EvalLast() => Evaluate(Preprocess(input));

        private string Preprocess(string s) => s.ToLowerInvariant()
            .Replace("pi", Math.PI.ToString("G15"))
            .Replace("×", "*").Replace("÷", "/");

        private string FormatResult(object v) => v switch
        {
            double d     => d.ToString("G10"),
            Vector2 v2   => $"vec2({v2.x:G}, {v2.y:G})",
            Vector3 v3   => $"vec3({v3.x:G}, {v3.y:G}, {v3.z:G})",
            Vector4 v4   => $"vec4({v4.x:G}, {v4.y:G}, {v4.z:G}, {v4.w:G})",
            Quaternion q => $"quat({q.x:G}, {q.y:G}, {q.z:G}, {q.w:G})",
            Color c      => $"color({c.r:G}, {c.g:G}, {c.b:G}, {c.a:G})",
            _            => v?.ToString() ?? "?"
        };

        // ── Evaluation ─────────────────────────────────────────────────────────
        private object Evaluate(string expr)
        {
            var tokens = Tokenize(expr);
            var output = new Queue<string>();
            var ops = new Stack<string>();

            foreach (var tok in tokens)
            {
                if (double.TryParse(tok, out _))
                    output.Enqueue(tok);
                else if (tok == "(")
                    ops.Push(tok);
                else if (tok == ")")
                {
                    while (ops.Count > 0 && ops.Peek() != "(") output.Enqueue(ops.Pop());
                    ops.Pop();
                    if (ops.Count > 0 && IsFunction(ops.Peek())) output.Enqueue(ops.Pop());
                }
                else if (IsFunction(tok) || IsConstructor(tok) || IsVectorMethod(tok))
                    ops.Push(tok);
                else
                {
                    while (ops.Count > 0 && Precedence(ops.Peek()) >= Precedence(tok) && ops.Peek() != "(")
                        output.Enqueue(ops.Pop());
                    ops.Push(tok);
                }
            }

            while (ops.Count > 0) output.Enqueue(ops.Pop());

            return EvalRPN(output);
        }

        private static List<string> Tokenize(string s)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (char.IsDigit(c) || c == '.' || (c == '-' && (i == 0 || s[i-1] == '(' || char.IsLetter(s[i-1]))))
                {
                    string num = "";
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '-' || s[i] == '+'))
                        num += s[i++];
                    tokens.Add(num);
                    continue;
                }

                if ("+-*/^().,".Contains(c))
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                if (char.IsLetter(c))
                {
                    string id = "";
                    while (i < s.Length && char.IsLetter(s[i])) id += s[i++];
                    tokens.Add(id.ToLowerInvariant());
                    continue;
                }

                throw new Exception($"Unknown character '{c}' at {i}");
            }
            return tokens;
        }

        private object EvalRPN(Queue<string> queue)
        {
            var stack = new Stack<object>();

            while (queue.Count > 0)
            {
                string tok = queue.Dequeue();

                if (double.TryParse(tok, out double num))
                {
                    stack.Push(num);
                    continue;
                }

                if (tok == ".")
                {
                    string next = queue.Dequeue(); // magnitude, normalized, x, y, z, w, r, g, b, a

                    object target = stack.Pop();

                    if (next == "magnitude" || next == "normalized")
                    {
                        stack.Push(ApplyVectorProperty(target, next));
                    }
                    else if (next.Length == 1)
                    {
                        stack.Push(GetComponent(target, next));
                    }
                    else
                    {
                        throw new Exception($"Unknown property after dot: .{next}");
                    }
                    continue;
                }

                if (IsFunction(tok))
                {
                    stack.Push(ApplyMathFunc(tok, stack));
                    continue;
                }

                if (IsConstructor(tok))
                {
                    stack.Push(ApplyConstructor(tok, stack));
                    continue;
                }

                if (IsVectorMethod(tok))
                {
                    stack.Push(ApplyVectorMethod(tok, stack));
                    continue;
                }

                object right = stack.Pop();
                object left = stack.Pop();
                stack.Push(ApplyOperator(tok, left, right));
            }

            if (stack.Count != 1)
                throw new Exception("Invalid expression - multiple values left on stack");

            return stack.Pop();
        }

        private static bool IsFunction(string s) =>
            s is "sin" or "cos" or "tan" or "asin" or "acos" or "atan" or
            "sqrt" or "exp" or "log" or "abs";

        private static bool IsConstructor(string s) =>
            s is "vec2" or "vec3" or "vec4" or "color" or "quat";

        private static bool IsVectorMethod(string s) =>
            s is "dot" or "cross" or "distance" or "lerp";

        private object ApplyMathFunc(string f, Stack<object> stack)
        {
            double x = Convert.ToDouble(stack.Pop());

            if (useDegrees && f is "sin" or "cos" or "tan")
                x *= Math.PI / 180.0;
            if (useDegrees && f is "asin" or "acos" or "atan")
                x *= 180.0 / Math.PI;

            return f switch
            {
                "sin"  => Math.Sin(x),
                "cos"  => Math.Cos(x),
                "tan"  => Math.Tan(x),
                "asin" => Math.Asin(x),
                "acos" => Math.Acos(x),
                "atan" => Math.Atan(x),
                "sqrt" => Math.Sqrt(x),
                "exp"  => Math.Exp(x),
                "log"  => Math.Log10(x),
                "abs"  => Math.Abs(x),
                _      => throw new Exception($"Unknown function: {f}")
            };
        }

        private object ApplyConstructor(string name, Stack<object> stack)
        {
            double w = (name == "vec4" || name == "color" || name == "quat") ? Convert.ToDouble(stack.Pop()) : 1f;
            double z = (name == "vec3" || name == "vec4" || name == "color" || name == "quat") ? Convert.ToDouble(stack.Pop()) : 0f;
            double y = (name != "vec2") ? Convert.ToDouble(stack.Pop()) : 0f;
            double x = Convert.ToDouble(stack.Pop());

            return name switch
            {
                "vec2"  => new Vector2((float)x, (float)y),
                "vec3"  => new Vector3((float)x, (float)y, (float)z),
                "vec4"  => new Vector4((float)x, (float)y, (float)z, (float)w),
                "color" => new Color((float)x, (float)y, (float)z, (float)w),
                "quat"  => new Quaternion((float)x, (float)y, (float)z, (float)w),
                _       => throw new Exception($"Unknown constructor: {name}")
            };
        }

        private object ApplyVectorProperty(object target, string prop)
        {
            return prop switch
            {
                "magnitude" => target switch
                {
                    Vector2 v2 => v2.magnitude,
                    Vector3 v3 => v3.magnitude,
                    Vector4 v4 => v4.magnitude,
                    _ => throw new Exception("magnitude only supported on Vector2/3/4")
                },

                "normalized" => target switch
                {
                    Vector2 v2 => v2.normalized,
                    Vector3 v3 => v3.normalized,
                    Vector4 v4 => v4.normalized,
                    _ => throw new Exception("normalized only supported on Vector2/3/4")
                },

                _ => throw new Exception($"Unknown vector property: {prop}")
            };
        }

        private object ApplyVectorMethod(string name, Stack<object> stack)
        {
            switch (name)
            {
                case "dot":
                    var bDot = stack.Pop();
                    var aDot = stack.Pop();
                    return Vector3.Dot((Vector3)aDot, (Vector3)bDot);

                case "cross":
                    var bCross = stack.Pop();
                    var aCross = stack.Pop();
                    return Vector3.Cross((Vector3)aCross, (Vector3)bCross);

                case "distance":
                    var bDist = stack.Pop();
                    var aDist = stack.Pop();
                    return Vector3.Distance((Vector3)aDist, (Vector3)bDist);

                case "lerp":
                    double t = Convert.ToDouble(stack.Pop());
                    var bLerp = stack.Pop();
                    var aLerp = stack.Pop();
                    return Vector3.Lerp((Vector3)aLerp, (Vector3)bLerp, (float)t);

                default:
                    throw new Exception($"Unknown vector method: {name}");
            }
        }

        private object GetComponent(object obj, string member)
        {
            if (member.Length != 1) throw new Exception("Component must be single letter");

            char c = member[0];

            return obj switch
            {
                Vector2 v2 => c switch { 'x' or 'r' => v2.x, 'y' or 'g' => v2.y, _ => throw new Exception("Invalid component") },
                Vector3 v3 => c switch { 'x' or 'r' => v3.x, 'y' or 'g' => v3.y, 'z' or 'b' => v3.z, _ => throw new Exception("Invalid component") },
                Vector4 v4 => c switch { 'x' or 'r' => v4.x, 'y' or 'g' => v4.y, 'z' or 'b' => v4.z, 'w' or 'a' => v4.w, _ => throw new Exception("Invalid component") },
                Color col  => c switch { 'r' => col.r, 'g' => col.g, 'b' => col.b, 'a' => col.a, _ => throw new Exception("Invalid component") },
                Quaternion q => c switch { 'x' => q.x, 'y' => q.y, 'z' => q.z, 'w' => q.w, _ => throw new Exception("Invalid component") },
                _ => throw new Exception("Component access not supported on this type")
            };
        }

        private static object ApplyOperator(string op, object left, object right)
        {
            if (left is double dl && right is double dr)
            {
                return op switch
                {
                    "+" => dl + dr,
                    "-" => dl - dr,
                    "*" => dl * dr,
                    "/" => dr != 0 ? dl / dr : throw new Exception("Division by zero"),
                    "^" => Math.Pow(dl, dr),
                    _   => throw new Exception($"Unknown operator {op}")
                };
            }

            if (op == "*" && ((left is double sl && IsVector(right)) || (right is double sr && IsVector(left))))
            {
                double scalar = left is double ? (double)left : (double)right;
                object vec = left is double ? right : left;
                return MultiplyVector(vec, (float)scalar);
            }

            if (op == "/" && IsVector(left) && right is double sr1)
            {
                if (sr1 == 0) throw new Exception("Division by zero");
                return DivideVector(left, (float)sr1);
            }

            throw new Exception($"Cannot apply {op} between {left?.GetType()?.Name} and {right?.GetType()?.Name}");
        }

        private static bool IsVector(object o) => o is Vector2 || o is Vector3 || o is Vector4;

        private static object MultiplyVector(object v, float s)
        {
            return v switch
            {
                Vector2 v2 => v2 * s,
                Vector3 v3 => v3 * s,
                Vector4 v4 => v4 * s,
                _ => throw new Exception("Multiply only on vectors")
            };
        }

        private static object DivideVector(object v, float s)
        {
            return v switch
            {
                Vector2 v2 => v2 / s,
                Vector3 v3 => v3 / s,
                Vector4 v4 => v4 / s,
                _ => throw new Exception("Divide only on vectors")
            };
        }

        private static int Precedence(string op) => op switch
        {
            "+" or "-" => 1,
            "*" or "/" => 2,
            "^"        => 3,
            _          => 0
        };

        private void TryPaste()
        {
            string clip = EditorGUIUtility.systemCopyBuffer?.Trim();
            if (string.IsNullOrEmpty(clip)) return;
            input = clip;
            justCalculated = false;
            UpdatePreview();
        }

        private void CopyResult() => EditorGUIUtility.systemCopyBuffer = displayResult != "Error" ? displayResult : "";

        private void CopyCSharp()
        {
            if (displayResult == "Error") return;
            string code = displayResult.Contains("vec") || displayResult.Contains("color") || displayResult.Contains("quat")
                ? $"var result = {displayResult};"
                : $"const float result = {displayResult}f;";
            EditorGUIUtility.systemCopyBuffer = code;
        }
    }
}