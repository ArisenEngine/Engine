using System.Globalization;

namespace Arisen.Versioning;

internal static class EngineCompatibility
{
    public const string CurrentVersionText = "0.1.0";

    public static readonly SemanticVersion CurrentVersion =
        SemanticVersion.Parse(CurrentVersionText);
}

internal readonly record struct SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string Prerelease) : IComparable<SemanticVersion>
{
    public static SemanticVersion Parse(string value)
    {
        if (!TryParse(value, out SemanticVersion version))
        {
            throw new FormatException($"'{value}' is not a valid semantic version.");
        }

        return version;
    }

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        string text = value.Trim();
        int buildIndex = text.IndexOf('+');
        if (buildIndex >= 0)
        {
            if (!IsValidIdentifierList(text[(buildIndex + 1)..], allowLeadingZeroes: true))
            {
                return false;
            }

            text = text[..buildIndex];
        }

        string prerelease = string.Empty;
        int prereleaseIndex = text.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            prerelease = text[(prereleaseIndex + 1)..];
            if (!IsValidIdentifierList(prerelease, allowLeadingZeroes: false))
            {
                return false;
            }

            text = text[..prereleaseIndex];
        }

        string[] parts = text.Split('.');
        if (parts.Length is < 1 or > 3 ||
            !TryParseCorePart(parts[0], out int major) ||
            !TryParseCorePart(parts.Length > 1 ? parts[1] : "0", out int minor) ||
            !TryParseCorePart(parts.Length > 2 ? parts[2] : "0", out int patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public static bool TryParseExact(string? value, out SemanticVersion version)
    {
        if (!TryParse(value, out version)) return false;

        string core = value!.Trim();
        int metadataIndex = core.IndexOfAny(['-', '+']);
        if (metadataIndex >= 0) core = core[..metadataIndex];
        return core.Count(character => character == '.') == 2;
    }

    public int CompareTo(SemanticVersion other)
    {
        int comparison = Major.CompareTo(other.Major);
        if (comparison != 0) return comparison;

        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0) return comparison;

        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0) return comparison;

        if (string.IsNullOrEmpty(Prerelease))
        {
            return string.IsNullOrEmpty(other.Prerelease) ? 0 : 1;
        }

        if (string.IsNullOrEmpty(other.Prerelease)) return -1;

        string[] leftIdentifiers = Prerelease.Split('.');
        string[] rightIdentifiers = other.Prerelease.Split('.');
        int count = Math.Min(leftIdentifiers.Length, rightIdentifiers.Length);
        for (int index = 0; index < count; index++)
        {
            comparison = ComparePrereleaseIdentifier(
                leftIdentifiers[index],
                rightIdentifiers[index]);
            if (comparison != 0) return comparison;
        }

        return leftIdentifiers.Length.CompareTo(rightIdentifiers.Length);
    }

    public override string ToString()
    {
        string value = $"{Major}.{Minor}.{Patch}";
        return string.IsNullOrEmpty(Prerelease) ? value : $"{value}-{Prerelease}";
    }

    private static bool TryParseCorePart(string value, out int part)
    {
        part = 0;
        if (value.Length == 0 || (value.Length > 1 && value[0] == '0')) return false;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out part);
    }

    private static bool IsValidIdentifierList(string value, bool allowLeadingZeroes)
    {
        if (value.Length == 0) return false;

        foreach (string identifier in value.Split('.'))
        {
            if (identifier.Length == 0 ||
                identifier.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                return false;
            }

            if (!allowLeadingZeroes && identifier.All(char.IsAsciiDigit) &&
                identifier.Length > 1 && identifier[0] == '0')
            {
                return false;
            }
        }

        return true;
    }

    private static int ComparePrereleaseIdentifier(string left, string right)
    {
        bool leftNumeric = left.All(char.IsAsciiDigit);
        bool rightNumeric = right.All(char.IsAsciiDigit);
        if (leftNumeric && rightNumeric)
        {
            int lengthComparison = left.Length.CompareTo(right.Length);
            return lengthComparison != 0
                ? lengthComparison
                : string.Compare(left, right, StringComparison.Ordinal);
        }

        if (leftNumeric != rightNumeric) return leftNumeric ? -1 : 1;
        return string.Compare(left, right, StringComparison.Ordinal);
    }
}

internal sealed class SemanticVersionRange
{
    private readonly IReadOnlyList<Comparator> m_Comparators;

    private SemanticVersionRange(string expression, IReadOnlyList<Comparator> comparators)
    {
        Expression = expression;
        m_Comparators = comparators;
    }

    public string Expression { get; }

    public static bool TryParse(
        string? expression,
        out SemanticVersionRange range,
        out string error)
    {
        string trimmed = string.IsNullOrWhiteSpace(expression) ? "*" : expression.Trim();
        range = new SemanticVersionRange(trimmed, Array.Empty<Comparator>());
        error = string.Empty;

        if (trimmed is "*" or "x" or "X") return true;
        if (trimmed.Contains("||", StringComparison.Ordinal))
        {
            error = "Alternative ('||') semantic-version ranges are not supported.";
            return false;
        }

        var comparators = new List<Comparator>();
        foreach (string token in trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith('^'))
            {
                if (!SemanticVersion.TryParse(token[1..], out SemanticVersion baseVersion))
                {
                    error = $"Invalid caret version '{token}'.";
                    return false;
                }

                comparators.Add(new Comparator(ComparisonOperator.GreaterThanOrEqual, baseVersion));
                comparators.Add(new Comparator(ComparisonOperator.LessThan, GetCaretUpperBound(baseVersion)));
                continue;
            }

            if (token.StartsWith('~'))
            {
                if (!SemanticVersion.TryParse(token[1..], out SemanticVersion baseVersion))
                {
                    error = $"Invalid tilde version '{token}'.";
                    return false;
                }

                comparators.Add(new Comparator(ComparisonOperator.GreaterThanOrEqual, baseVersion));
                comparators.Add(new Comparator(
                    ComparisonOperator.LessThan,
                    new SemanticVersion(baseVersion.Major, baseVersion.Minor + 1, 0, string.Empty)));
                continue;
            }

            ComparisonOperator comparisonOperator = ComparisonOperator.Equal;
            string versionText = token;
            foreach (string candidate in new[] { ">=", "<=", ">", "<", "=" })
            {
                if (!token.StartsWith(candidate, StringComparison.Ordinal)) continue;

                comparisonOperator = candidate switch
                {
                    ">=" => ComparisonOperator.GreaterThanOrEqual,
                    "<=" => ComparisonOperator.LessThanOrEqual,
                    ">" => ComparisonOperator.GreaterThan,
                    "<" => ComparisonOperator.LessThan,
                    _ => ComparisonOperator.Equal
                };
                versionText = token[candidate.Length..];
                break;
            }

            if (!SemanticVersion.TryParse(versionText, out SemanticVersion version))
            {
                error = $"Invalid semantic-version range token '{token}'.";
                return false;
            }

            comparators.Add(new Comparator(comparisonOperator, version));
        }

        if (comparators.Count == 0)
        {
            error = $"Semantic-version range '{trimmed}' contains no constraints.";
            return false;
        }

        range = new SemanticVersionRange(trimmed, comparators);
        return true;
    }

    public bool Matches(SemanticVersion version)
    {
        return m_Comparators.All(comparator => comparator.Matches(version));
    }

    public bool Matches(string versionText)
    {
        return SemanticVersion.TryParseExact(versionText, out SemanticVersion version) &&
            Matches(version);
    }

    private static SemanticVersion GetCaretUpperBound(SemanticVersion version)
    {
        if (version.Major > 0)
        {
            return new SemanticVersion(version.Major + 1, 0, 0, string.Empty);
        }

        if (version.Minor > 0)
        {
            return new SemanticVersion(0, version.Minor + 1, 0, string.Empty);
        }

        return new SemanticVersion(0, 0, version.Patch + 1, string.Empty);
    }

    private readonly record struct Comparator(
        ComparisonOperator Operator,
        SemanticVersion Version)
    {
        public bool Matches(SemanticVersion version)
        {
            int comparison = version.CompareTo(Version);
            return Operator switch
            {
                ComparisonOperator.Equal => comparison == 0,
                ComparisonOperator.GreaterThan => comparison > 0,
                ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
                ComparisonOperator.LessThan => comparison < 0,
                ComparisonOperator.LessThanOrEqual => comparison <= 0,
                _ => false
            };
        }
    }

    private enum ComparisonOperator
    {
        Equal,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual
    }
}
