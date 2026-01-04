using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class EasyCalculatorEditor : EditorWindow
{
    private string input = "";
    private string displayResult = "0";
    private Vector2 historyScrollPos;
    private List<string> history = new List<string>();
    private const int MaxHistory = 100;
    private bool justCalculated = false;

    [MenuItem("Window/Tools/Easy Calculator %#c")] // Ctrl+Shift+C
    public static void ShowWindow()
    {
        GetWindow<EasyCalculatorEditor>("Easy Calculator").Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(400, 700);
    }

    private void OnGUI()
    {
        // Center the entire calculator
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical(GUILayout.Width(380));

        // Title
        GUILayout.Label("Easy Calculator", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // History with delete button
        if (history.Count > 0)
        {
            historyScrollPos = EditorGUILayout.BeginScrollView(historyScrollPos, GUILayout.Height(150));

            for (int i = history.Count - 1; i >= 0; i--)
            {
                string entry = history[i];

                // Alternating background
                if (i % 2 == 0)
                {
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                    GUILayout.BeginHorizontal(GUILayout.Height(32));
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = prev;
                }

                GUILayout.BeginHorizontal();

                GUIStyle entryStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    richText = true,
                    fontSize = 13,
                    padding = new RectOffset(15, 8, 8, 8),
                    clipping = TextClipping.Clip
                };

                if (GUILayout.Button(entry, entryStyle, GUILayout.Height(32)))
                {
                    int eqIndex = entry.IndexOf(" = ");
                    if (eqIndex > 0)
                    {
                        input = entry.Substring(0, eqIndex);
                        justCalculated = false;
                        UpdateLivePreview();
                        Repaint();
                    }
                }

                // Delete button (×)
                GUIStyle deleteStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    margin = new RectOffset(0, 10, 8, 8)
                };

                if (GUILayout.Button("×", deleteStyle, GUILayout.Width(30), GUILayout.Height(32)))
                {
                    history.RemoveAt(i);
                    Repaint();
                }

                GUILayout.EndHorizontal();

                // Separator line
                Rect r = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(r.x + 10, r.yMax, r.width - 20, 1), new Color(0.35f, 0.35f, 0.35f));
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(10);
        }

        // Input field
        GUIStyle inputStyle = new GUIStyle(EditorStyles.textField)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleRight,
            padding = new RectOffset(20, 20, 12, 12)
        };
        string newInput = EditorGUILayout.TextField(input, inputStyle, GUILayout.Height(55));
        if (newInput != input)
        {
            input = newInput;
            justCalculated = false;
            UpdateLivePreview();
        }

        // Result display
        GUIStyle resultStyle = new GUIStyle(EditorStyles.largeLabel)
        {
            fontSize = 42,
            alignment = TextAnchor.MiddleRight,
            padding = new RectOffset(20, 20, 10, 25)
        };
        EditorGUILayout.LabelField(displayResult, resultStyle, GUILayout.Height(100));

        EditorGUILayout.Space(25);

        // Uniform button grid (all buttons same size)
        DrawUniformButtonGrid();

        EditorGUILayout.Space(15);

        // Equals button (centered)
        GUIStyle equalsStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 34,
            normal = { textColor = Color.white }
        };
        equalsStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.65f, 1f));
        equalsStyle.active.background = MakeTex(2, 2, new Color(0.1f, 0.55f, 0.9f));

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("=", equalsStyle, GUILayout.Width(360), GUILayout.Height(70)))
        {
            PerformCalculation();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical(); // End main column

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        // Enter key = calculate
        if (Event.current.isKey && 
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            PerformCalculation();
            Event.current.Use();
        }
    }

    private void DrawUniformButtonGrid()
    {
        void Row(params (string label, Action action)[] buttons)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (var (label, action) in buttons)
            {
                if (GUILayout.Button(label, GUILayout.Width(85), GUILayout.Height(55)))
                {
                    action?.Invoke();
                    Repaint();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }

        // All buttons are now exactly 85x55
        Row(("C", ClearAll), ("←", Backspace), ("(", () => Append("(")), (")", () => Append(")")));

        Row(("sqrt", () => AppendFunction("sqrt")), 
            ("sin", () => AppendFunction("sin")), 
            ("cos", () => AppendFunction("cos")), 
            ("tan", () => AppendFunction("tan")));

        Row(("7", () => Append("7")), ("8", () => Append("8")), ("9", () => Append("9")), ("÷", () => Append("/")));

        Row(("4", () => Append("4")), ("5", () => Append("5")), ("6", () => Append("6")), ("×", () => Append("*")));

        Row(("1", () => Append("1")), ("2", () => Append("2")), ("3", () => Append("3")), ("-", () => Append("-")));

        Row(("0", () => Append("0")), (".", () => Append(".")), ("π", () => Append("pi")), ("+", () => Append("+")));
    }

    private void Append(string text)
    {
        if (justCalculated)
        {
            input = "";
            justCalculated = false;
        }
        input += text;
        UpdateLivePreview();
    }

    private void AppendFunction(string func)
    {
        if (justCalculated)
        {
            input = "";
            justCalculated = false;
        }
        input += func + "(";
        UpdateLivePreview();
        // Optional: move cursor inside parentheses - not needed in IMGUI text field
    }

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
        justCalculated = false;
    }

    private void UpdateLivePreview()
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            displayResult = "0";
            return;
        }

        try
        {
            string processed = PreprocessExpression(input);
            double result = EvaluateExpression(processed);
            displayResult = result.ToString("G12");
        }
        catch
        {
            // Don't show error during typing
        }
    }

    private void PerformCalculation()
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        string original = input.Trim();
        string processed = PreprocessExpression(original);

        string result;
        try
        {
            double value = EvaluateExpression(processed);
            result = value.ToString("G12");
            displayResult = result;
        }
        catch
        {
            result = "<color=red>Error</color>";
            displayResult = "Error";
        }

        string historyEntry = $"{original} = {result.Replace("<color=red>", "").Replace("</color>", "")}";
        if (result.Contains("Error")) historyEntry = $"{original} = <color=red>Error</color>";

        history.Insert(0, historyEntry);
        if (history.Count > MaxHistory) history.RemoveAt(history.Count - 1);

        justCalculated = true;
        input = "";
        Repaint();
    }

    private Texture2D MakeTex(int w, int h, Color col)
    {
        Color[] pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }

    // ====================== PREPROCESSING ======================
    private string PreprocessExpression(string expr)
    {
        expr = expr.ToLower()
                   .Replace("pi", "3.141592653589793")
                   .Replace("π", "3.141592653589793")
                   .Replace("tau", "6.283185307179586")
                   .Replace("e", "2.718281828459045")
                   .Replace("×", "*")
                   .Replace("÷", "/");

        // deg/rad
        expr = Regex.Replace(expr, @"(\d+\.?\d*)\s*deg", m => (double.Parse(m.Groups[1].Value) * Mathf.Deg2Rad).ToString());
        expr = Regex.Replace(expr, @"(\d+\.?\d*)\s*rad", m => (double.Parse(m.Groups[1].Value) * Mathf.Rad2Deg).ToString());

        // Implicit multiplication: 2sin → 2*sin, (3)4 → (3)*4
        expr = Regex.Replace(expr, @"(\d)\s*([a-z(])", "$1*$2");
        expr = Regex.Replace(expr, @"([)\d])\s*([a-z(])", "$1*$2");

        return expr;
    }

    // ====================== EVALUATOR (Shunting-Yard) ======================
    private double EvaluateExpression(string expression)
    {
        var tokens = Tokenize(expression);
        return ParseExpression(tokens);
    }

    private List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c)) continue;

            if (char.IsDigit(c) || c == '.')
                sb.Append(c);
            else
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }

                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '^' || c == '(' || c == ')')
                    tokens.Add(c.ToString());
                else if (char.IsLetter(c))
                {
                    sb.Clear();
                    while (i < input.Length && char.IsLetter(input[i]))
                        sb.Append(input[i++]);
                    i--;
                    tokens.Add(sb.ToString());
                }
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    private double ParseExpression(List<string> tokens)
    {
        var output = new Queue<string>();
        var ops = new Stack<string>();

        foreach (var token in tokens)
        {
            if (double.TryParse(token, out _))
                output.Enqueue(token);
            else if (IsFunction(token))
                ops.Push(token);
            else if (token == "(")
                ops.Push(token);
            else if (token == ")")
            {
                while (ops.Count > 0 && ops.Peek() != "(")
                    output.Enqueue(ops.Pop());
                if (ops.Count > 0) ops.Pop();
                if (ops.Count > 0 && IsFunction(ops.Peek()))
                    output.Enqueue(ops.Pop());
            }
            else
            {
                while (ops.Count > 0 && GetPrecedence(ops.Peek()) >= GetPrecedence(token) && ops.Peek() != "(")
                    output.Enqueue(ops.Pop());
                ops.Push(token);
            }
        }

        while (ops.Count > 0) output.Enqueue(ops.Pop());

        return EvaluateRPN(output);
    }

    private double EvaluateRPN(Queue<string> queue)
    {
        var stack = new Stack<double>();
        while (queue.Count > 0)
        {
            string token = queue.Dequeue();
            if (double.TryParse(token, out double num))
                stack.Push(num);
            else if (IsFunction(token))
                stack.Push(ApplyFunction(token, stack.Pop()));
            else
            {
                double b = stack.Pop();
                double a = stack.Pop();
                stack.Push(ApplyOperator(token, a, b));
            }
        }
        return stack.Pop();
    }

    private bool IsFunction(string s) => s is "sin" or "cos" or "tan" or "sqrt" or "abs" or "log" or "ln" or "floor" or "ceil" or "round";

    private double ApplyFunction(string f, double x) => f switch
    {
        "sin" => Mathf.Sin((float)x),
        "cos" => Mathf.Cos((float)x),
        "tan" => Mathf.Tan((float)x),
        "sqrt" => Mathf.Sqrt((float)x),
        "abs" => Mathf.Abs((float)x),
        "log" => Mathf.Log10((float)x),
        "ln" => Mathf.Log((float)x),
        "floor" => Mathf.Floor((float)x),
        "ceil" => Mathf.Ceil((float)x),
        "round" => Mathf.Round((float)x),
        _ => throw new Exception("Unknown function")
    };

    private double ApplyOperator(string op, double a, double b) => op switch
    {
        "+" => a + b,
        "-" => a - b,
        "*" => a * b,
        "/" => b != 0 ? a / b : throw new Exception("Division by zero"),
        "^" => Mathf.Pow((float)a, (float)b),
        _ => throw new Exception("Unknown operator")
    };

    private int GetPrecedence(string op) => op switch
    {
        "+" or "-" => 1,
        "*" or "/" => 2,
        "^" => 3,
        _ => 0
    };
}