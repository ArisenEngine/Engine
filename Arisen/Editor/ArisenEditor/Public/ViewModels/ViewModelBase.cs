using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using ReactiveUI;

namespace ArisenEditor.ViewModels
{
	public interface IViewModel
	{
		public void OnLoaded();
		public void OnUnloaded();
		
	}
	public class ViewModelBase : ReactiveObject, IViewModel
	{
		public void OnLoaded()
		{
			throw new NotImplementedException();
		}

		public void OnUnloaded()
		{
			throw new NotImplementedException();
		}
	}
}