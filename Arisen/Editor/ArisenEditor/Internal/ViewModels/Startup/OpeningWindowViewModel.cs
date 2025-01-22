using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArisenEditor.ViewModels.Startup
{
    public class OpeningWindowViewModel : ViewModelBase
    {
        private double m_ProgressVaule = 0;
        internal double ProgressValue 
        {
            get { return m_ProgressVaule; }
            set { this.RaiseAndSetIfChanged(ref m_ProgressVaule, value); }
        }

        private string m_ProgressText = "Project Loading...";

        internal string ProgressText
        {
            get { return m_ProgressText; }
            set { this.RaiseAndSetIfChanged(ref m_ProgressText, value); }
        }

        public OpeningWindowViewModel(): base()
        {
            ProgressValue = 0;
        }

        internal Task UpdateProgress()
        {
            ProgressValue = 0;

            return Task.Run(() =>
            {
                for (int i = 0; i <= 100; i+=15)
                {
                    // Update ProgressBar.Value on the UI thread
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        ProgressValue = i;
                    });

                    // Simulate some work
                    Task.Delay(100).Wait();
                }
            });

        }
    }
}
