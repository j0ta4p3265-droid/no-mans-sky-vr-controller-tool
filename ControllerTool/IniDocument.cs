using System.Globalization;

namespace NMSOpenCompositeConfigurator;

internal sealed class IniDocument
{
    private readonly List<string> _lines = new();

    public static IniDocument Load(string path)
    {
        var document = new IniDocument();
        if (File.Exists(path))
            document._lines.AddRange(File.ReadAllLines(path));
        return document;
    }

    public string Get(string key, string fallback)
    {
        var currentSection = "";
        foreach (var rawLine in _lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            if (currentSection.Length != 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            var equals = line.IndexOf('=');
            if (equals <= 0)
                continue;
            if (line[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return line[(equals + 1)..].Trim();
        }
        return fallback;
    }

    public decimal GetDecimal(string key, decimal fallback)
    {
        return decimal.TryParse(Get(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public bool GetBoolean(string key, bool fallback)
    {
        var value = Get(key, "");
        if (bool.TryParse(value, out var parsed))
            return parsed;
        return value switch
        {
            "1" or "yes" or "on" => true,
            "0" or "no" or "off" => false,
            _ => fallback
        };
    }

    public void Set(string key, decimal value)
    {
        Set(key, value.ToString("0.00", CultureInfo.InvariantCulture));
    }

    public void Set(string key, bool value)
    {
        Set(key, value ? "true" : "false");
    }

    public void Set(string key, int value)
    {
        Set(key, value.ToString(CultureInfo.InvariantCulture));
    }

    public void Set(string key, string value)
    {
        var currentSection = "";
        var insertAt = _lines.Count;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i].Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (currentSection.Length == 0)
                    insertAt = i;
                currentSection = line[1..^1].Trim();
                continue;
            }

            if (currentSection.Length != 0)
                continue;

            var equals = line.IndexOf('=');
            if (equals > 0 && line[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                _lines[i] = $"{key}={value}";
                return;
            }
        }

        _lines.Insert(insertAt, $"{key}={value}");
    }

	public void Remove(string key)
	{
		var currentSection = "";
		for (var i = _lines.Count - 1; i >= 0; i--)
		{
			var line = _lines[i].Trim();
			if (line.StartsWith('[') && line.EndsWith(']'))
			{
				currentSection = line[1..^1].Trim();
				continue;
			}
			if (currentSection.Length != 0)
				continue;
			var equals = line.IndexOf('=');
			if (equals > 0 && line[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
				_lines.RemoveAt(i);
		}
	}

    public void SaveAtomic(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllLines(temporary, _lines);
        File.Move(temporary, path, true);
    }
}
