using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Networking;

namespace Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	public SplitViewDisplayMode MenuDisplayMode { get; }
	
	public ObservableCollection<SideMenuItemTemplate> SideMenuItems { get; }
	
	public ObservableCollection<VMTabTemplate> VMTabs { get; }
	
	[ObservableProperty] 
	private ViewModelBase _currentPageViewModel;

	[ObservableProperty]
	private SideMenuItemTemplate? _currentSideMenuItem;

	[ObservableProperty] 
	private string _accountMenuTitle;
	
	private string _username;
	
	
	
	public MainViewModel(NavigationService navigationService, ClientService clientService, string username)
		: base(navigationService, clientService)
	{
		_username = username;
		AccountMenuTitle = $"Welcome, {_username}.";
		CurrentPageViewModel = new HomeViewModel(navigationService, clientService);
		SideMenuItems = new ObservableCollection<SideMenuItemTemplate>()
		{
			new SideMenuItemTemplate("Home", new HomeViewModel(_navigationService, _clientService))
		};
		CurrentSideMenuItem = SideMenuItems[0];

		VMTabs = new ObservableCollection<VMTabTemplate>() { new VMTabTemplate("My first VM"), new VMTabTemplate("My second VM") };

		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
		{
			MenuDisplayMode = SplitViewDisplayMode.Overlay;
		}
		else
		{
			MenuDisplayMode = SplitViewDisplayMode.CompactInline;
		}
	}

	/* WARNING: ONLY FOR THE PREVIEWER IN THE IDE - DO NOT USE THIS */
	public MainViewModel() : base(new NavigationService(), new ClientService())
	{
	}

	partial void OnCurrentSideMenuItemChanged(SideMenuItemTemplate? value)
	{
		if (value == null)
			return;
		
		CurrentPageViewModel = value.ViewModel;
	}
	
	[RelayCommand]
	private void CloseVmTab(VMTabTemplate? value)
	{
		if (value == null)
			return;
		
		VMTabs.Remove(value);
	}

	[RelayCommand]
	private async Task LogoutAsync()
	{
		MessageResponseLogout.Status result = await _clientService.LogoutAsync();
		if (result == MessageResponseLogout.Status.Success || result == MessageResponseLogout.Status.UserNotLoggedIn)
		{
			_navigationService.NavigateToView(new LoginView() { DataContext = new LoginViewModel(_navigationService, _clientService) });
		}
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

public class VMTabTemplate
{
	public string Name { get; }

	public VMTabTemplate(string name)
	{
		Name = name;
	}
}