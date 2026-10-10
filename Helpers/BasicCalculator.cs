using System.Globalization;

namespace MyActivity.Helpers;

/// <summary>
/// Immediate-execution calculator (like a phone calculator). Pure logic, no UI.
/// Keys: 0-9 . + - * / = % C (clear) B (backspace) N (negate)
/// </summary>
public class BasicCalculator
{
    private const int MaxDigits = 15;

    private string _entry = "0";
    private decimal? _accumulator;
    private char? _pendingOp;
    private bool _startNewEntry = true;   // next digit replaces the entry
    private bool _justEvaluated;
    private string? _error;
    private string _expression = string.Empty;

    public string Display => _error ?? _entry;
    public string Expression => _expression;
    public bool HasError => _error is not null;

    public void Press(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var c = key[0];
        switch (c)
        {
            case >= '0' and <= '9': Digit(c); break;
            case '.': Decimal(); break;
            case '+': case '-': case '*': case '/': Operator(c); break;
            case '=': Evaluate(); break;
            case '%': Percent(); break;
            case 'C': Clear(); break;
            case 'B': Backspace(); break;
            case 'N': Negate(); break;
        }
    }

    public void Clear()
    {
        _entry = "0"; _accumulator = null; _pendingOp = null;
        _startNewEntry = true; _justEvaluated = false; _error = null; _expression = string.Empty;
    }

    private void Digit(char d)
    {
        if (_error is not null) Clear();
        if (_justEvaluated) { Clear(); }

        if (_startNewEntry) { _entry = d.ToString(); _startNewEntry = false; return; }
        if (_entry == "0") { _entry = d.ToString(); return; }
        if (_entry == "-0") { _entry = "-" + d; return; }
        if (_entry.Count(char.IsDigit) >= MaxDigits) return;
        _entry += d;
    }

    private void Decimal()
    {
        if (_error is not null) Clear();
        if (_justEvaluated) Clear();
        if (_startNewEntry) { _entry = "0."; _startNewEntry = false; return; }
        if (!_entry.Contains('.')) _entry += ".";
    }

    private void Operator(char op)
    {
        if (_error is not null) return;
        _justEvaluated = false;

        if (_pendingOp is not null && _accumulator is not null && !_startNewEntry)
        {
            if (!TryCompute(_accumulator.Value, Parse(_entry), _pendingOp.Value, out var result)) return;
            _accumulator = result;
            _entry = Format(result);
        }
        else if (_accumulator is null || _pendingOp is null)
        {
            _accumulator = Parse(_entry);
        }

        _pendingOp = op;
        _startNewEntry = true;
        _expression = $"{Format(_accumulator!.Value)} {Symbol(op)}";
    }

    private void Evaluate()
    {
        if (_error is not null || _pendingOp is null || _accumulator is null) return;

        var rhs = Parse(_entry);
        var left = _accumulator.Value;
        var op = _pendingOp.Value;
        if (!TryCompute(left, rhs, op, out var result)) return;

        _expression = $"{Format(left)} {Symbol(op)} {Format(rhs)} =";
        _entry = Format(result);
        _accumulator = null;
        _pendingOp = null;
        _startNewEntry = true;
        _justEvaluated = true;
    }

    /// <summary>a + b% means a + (a x b/100); a x b% means a x (b/100); a lone value becomes value/100.</summary>
    private void Percent()
    {
        if (_error is not null) return;
        var v = Parse(_entry);

        if (_pendingOp is not null && _accumulator is not null)
        {
            v = _pendingOp is '+' or '-' ? _accumulator.Value * v / 100 : v / 100;
            _entry = Format(v);
            _startNewEntry = false;
        }
        else
        {
            _entry = Format(v / 100);
            _startNewEntry = true;
            _justEvaluated = false;
        }
    }

    private void Backspace()
    {
        if (_error is not null || _startNewEntry || _justEvaluated) return;
        _entry = _entry.Length <= 1 || (_entry.Length == 2 && _entry[0] == '-') ? "0" : _entry[..^1];
    }

    private void Negate()
    {
        if (_error is not null || _entry == "0") return;
        _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
    }

    private bool TryCompute(decimal a, decimal b, char op, out decimal result)
    {
        result = 0;
        try
        {
            switch (op)
            {
                case '+': result = a + b; break;
                case '-': result = a - b; break;
                case '*': result = a * b; break;
                case '/':
                    if (b == 0) { Fail("Cannot divide by zero"); return false; }
                    result = a / b; break;
            }
            return true;
        }
        catch (OverflowException)
        {
            Fail("Number too large");
            return false;
        }
    }

    private void Fail(string message)
    {
        _error = message;
        _accumulator = null;
        _pendingOp = null;
        _startNewEntry = true;
        _expression = string.Empty;
    }

    private static decimal Parse(string s) =>
        decimal.Parse(s.EndsWith('.') ? s + "0" : s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string Format(decimal v)
    {
        v = Math.Round(v, 10, MidpointRounding.AwayFromZero);
        var s = v.ToString("0.##########", CultureInfo.InvariantCulture);
        return s == "-0" ? "0" : s;
    }

    private static string Symbol(char op) => op switch { '*' => "×", '/' => "÷", '-' => "−", _ => "+" };
}
