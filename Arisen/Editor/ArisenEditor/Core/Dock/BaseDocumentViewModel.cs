using System.Runtime.Serialization;
using Dock.Model.ReactiveUI.Controls;

namespace ArisenEditor.ViewModels;

[DataContract(IsReference = true)]
public class BaseDocumentViewModel : Document
{
    public BaseDocumentViewModel()
    {
        Id = "Window";
        Title = "Default";
    }
}