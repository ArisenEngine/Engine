using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArisenKernel.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArisenLauncher.ViewModels;

public partial class PackageManagerViewModel
{
    public ObservableCollection<ServiceCapabilityViewModel> Capabilities { get; } = new();

    private void InitializeServices()
    {
        Capabilities.Clear();

        // 1. Data-Driven Service Discovery Engine
        // Reflect over all kernel interfaces matching [ServiceContract]
        Type attrType = typeof(ServiceContractAttribute);
        var kernelTypes = typeof(IWindowProvider).Assembly.GetTypes();

        foreach (var type in kernelTypes)
        {
            if (type.IsInterface)
            {
                var attr = (ServiceContractAttribute?)type.GetCustomAttribute(attrType);
                if (attr != null)
                {
                    Capabilities.Add(new ServiceCapabilityViewModel
                    {
                        ContractName = type.FullName ?? type.Name,
                        FriendlyName = attr.Name,
                        Description = attr.Description
                    });
                }
            }
        }
    }

    private void RefreshServiceStatus()
    {
        // Reset all Capabilities
        foreach (var cap in Capabilities)
        {
            cap.IsProvided = false;
            cap.IsRequired = false;
            cap.ProvidingPackages.Clear();
            cap.RequiringPackages.Clear();
        }

        var dynamicCapabilities = new Dictionary<string, ServiceCapabilityViewModel>();

        // 2. Automated Validation Table Scanning
        foreach (var pkg in Packages)
        {
            if (pkg.Url != null && pkg.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                string localPath = Uri.UnescapeDataString(new Uri(pkg.Url).LocalPath);
                string packageJsonPath = Path.Combine(localPath, "package.json");
                
                if (File.Exists(packageJsonPath))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
                        if (doc.RootElement.TryGetProperty("services", out var servicesProp))
                        {
                            if (servicesProp.TryGetProperty("provides", out var providesArray))
                            {
                                foreach (var element in providesArray.EnumerateArray())
                                {
                                    string contract = string.Empty;
                                    if (element.ValueKind == JsonValueKind.String)
                                        contract = element.GetString() ?? "";
                                    else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("interface", out var intfProp))
                                        contract = intfProp.GetString() ?? "";

                                    if (!string.IsNullOrEmpty(contract))
                                    {
                                        var cap = GetOrCreateCapability(contract, dynamicCapabilities);
                                        cap.IsProvided = true;
                                        if (!cap.ProvidingPackages.Contains(pkg.Id))
                                            cap.ProvidingPackages.Add(pkg.Id);
                                    }
                                }
                            }

                            if (servicesProp.TryGetProperty("requires", out var requiresArray))
                            {
                                foreach (var element in requiresArray.EnumerateArray())
                                {
                                    string contract = element.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(contract))
                                    {
                                        var cap = GetOrCreateCapability(contract, dynamicCapabilities);
                                        cap.IsRequired = true;
                                        if (!cap.RequiringPackages.Contains(pkg.Id))
                                            cap.RequiringPackages.Add(pkg.Id);
                                    }
                                }
                            }
                        }
                    }
                    catch { /* Ignore corrupt JSON fragments during active editing */ }
                }
            }
        }
    }

    private ServiceCapabilityViewModel GetOrCreateCapability(string contract, Dictionary<string, ServiceCapabilityViewModel> dynamicCache)
    {
        var existing = Capabilities.FirstOrDefault(c => c.ContractName.EndsWith(contract, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        if (dynamicCache.TryGetValue(contract, out var cached)) return cached;

        // 3. Regex-Based Contract Aliasing for User-Defined Contracts
        var friendlyName = Regex.Match(contract, @"[^.]+$").Value;
        
        var newCap = new ServiceCapabilityViewModel
        {
            ContractName = contract, // Keep native path for manifest exact matching
            FriendlyName = friendlyName,
            Description = "User-defined capability dynamically resolved."
        };
        
        Capabilities.Add(newCap);
        dynamicCache[contract] = newCap;
        return newCap;
    }
}

public partial class ServiceCapabilityViewModel : ObservableObject
{
    [ObservableProperty] private string _contractName = string.Empty;
    [ObservableProperty] private string _friendlyName = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotSatisfied))]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    private bool _isProvided;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsNotSatisfied))]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    private bool _isRequired;

    public bool IsNotSatisfied => IsRequired && !IsProvided;

    public string BackgroundColor 
    {
        get
        {
            if (IsNotSatisfied) return "#ef444420"; // Red Warn
            if (IsProvided && IsRequired) return "#22c55e20"; // Green Success
            return "#1a1e26"; // Default Grey
        }
    }

    public ObservableCollection<string> ProvidingPackages { get; } = new();
    public ObservableCollection<string> RequiringPackages { get; } = new();
}
