using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// A lightweight Tracery-style grammar engine for Unity.
/// Load a grammar JSON file, then call Generate("origin", shapeName) to get a line.
/// 
/// Grammar format: { "ruleName": ["option1", "option2", ...] }
/// Reference other rules inline with #ruleName#
/// The special token #shapeName# is replaced with the provided shape name.
/// </summary>
public class TraceryEngine
{
    private Dictionary<string, List<string>> _grammar = new();
    private const int MaxDepth = 20; // prevent infinite recursion

    // ─────────────────────────────────────────────
    //  Loading
    // ─────────────────────────────────────────────

    public static TraceryEngine FromJson(string json)
    {
        var engine = new TraceryEngine();
        engine.LoadJson(json);
        return engine;
    }

    public static TraceryEngine FromTextAsset(TextAsset asset)
    {
        return FromJson(asset.text);
    }

    private void LoadJson(string json)
    {
        // Unity's JsonUtility can't deserialize arbitrary dicts,
        // so we do a lightweight manual parse using Regex.
        _grammar.Clear();

        // Match "key": ["val1", "val2", ...]
        var rulePattern = new Regex(@"""(\w+)""\s*:\s*\[([^\]]*)\]", RegexOptions.Singleline);
        var valuePattern = new Regex(@"""((?:[^""\\]|\\.)*)""");

        foreach (Match ruleMatch in rulePattern.Matches(json))
        {
            string key = ruleMatch.Groups[1].Value;
            string valuesBlock = ruleMatch.Groups[2].Value;

            var values = new List<string>();
            foreach (Match valMatch in valuePattern.Matches(valuesBlock))
            {
                values.Add(valMatch.Groups[1].Value);
            }

            if (values.Count > 0)
                _grammar[key] = values;
        }

        Debug.Log($"[Tracery] Loaded grammar with {_grammar.Count} rules.");
    }

    // ─────────────────────────────────────────────
    //  Generation
    // ─────────────────────────────────────────────

    /// <summary>
    /// Generate text from a starting rule, substituting shapeName for #shapeName#.
    /// </summary>
    public string Generate(string startRule, string shapeName = "cloud")
    {
        if (!_grammar.ContainsKey(startRule))
        {
            Debug.LogWarning($"[Tracery] Rule '{startRule}' not found in grammar.");
            return string.Empty;
        }

        string result = Expand(startRule, 0);

        // Final substitution: replace any literal #shapeName# tokens
        result = result.Replace("#shapeName#", shapeName);

        // Capitalise first letter
        if (result.Length > 0)
            result = char.ToUpper(result[0]) + result.Substring(1);

        return result;
    }

    private string Expand(string symbol, int depth)
    {
        if (depth > MaxDepth)
        {
            Debug.LogWarning($"[Tracery] Max depth reached expanding '{symbol}'.");
            return symbol;
        }

        if (!_grammar.ContainsKey(symbol))
            return symbol; // treat unknown symbols as literals

        // Pick a random option from this rule
        var options = _grammar[symbol];
        string chosen = options[Random.Range(0, options.Count)];

        // Recursively expand any #tag# references within the chosen string
        return ExpandTags(chosen, depth + 1);
    }

    private string ExpandTags(string text, int depth)
    {
        // Find all #tag# patterns (excluding #shapeName# which is resolved at the end)
        var tagPattern = new Regex(@"#(\w+)#");
        return tagPattern.Replace(text, match =>
        {
            string tag = match.Groups[1].Value;
            if (tag == "shapeName") return "#shapeName#"; // preserve for final substitution
            return Expand(tag, depth);
        });
    }

    // ─────────────────────────────────────────────
    //  Utilities
    // ─────────────────────────────────────────────

    public bool HasRule(string ruleName) => _grammar.ContainsKey(ruleName);

    public void AddRule(string ruleName, List<string> options)
    {
        _grammar[ruleName] = options;
    }
}
