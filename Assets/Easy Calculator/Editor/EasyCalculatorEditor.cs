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
    private const int MaxHistory = 50;
    private bool justCalculated = false;

    [MenuItem("Window/Tools/Easy Calculator %#c")] // Ctrl+Shift+C
    public static void ShowWindow()
    {
        GetWindow<EasyCalculatorEditor>("Easy Calculator").Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(400, 680);
    }

    private void OnGUI()
    {
        // Center everything horizontally
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical(GUILayout.Width(380));

        // Title
        GUILayout.Label("Easy Calculator", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // History Area with alternating rows and separators
        if (history.Count > 0)
        {
            historyScrollPos = EditorGUILayout.BeginScrollView(historyScrollPos, GUILayout.Height(140));

            for (int i = history.Count - 1; i >= 0; i--)
            {
                string entry = history[i];

                // Alternating background
                if (i % 2 == 0)
                {
                    Color prevColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                    GUILayout.BeginHorizontal(GUILayout.Height(28));
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = prevColor;
                }

                GUILayout.BeginHorizontal();

                GUIStyle historyStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    richText = true,
                    fontSize = 13,
                    padding = new RectOffset(15, 15, 6, 6)
                };

                if (GUILayout.Button(entry, historyStyle))
                {
                    int equalsIndex = entry.IndexOf(" = ");
                    if (equalsIndex > 0)
                    {
                        input = entry.Substring(0, equalsIndex);
                        justCalculated = false;
                        UpdateLivePreview();
                        Repaint();
                    }
                }

                GUILayout.EndHorizontal();

                // Thin separator line
                Rect separatorRect = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(separatorRect.x, separatorRect.yMax, separatorRect.width, 1), 
                    new Color(0.3f, 0.3f, 0.3f));
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(10);
        }

        // Current Input Field
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

        // Large Result Display
        GUIStyle resultStyle = new GUIStyle(EditorStyles.largeLabel)
        {
            fontSize = 42,
            alignment = TextAnchor.MiddleRight,
            padding = new RectOffset(20, 20, 10, 25)
        };
        EditorGUILayout.LabelField(displayResult, resultStyle, GUILayout.Height(100));

        EditorGUILayout.Space(25);

        // Centered Button Grid
        DrawCenteredButtonGrid();

        EditorGUILayout.Space(15);

        // Equals Button (centered, full width)
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

        GUILayout.EndVertical(); // End centered column

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        // Handle Enter key
        if (Event.current.isKey &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            PerformCalculation();
            Event.current.Use();
        }
    }

    private void DrawCenteredButtonGrid()
    {
        // Helper to center a row
        void CenteredRow(Action drawButtons)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            drawButtons();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // Row 1: C ← ( )
        CenteredRow(() =>
        {
            Button("C", ClearAll);
            Button("←", Backspace);
            Button("(", () => Append("("));
            Button(")", () => Append(")"));
        });

        // Row 2: Functions
        CenteredRow(() =>
        {
            Button("sqrt", () => Append("sqrt()"));
            Button("sin", () => Append("sin()"));
            Button("cos", () => Append("cos()"));
            Button("tan", () => Append("tan()"));
        });

        // Row 3: 7 8 9 ÷
        CenteredRow(() =>
        {
            Button("7", () => Append("7"));
            Button("8", () => Append("8"));
            Button("9", () => Append("9"));
            Button("÷", () => Append("/"));
        });

        // Row 4: 4 5 6 ×
        CenteredRow(() =>
        {
            Button("4", () => Append("4"));
            Button("5", () => Append("5"));
            Button("6", () => Append("6"));
            Button("×", () => Append("*"));
        });

        // Row 5: 1 2 3 -
        CenteredRow(() =>
        {
            Button("1", () => Append("1"));
            Button("2", () => Append("2"));
            Button("3", () => Append("3"));
            Button("-", () => Append("-"));
        });

        // Row 6: 0 . π +
        CenteredRow(() =>
        {
            Button("0", () => Append("0"), GUILayout.Width(140));
            Button(".", () => Append("."));
            Button("π", () => Append("pi"));
            Button("+", () => Append("+"));
        });
    }

    private void Button(string label, Action action, params GUILayoutOption[] options)
    {
        GUILayoutOption[] opts = options.Length > 0 ? options : new[] { GUILayout.Width(85), GUILayout.Height(55) };
        if (GUILayout.Button(label, opts))
        {
            action?.Invoke();
            Repaint();
        }
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
            // Keep last valid result while typing
        }
    }

    private void PerformCalculation()
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        string originalExpression = input.Trim();
        string processed = PreprocessExpression(originalExpression);

        try
        {
            double value = EvaluateExpression(processed);
            displayResult = value.ToString("G12");

            string historyEntry = $"{originalExpression} = {displayResult}";
            history.Insert(0, historyEntry);
        }
        catch
        {
            displayResult = "Error";
            string historyEntry = $"{originalExpression} = <color=red>Error</color>";
            history.Insert(0, historyEntry);
        }

        if (history.Count > MaxHistory)
            history.RemoveAt(history.Count - 1);

        justCalculated = true;
        input = "";
        Repaint();
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    // ====================== PREPROCESSING & EVALUATOR ======================
    // (Same robust custom evaluator as before - unchanged for reliability)

    private string PreprocessExpression(string expr)
    {
        expr = expr.ToLower()
                   .Replace("pi", "3.141592653589793")
                   .Replace("π", "3.141592653589793")
                   .Replace("tau", "6.283185307179586")
                   .Replace("e", "2.718281828459045")
                   .Replace("×", "*")
                   .Replace("÷", "/");

        expr = Regex.Replace(expr, @"(\d+\.?\d*)\s*deg", m => (double.Parse(m.Groups[1].Value) * Mathf.Deg2Rad).ToString());
        expr = Regex.Replace(expr, @"(\d+\.?\d*)\s*rad", m => (double.Parse(m.Groups[1].Value) * Mathf.Rad2Deg).ToString());

        expr = Regex.Replace(expr, @"(\d)\s*\(", "$1*(");
        expr = Regex.Replace(expr, @"\)\s*(\d)", ")*$1");
        expr = Regex.Replace(expr, @"([a-z])\s*\(", "$1*(");

        return expr;
    }

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