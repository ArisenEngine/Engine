using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Com.Arisen.Rendering.Tests.CppSourceContractScanner;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VulkanResultSourceContractTests
{
    private const string VulkanSourceDirectory =
        "Arisen/Development/PackageGame/Local/com.arisen.rhi.vulkan.native/RHI.Vulkan";

    [Fact]
    public void EveryFirstPartyVulkanResultIsCheckedBranchedOrPropagated()
    {
        HashSet<string> resultCommands = LoadVulkanResultCommands(FindVulkanRegistry());
        Assert.True(
            resultCommands.Count >= 250,
            $"Vulkan registry exposed only {resultCommands.Count} VkResult commands.");

        string sourceRoot = Path.Combine(FindRepoRoot(), VulkanSourceDirectory);
        string[] sourceFiles = Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(static path =>
                Path.GetExtension(path) is ".cpp" or ".h" &&
                !HasExcludedSourceDirectory(path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(sourceFiles);

        var violations = new List<string>();
        int callCount = 0;
        int checkedCount = 0;
        int branchedCount = 0;
        int propagatedCount = 0;
        var callCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string sourceFile in sourceFiles)
        {
            string source = MaskCommentsAndLiterals(File.ReadAllText(sourceFile));
            foreach (Match match in Regex.Matches(
                         source,
                         @"\bvk[A-Za-z0-9_]+(?=\s*\()",
                         RegexOptions.CultureInvariant))
            {
                string command = match.Value;
                if (!resultCommands.Contains(command))
                {
                    continue;
                }

                callCount++;
                callCounts[command] = callCounts.GetValueOrDefault(command) + 1;
                int parameterStart = source.IndexOf('(', match.Index + match.Length);
                int parameterEnd = FindMatchingDelimiter(source, parameterStart, '(', ')');
                if (parameterEnd < 0)
                {
                    violations.Add(FormatViolation(
                        sourceFile,
                        source,
                        match.Index,
                        command,
                        "has an unterminated argument list"));
                    continue;
                }

                if (IsInsideInvocation(source, match.Index, parameterEnd, "CheckVkResult"))
                {
                    checkedCount++;
                    continue;
                }

                int statementStart = FindStatementStart(source, match.Index);
                string statementPrefix = source[statementStart..match.Index];
                if (IsPropagatedThroughAuditedBoundary(
                        source,
                        command,
                        match.Index,
                        parameterEnd,
                        statementPrefix))
                {
                    propagatedCount++;
                    continue;
                }

                if (TryGetAssignedVariable(statementPrefix, out string? variable) &&
                    IsResultVariableObserved(source, variable, match.Index, parameterEnd))
                {
                    branchedCount++;
                    continue;
                }

                violations.Add(FormatViolation(
                    sourceFile,
                    source,
                    match.Index,
                    command,
                    "discards its VkResult or does not make the handling path explicit"));
            }
        }

        Assert.True(callCount > 0, $"No VkResult command calls were found under '{sourceRoot}'.");
        Assert.True(
            checkedCount + branchedCount + propagatedCount + violations.Count == callCount,
            $"Accounted for {checkedCount + branchedCount + propagatedCount + violations.Count} " +
            $"of {callCount} calls.");
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));

        AssertCommandFamilyObserved(
            callCounts, "queue submission", "vkQueueSubmit", "vkQueueSubmit2", "vkQueueSubmit2KHR");
        AssertCommandFamilyObserved(callCounts, "queue idle", "vkQueueWaitIdle");
        AssertCommandFamilyObserved(callCounts, "semaphore creation", "vkCreateSemaphore");
        AssertCommandFamilyObserved(
            callCounts, "semaphore host wait", "vkWaitSemaphores", "vkWaitSemaphoresKHR");
        AssertCommandFamilyObserved(
            callCounts, "semaphore host signal", "vkSignalSemaphore", "vkSignalSemaphoreKHR");
        AssertCommandFamilyObserved(
            callCounts,
            "semaphore counter query",
            "vkGetSemaphoreCounterValue",
            "vkGetSemaphoreCounterValueKHR");
        AssertCommandFamilyObserved(
            callCounts, "swapchain acquisition", "vkAcquireNextImageKHR", "vkAcquireNextImage2KHR");
        AssertCommandFamilyObserved(callCounts, "swapchain presentation", "vkQueuePresentKHR");
        AssertCommandFamilyObserved(callCounts, "logical-device creation", "vkCreateDevice");
        AssertCommandFamilyObserved(callCounts, "device idle", "vkDeviceWaitIdle");

        string[] fenceCommands =
        [
            "vkCreateFence",
            "vkGetFenceStatus",
            "vkResetFences",
            "vkWaitForFences"
        ];
        Assert.True(
            fenceCommands.All(command => !callCounts.ContainsKey(command)),
            "Queue completion is timeline-semaphore owned; first-party fence operations require " +
            "an explicit synchronization-architecture update.");
    }

    private static void AssertCommandFamilyObserved(
        IReadOnlyDictionary<string, int> callCounts,
        string family,
        params string[] commands)
    {
        int count = commands.Sum(command => callCounts.GetValueOrDefault(command));
        Assert.True(
            count > 0,
            $"No first-party {family} VkResult command was found. Expected one of: " +
            string.Join(", ", commands));
    }

    private static HashSet<string> LoadVulkanResultCommands(string registryPath)
    {
        XDocument registry = XDocument.Load(registryPath, LoadOptions.None);
        XElement[] commands = registry
            .Descendants("commands")
            .Elements("command")
            .ToArray();
        var resultCommands = commands
            .Where(static command =>
                string.Equals(
                    (string?)command.Element("proto")?.Element("type"),
                    "VkResult",
                    StringComparison.Ordinal))
            .Select(static command => (string?)command.Element("proto")?.Element("name"))
            .Where(static name => !string.IsNullOrEmpty(name))
            .Select(static name => name!)
            .ToHashSet(StringComparer.Ordinal);

        bool addedAlias;
        do
        {
            addedAlias = false;
            foreach (XElement command in commands)
            {
                string? name = (string?)command.Attribute("name");
                string? alias = (string?)command.Attribute("alias");
                if (name != null && alias != null &&
                    resultCommands.Contains(alias) &&
                    resultCommands.Add(name))
                {
                    addedAlias = true;
                }
            }
        }
        while (addedAlias);

        return resultCommands;
    }

    private static string FindVulkanRegistry()
    {
        string? sdkRoot = Environment.GetEnvironmentVariable("VULKAN_SDK");
        if (!string.IsNullOrWhiteSpace(sdkRoot))
        {
            string configuredPath = Path.Combine(sdkRoot, "share", "vulkan", "registry", "vk.xml");
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        const string windowsSdkRoot = @"C:\VulkanSDK";
        if (Directory.Exists(windowsSdkRoot))
        {
            string? discoveredPath = Directory.EnumerateDirectories(windowsSdkRoot)
                .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
                .Select(static path => Path.Combine(path, "share", "vulkan", "registry", "vk.xml"))
                .FirstOrDefault(File.Exists);
            if (discoveredPath != null)
            {
                return discoveredPath;
            }
        }

        const string systemRegistry = "/usr/share/vulkan/registry/vk.xml";
        if (File.Exists(systemRegistry))
        {
            return systemRegistry;
        }

        throw new FileNotFoundException(
            "Could not locate Vulkan registry vk.xml. Install the Vulkan SDK or set VULKAN_SDK.");
    }

    private static bool HasExcludedSourceDirectory(string path)
    {
        return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(static segment =>
                segment.Equals("3rdparty", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Generated", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInsideInvocation(
        string source,
        int callStart,
        int callEnd,
        string enclosingFunction)
    {
        int searchStart = 0;
        while (TryFindIdentifier(source, enclosingFunction, searchStart, out int functionStart) &&
               functionStart < callStart)
        {
            int parameterStart = source.IndexOf(
                '(',
                functionStart + enclosingFunction.Length);
            if (parameterStart >= 0 && parameterStart < callStart)
            {
                int parameterEnd = FindMatchingDelimiter(source, parameterStart, '(', ')');
                if (parameterEnd >= callEnd && parameterStart < callStart)
                {
                    return true;
                }
            }

            searchStart = functionStart + enclosingFunction.Length;
        }

        return false;
    }

    private static bool IsPropagatedThroughAuditedBoundary(
        string source,
        string command,
        int callStart,
        int callEnd,
        string statementPrefix)
    {
        if (!Regex.IsMatch(
                statementPrefix,
                @"\breturn\b",
                RegexOptions.CultureInvariant))
        {
            return false;
        }

        if (IsInsideInvocation(source, callStart, callEnd, "EnumerateVkObjects"))
        {
            return true;
        }

        if (command != "vkQueueWaitIdle")
        {
            return false;
        }

        int scopeStart = FindEnclosingScopeStart(source, callStart);
        int signatureStart = FindStatementStart(source, scopeStart);
        string signature = source[signatureStart..scopeStart];
        return Regex.IsMatch(
            signature,
            @"\bRHIVkQueue::WaitIdleNoThrow\s*\([^)]*\)\s*noexcept\s*$",
            RegexOptions.CultureInvariant);
    }

    private static bool TryGetAssignedVariable(string statementPrefix, out string variable)
    {
        for (int index = statementPrefix.Length - 1; index >= 0; index--)
        {
            if (statementPrefix[index] != '=' ||
                index > 0 && statementPrefix[index - 1] is '=' or '!' or '<' or '>' ||
                index + 1 < statementPrefix.Length && statementPrefix[index + 1] == '=')
            {
                continue;
            }

            int nameEnd = index - 1;
            while (nameEnd >= 0 && char.IsWhiteSpace(statementPrefix[nameEnd]))
            {
                nameEnd--;
            }

            int nameStart = nameEnd;
            while (nameStart >= 0 && IsIdentifierCharacter(statementPrefix[nameStart]))
            {
                nameStart--;
            }

            nameStart++;
            if (nameStart <= nameEnd)
            {
                variable = statementPrefix[nameStart..(nameEnd + 1)];
                return true;
            }
        }

        variable = string.Empty;
        return false;
    }

    private static bool IsResultVariableObserved(
        string source,
        string variable,
        int callStart,
        int callEnd)
    {
        int scopeEnd = FindEnclosingScopeEnd(source, callStart);
        if (scopeEnd <= callEnd)
        {
            return false;
        }

        string remainingScope = source[(callEnd + 1)..scopeEnd];
        Match nextAssignment = Regex.Match(
            remainingScope,
            $@"\b{Regex.Escape(variable)}\b\s*=(?!=)",
            RegexOptions.CultureInvariant);
        string observation = nextAssignment.Success
            ? remainingScope[..nextAssignment.Index]
            : remainingScope;
        string escapedVariable = Regex.Escape(variable);

        return Regex.IsMatch(
                   observation,
                   $@"\b{escapedVariable}\b\s*(?:==|!=)\s*VK_[A-Z0-9_]+",
                   RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   observation,
                   $@"VK_[A-Z0-9_]+\s*(?:==|!=)\s*\b{escapedVariable}\b",
                   RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   observation,
                   $@"\bCheckVkResult\s*\(\s*{escapedVariable}\b",
                   RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   observation,
                   $@"\bswitch\s*\(\s*{escapedVariable}\b",
                   RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   observation,
                   $@"\breturn\s+{escapedVariable}\b",
                   RegexOptions.CultureInvariant);
    }

    private static int FindEnclosingScopeEnd(string source, int offset)
    {
        int scopeStart = FindEnclosingScopeStart(source, offset);
        return scopeStart < 0
            ? source.Length
            : FindMatchingDelimiter(source, scopeStart, '{', '}');
    }

    private static int FindEnclosingScopeStart(string source, int offset)
    {
        var scopeStack = new Stack<int>();
        for (int index = 0; index < offset; index++)
        {
            if (source[index] == '{')
            {
                scopeStack.Push(index);
            }
            else if (source[index] == '}' && scopeStack.Count > 0)
            {
                scopeStack.Pop();
            }
        }

        return scopeStack.Count == 0 ? -1 : scopeStack.Peek();
    }

    private static int FindStatementStart(string source, int offset)
    {
        for (int index = offset - 1; index >= 0; index--)
        {
            if (source[index] is ';' or '{' or '}')
            {
                return index + 1;
            }
        }

        return 0;
    }

    private static int FindMatchingDelimiter(
        string source,
        int openingIndex,
        char opening,
        char closing)
    {
        if (openingIndex < 0 || openingIndex >= source.Length || source[openingIndex] != opening)
        {
            return -1;
        }

        int depth = 0;
        for (int index = openingIndex; index < source.Length; index++)
        {
            if (source[index] == opening)
            {
                depth++;
            }
            else if (source[index] == closing && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static string FormatViolation(
        string sourceFile,
        string source,
        int offset,
        string command,
        string detail)
    {
        return $"{Path.GetFileName(sourceFile)}:{GetLineNumber(source, offset)} {command} {detail}.";
    }
}
