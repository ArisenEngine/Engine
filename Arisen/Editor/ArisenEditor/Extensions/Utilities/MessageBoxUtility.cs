using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ArisenEditor.Utilities
{
    /// <summary>
    /// 
    /// </summary>
    public static class MessageBoxUtility
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="text"></param>
        /// <param name="enum"></param>
        /// <param name="icon"></param>
        /// <param name="windowStartupLocation"></param>
        public static async Task ShowMessageBoxStandard(string title, string text, ButtonEnum @enum = ButtonEnum.Ok, 
            Icon icon = Icon.None, WindowStartupLocation windowStartupLocation = WindowStartupLocation.CenterScreen)
        {
            var box = MessageBoxManager
            .GetMessageBoxStandard(title, text,
                @enum);
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                await box.ShowWindowDialogAsync(desktop.MainWindow);
            }
            else
            {
                await box.ShowAsync();
            }
        }
    }
}
