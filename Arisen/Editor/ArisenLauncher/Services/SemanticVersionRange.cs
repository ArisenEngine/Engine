using System;
using System.Collections.Generic;
using System.Linq;

namespace ArisenLauncher.Services;

internal readonly record struct SemanticVersion(int Major, int Minor, int Patch, string Prerelease) : IComparable<SemanticVersion>
{
    public static bool TryParse(string value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string core = value.Trim();
        int buildIndex = core.IndexOf('+');
        if (buildIndex >= 0)
            core = core[..buildIndex];

        string prerelease = string.Empty;
        int prereleaseIndex = core.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            prerelease = core[(prereleaseIndex + 1)..];
            core = core[..prereleaseIndex];
        }

        string[] parts = core.Split('.');
        if (parts.Length is < 1 or > 3)
            return false;

        if (!TryParsePart(parts[0], out int major)
            || !TryParsePart(parts.Length > 1 ? parts[1] : "0", out int minor)
            || !TryParsePart(parts.Length > 2 ? parts[2] : "0", out int patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        int major = Major.CompareTo(other.Major);
        if (major != 0) return major;

        int minor = Minor.CompareTo(other.Minor);
        if (minor != 0) return minor;

        int patch = Patch.CompareTo(other.Patch);
        if (patch != 0) return patch;

        if (string.IsNullOrEmpty(Prerelease) && string.IsNullOrEmpty(other.Prerelease)) return 0;
        if (string.IsNullOrEmpty(Prerelease)) return 1;
        if (string.IsNullOrEmpty(other.Prerelease)) return -1;

        return string.Compare(Prerelease, other.Prerelease, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString()
    {
        string value = $"{Major}.{Minor}.{Patch}";
        return string.IsNullOrEmpty(Prerelease) ? value : $"{value}-{Prerelease}";
    }

    private static bool TryParsePart(string value, out int part)
    {
        return int.TryParse(value, out part) && part >= 0;
    }
}

internal sealed class SemanticVersionRange
{
    private readonly List<Comparator> m_Comparators;

    private SemanticVersionRange(string expression, List<Comparator> comparators)
    {
        Expression = expression;
        m_Comparators = comparators;
    }

    public string Expression { get; }

    public static bool TryParse(string? expression, out SemanticVersionRange range, out string error)
    {
        string trimmed = string.IsNullOrWhiteSpace(expression) ? "*" : expression.Trim();
        range = new SemanticVersionRange(trimmed, new List<Comparator>());
        error = string.Empty;

        if (trimmed is "*" or "x" or "X")
            return true;

        var comparators = new List<Comparator>();
        foreach (string rawToken in trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string token = rawToken.Trim();
            if (token.StartsWith('^'))
            {
                if (!SemanticVersion.TryParse(token[1..], out var baseVersion))
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
                if (!SemanticVersion.TryParse(token[1..], out var baseVersion))
                {
                    error = $"Invalid tilde version '{token}'.";
                    return false;
                }

                comparators.Add(new Comparator(ComparisonOperator.GreaterThanOrEqual, baseVersion));
                comparators.Add(new Comparator(ComparisonOperator.LessThan, new SemanticVersion(baseVersion.Major, baseVersion.Minor + 1, 0, string.Empty)));
                continue;
            }

            var op = ComparisonOperator.Equal;
            string versionText = token;
            foreach (var candidate in new[] { ">=", "<=", ">", "<", "=" })
            {
                if (token.StartsWith(candidate, StringComparison.Ordinal))
                {
                    op = candidate switch
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
            }

            if (!SemanticVersion.TryParse(versionText, out var version))
            {
                error = $"Invalid version range token '{token}'.";
                return false;
            }

            comparators.Add(new Comparator(op, version));
        }

        range = new SemanticVersionRange(trimmed, comparators);
        return true;
    }

    public bool Matches(string versionText)
    {
        if (!SemanticVersion.TryParse(versionText, out var version))
            return false;

        return m_Comparators.All(comparator => comparator.Matches(version));
    }

    public PackageRegistryPackageVersion? SelectHighestMatch(IEnumerable<PackageRegistryPackageVersion> packages)
    {
        return packages
            .Select(package => new
            {
                Package = package,
                Parsed = SemanticVersion.TryParse(package.Version, out var parsed) ? parsed : (SemanticVersion?)null
            })
            .Where(x => x.Parsed.HasValue && Matches(x.Package.Version))
            .OrderByDescending(x => x.Parsed!.Value)
            .ThenByDescending(x => x.Package.Version, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Package)
            .FirstOrDefault();
    }

    private static SemanticVersion GetCaretUpperBound(SemanticVersion version)
    {
        if (version.Major > 0)
            return new SemanticVersion(version.Major + 1, 0, 0, string.Empty);

        if (version.Minor > 0)
            return new SemanticVersion(0, version.Minor + 1, 0, string.Empty);

        return new SemanticVersion(0, 0, version.Patch + 1, string.Empty);
    }

    private readonly record struct Comparator(ComparisonOperator Operator, SemanticVersion Version)
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
