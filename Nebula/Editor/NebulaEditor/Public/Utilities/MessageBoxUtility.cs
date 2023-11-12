using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace NebulaEditor.Utilities
{
    public static class MessageBoxUtility
    {
        public static async Task ShowMessageBoxStandard(string title, string text, ButtonEnum @enum = ButtonEnum.Ok, 
            Icon icon = Icon.None, WindowStartupLocation windowStartupLocation = WindowStartupLocation.CenterScreen)
        {
            var box = MessageBoxManager
            .GetMessageBoxStandard(title, text,
                @enum);

            var task = box.ShowAsync();
            await task;
        }
    }
}
