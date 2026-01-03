using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class EasyCalculatorEditor : EditorWindow
{
    private string input = "";
    private string previewResult = "";
    private Vector2 scrollPosition;
    private List<HistoryEntry> history = new List<HistoryEntry>();
    private const int MaxHistory = 100;

    private struct HistoryEntry
    {
        public string expression;
        public string result;
        public bool isError;
    }

    [MenuItem("Window/Tools/Easy Calculator %#c")] // Hotkey: Ctrl+Shift+C (or Cmd+Shift+C on Mac)
    public static void ShowWindow()
    {
        GetWindow<EasyCalculatorEditor>("Easy Calculator").Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(320, 400);
    }

    private void OnGUI()
    {
        GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

        // Title
        GUILayout.Label("Easy Calculator", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        // History Area (newest on top)
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var entry in history)
        {
            GUILayout.BeginHorizontal();

            string display = $"<b>{entry.expression}</b> = {entry.result}";
            GUIStyle style = new GUIStyle(EditorStyles.label) { richText = true };

            if (entry.isError)
                style.normal.textColor = new Color(1f, 0.4f, 0.4f);

            if (GUILayout.Button(display, style, GUILayout.ExpandWidth(true)))
            {
                input = entry.expression;
                UpdatePreview();
                GUI.FocusControl("InputField");
            }

            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
                EditorGUIUtility.systemCopyBuffer = entry.result;
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        // Input Field
        GUI.SetNextControlName("InputField");
        string newInput = EditorGUILayout.TextField(input, GUILayout.Height(32));

        if (newInput != input)
        {
            input = newInput;
            UpdatePreview();
        }

        // Live Preview
        EditorGUILayout.LabelField("Result:", EditorStyles.miniBoldLabel);
        if (string.IsNullOrEmpty(previewResult))
        {
            EditorGUILayout.LabelField("Enter an expression...", EditorStyles.miniLabel);
        }
        else
        {
            GUIStyle previewStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft
            };

            if (previewResult.StartsWith("Error"))
                previewStyle.normal.textColor = Color.red;

            EditorGUILayout.LabelField(previewResult, previewStyle);
        }

        // Handle Enter key
        if (Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
            GUI.GetNameOfFocusedControl() == "InputField")
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                PerformCalculation();
                input = "";
                previewResult = "";
                GUI.FocusControl("InputField");
            }
            Event.current.Use();
        }

        // Auto-focus on first open
        if (Event.current.type == EventType.Repaint && history.Count == 0 && string.IsNullOrEmpty(input))
        {
            GUI.FocusControl("InputField");
        }

        GUILayout.EndVertical();
    }

    private void UpdatePreview()
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            previewResult = "";
            return;
        }

        try
        {
            string processed = PreprocessExpression(input);
            double result = EvaluateExpression(processed);
            previewResult = result.ToString("G12");
        }
        catch
        {
            previewResult = "";
        }
    }

    private void PerformCalculation()
    {
        string expr = input.Trim();
        if (string.IsNullOrEmpty(expr)) return;

        string processed = PreprocessExpression(expr);
        string result;
        bool isError = false;

        try
        {
            double value = EvaluateExpression(processed);
            result = value.ToString("G12");
        }
        catch (Exception ex)
        {
            result = "Error: " + ex.Message;
            isError = true;
        }

        history.Insert(0, new HistoryEntry
        {
            expression = expr,
            result = result,
            isError = isError
        });

        if (history.Count > MaxHistory)
            history.RemoveAt(history.Count - 1);

        Repaint();
    }

    // Preprocess: replace common Unity/game dev shortcuts
    private string PreprocessExpression(string expr)
    {
        expr = expr.ToLower()
                   .Replace("pi", "3.141592653589793")
                   .Replace("π", "3.141592653589793")
                   .Replace("tau", "6.283185307179586")
                   .Replace("e", "2.718281828459045");

        // Degree/Radian helpers
        expr = Regex.Replace(expr, @"(\d+\.?\d*)\s*deg", m => (double.Parse(m.Groups[1].Value) * Mathf.Deg2Rad).ToString());
        expr = Regex.Replace(expr, @"(\d+\.?\d*)\s*rad", m => (double.Parse(m.Groups[1].Value) * Mathf.Rad2Deg).ToString());

        // Implicit multiplication: 2(3+4) → 2*(3+4), 5sin(30) → 5*sin(30)
        expr = Regex.Replace(expr, @"(\d)\s*\(", "$1*(");
        expr = Regex.Replace(expr, @"\)\s*(\d)", ")*$1");
        expr = Regex.Replace(expr, @"([a-z])\s*\(", "$1*(");

        return expr;
    }

    // Custom expression evaluator with shunting-yard algorithm (no dependencies!)
    private double EvaluateExpression(string expression)
    {
        try
        {
            return ParseExpression(Tokenize(expression));
        }
        catch
        {
            throw new Exception("Invalid expression");
        }
    }

    private List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var numberBuilder = new System.Text.StringBuilder();

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (char.IsWhiteSpace(c)) continue;

            if (char.IsDigit(c) || c == '.')
            {
                numberBuilder.Append(c);
            }
            else
            {
                if (numberBuilder.Length > 0)
                {
                    tokens.Add(numberBuilder.ToString());
                    numberBuilder.Clear();
                }

                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '^' || c == '(' || c == ')')
                {
                    tokens.Add(c.ToString());
                }
                else
                {
                    // Function name like sin, cos, etc.
                    var funcBuilder = new System.Text.StringBuilder();
                    while (i < input.Length && char.IsLetter(input[i]))
                    {
                        funcBuilder.Append(input[i]);
                        i++;
                    }
                    i--; // step back one
                    tokens.Add(funcBuilder.ToString());
                }
            }
        }

        if (numberBuilder.Length > 0)
            tokens.Add(numberBuilder.ToString());

        return tokens;
    }

    private double ParseExpression(List<string> tokens)
    {
        var outputQueue = new Queue<string>();
        var operatorStack = new Stack<string>();

        foreach (var token in tokens)
        {
            if (double.TryParse(token, out _))
            {
                outputQueue.Enqueue(token);
            }
            else if (IsFunction(token))
            {
                operatorStack.Push(token);
            }
            else if (token == "(")
            {
                operatorStack.Push(token);
            }
            else if (token == ")")
            {
                while (operatorStack.Peek() != "(")
                {
                    outputQueue.Enqueue(operatorStack.Pop());
                }
                operatorStack.Pop(); // remove '('

                if (operatorStack.Count > 0 && IsFunction(operatorStack.Peek()))
                {
                    outputQueue.Enqueue(operatorStack.Pop());
                }
            }
            else // operator
            {
                while (operatorStack.Count > 0 && GetPrecedence(operatorStack.Peek()) >= GetPrecedence(token) &&
                       operatorStack.Peek() != "(")
                {
                    outputQueue.Enqueue(operatorStack.Pop());
                }
                operatorStack.Push(token);
            }
        }

        while (operatorStack.Count > 0)
        {
            outputQueue.Enqueue(operatorStack.Pop());
        }

        return EvaluateRPN(outputQueue);
    }

    private double EvaluateRPN(Queue<string> queue)
    {
        var stack = new Stack<double>();

        while (queue.Count > 0)
        {
            string token = queue.Dequeue();

            if (double.TryParse(token, out double value))
            {
                stack.Push(value);
            }
            else if (IsFunction(token))
            {
                double arg = stack.Pop();
                stack.Push(ApplyFunction(token, arg));
            }
            else
            {
                double b = stack.Pop();
                double a = stack.Pop();
                stack.Push(ApplyOperator(token, a, b));
            }
        }

        return stack.Pop();
    }

    private bool IsFunction(string token)
    {
        return token == "sin" || token == "cos" || token == "tan" ||
               token == "asin" || token == "acos" || token == "atan" ||
               token == "sqrt" || token == "abs" || token == "log" || token == "ln" ||
               token == "floor" || token == "ceil" || token == "round";
    }

    private double ApplyFunction(string func, double arg)
    {
        return func switch
        {
            "sin"   => Mathf.Sin((float)arg),
            "cos"   => Mathf.Cos((float)arg),
            "tan"   => Mathf.Tan((float)arg),
            "asin"  => Mathf.Asin((float)arg),
            "acos"  => Mathf.Acos((float)arg),
            "atan"  => Mathf.Atan((float)arg),
            "sqrt"  => Mathf.Sqrt((float)arg),
            "abs"   => Mathf.Abs((float)arg),
            "log"   => Mathf.Log10((float)arg),
            "ln"    => Mathf.Log((float)arg),
            "floor" => Mathf.Floor((float)arg),
            "ceil"  => Mathf.Ceil((float)arg),
            "round" => Mathf.Round((float)arg),
            _       => throw new Exception("Unknown function")
        };
    }

    private double ApplyOperator(string op, double a, double b)
    {
        return op switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,
            "/" => a / b,
            "^" => Mathf.Pow((float)a, (float)b),
            _   => throw new Exception("Unknown operator")
        };
    }

    private int GetPrecedence(string op)
    {
        return op switch
        {
            "+" or "-" => 1,
            "*" or "/" => 2,
            "^" => 3,
            _ => 0
        };
    }
}