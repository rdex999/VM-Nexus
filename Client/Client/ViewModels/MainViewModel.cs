using System;
using Avalonia.Controls;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	[ObservableProperty] 
	private ViewModelBase _currentPageViewModel;

	public SplitViewDisplayMode MenuDisplayMode { get; }
	
	public MainViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
		CurrentPageViewModel = new HomeViewModel(navigationService, clientService);

		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
		{
			MenuDisplayMode = SplitViewDisplayMode.Overlay;
		}
		else
		{
			MenuDisplayMode = SplitViewDisplayMode.CompactInline;
		}
	}

}