using System;
using System.Collections.ObjectModel;
using System.Net.Mime;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
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
	
	public ObservableCollection<VmTabTemplate> VMTabs { get; }
	
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
			new SideMenuItemTemplate("Home", new HomeViewModel(_navigationService, _clientService), "HomeRegular"),
			new SideMenuItemTemplate("Create a New Virtual Machine", new CreateVmViewModel(_navigationService,  _clientService), "AddRegular"),
		};
		CurrentSideMenuItem = SideMenuItems[0];

		VMTabs = new ObservableCollection<VmTabTemplate>() { new VmTabTemplate("My first VM"), new VmTabTemplate("My second VM") };

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
	
	public void CloseVmTab(VmTabTemplate? value)
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

public partial class SideMenuItemTemplate : ObservableObject
{
	public string Title { get; }
	public ViewModelBase ViewModel { get; }
	public string IconKey { get; }

	[ObservableProperty] 
	private Geometry? _icon;
	
	public SideMenuItemTemplate(string title, ViewModelBase viewModel, string iconGeometry)
	{
		Title = title;
		ViewModel = viewModel;
		IconKey = iconGeometry;
	}
}

public class VmTabTemplate
{
	public string Name { get; }

	public VmTabTemplate(string name)
	{
		Name = name;
	}
}