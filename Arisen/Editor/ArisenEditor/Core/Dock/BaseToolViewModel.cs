using System.Runtime.Serialization;
using Dock.Model.Mvvm.Controls;

namespace ArisenEditor.ViewModels;

[DataContract(IsReference = true)]

public class BaseToolViewModel : Tool
{
    
}