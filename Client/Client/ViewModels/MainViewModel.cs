using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	public SplitViewDisplayMode MenuDisplayMode { get; }
	
	public ObservableCollection<SideMenuItemTemplate> SideMenuItems { get; }
		
	[ObservableProperty] 
	private ViewModelBase _currentPageViewModel;

	[ObservableProperty]
	private SideMenuItemTemplate? _currentSideMenuItem;
	
	public MainViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
		CurrentPageViewModel = new HomeViewModel(navigationService, clientService);
		SideMenuItems = new ObservableCollection<SideMenuItemTemplate>()
		{
			new SideMenuItemTemplate("Home", new HomeViewModel(_navigationService, _clientService))
		};
		CurrentSideMenuItem = SideMenuItems[0];

		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
		{
			MenuDisplayMode = SplitViewDisplayMode.Overlay;
		}
		else
		{
			MenuDisplayMode = SplitViewDisplayMode.CompactInline;
		}
	}

	partial void OnCurrentSideMenuItemChanged(SideMenuItemTemplate? value)
	{
		if (value == null)
			return;
		
		CurrentPageViewModel = value.ViewModel;
	}
}

public class SideMenuItemTemplate
{
	public string Title { get; }
	public ViewModelBase ViewModel { get; }

	public SideMenuItemTemplate(string title, ViewModelBase viewModel)
	{
		Title = title;
		ViewModel = viewModel;
	}
}