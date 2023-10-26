using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.ViewModels.Startup
{
    public class OpeningWindowViewModel : ViewModelBase
    {
        private double m_ProgressVaule = 0;
        public double ProgressValue 
        {
            get { return m_ProgressVaule; }
            set { this.RaiseAndSetIfChanged(ref m_ProgressVaule, value); }
        }

        public OpeningWindowViewModel()
        {
            ProgressValue = 0;
        }

        public Task UpdateProgress()
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
