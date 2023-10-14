using Avalonia.Controls.Templates;
using Avalonia.Controls;
using System;

using NebulaEditor.Utility;
using NebulaEditor.ViewModels;

namespace NebulaEditor.Views.DataTemplates
{
    public class StartupViewLocator : IDataTemplate
    {
      
        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }

        public Control? Build(object? param)
        {
            if (param== null)
            {
                MessageBoxUtility.ShowMessageBoxStandard("Exception", "startup view locator 's param is null");

                return null;
            }

            var name = param.GetType().FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }
            else
            {
                MessageBoxUtility.ShowMessageBoxStandard("Exception", $"can't find suitable view for type:{type}");

                return null;
            }

        }
    }
}