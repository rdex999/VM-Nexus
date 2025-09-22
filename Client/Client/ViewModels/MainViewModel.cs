using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Networking;

namespace Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	public SplitViewDisplayMode MenuDisplayMode { get; }
	
	public ObservableCollection<SideMenuItemTemplate> SideMenuItems { get; }
	
	public ObservableCollection<VmTabTemplate> VmTabs { get; }
	
	[ObservableProperty]
	private VmTabTemplate? _selectedVmTab;
	
	[ObservableProperty] 
	private ViewModelBase _currentPageViewModel;

	[ObservableProperty]
	private SideMenuItemTemplate? _currentSideMenuItem;

	[ObservableProperty] 
	private string _accountMenuTitle;

	/// <summary>
	/// Creates the MainViewModel object. Initializes UI.
	/// </summary>
	/// <param name="navigationSvc">
	/// An instance of the navigation service. navigationService != null.
	/// </param>
	/// <param name="clientSvc">
	/// an instance of the client service. clientService != null.
	/// </param>
	/// <param name="username">
	/// The username of the user. username != null.
	/// </param>
	/// <remarks>
	/// Precondition: User has logged in or created an account successfully.
	/// navigationService != null &amp;&amp; clientService != null &amp;&amp; username != null. <br/>
	/// Postcondition: MainView UI is ready and initialized.
	/// </remarks>
	public MainViewModel(NavigationService navigationSvc, ClientService clientSvc, string username)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.VmDataUpdated += OnVmDataUpdated;
		
		AccountMenuTitle = $"Welcome, {username}.";
		
		SideMenuItems = new ObservableCollection<SideMenuItemTemplate>()
		{
			new SideMenuItemTemplate("Home", new HomeViewModel(NavigationSvc, ClientSvc), "HomeRegular"),
			new SideMenuItemTemplate("Create a New Virtual Machine", new CreateVmViewModel(NavigationSvc,  ClientSvc), "AddRegular"),
		};
		CurrentSideMenuItem = SideMenuItems[0];
		CurrentPageViewModel = SideMenuItems.First().ViewModel;
		((HomeViewModel)CurrentPageViewModel).VmOpenClicked += OnVmOpenClicked;

		VmTabs = new ObservableCollection<VmTabTemplate>();

		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
		{
			MenuDisplayMode = SplitViewDisplayMode.Overlay;
		}
		else
		{
			MenuDisplayMode = SplitViewDisplayMode.CompactInline;
		}
	}
	
	/// <summary>
	/// Handles a click on one of the VMs Open button. If no open tab for the VM exists, create a new tab. If there is a tab, redirect to it.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="descriptor">
	/// A descriptor of the VM that the open button of was clicked. descriptor != null.
	/// </param>
	/// <remarks>
	/// Precondition: User has clicked the Open button on one of its VMs. descriptor != null. <br/>
	/// Postcondition: If no open tab for the VM exists, create a new tab. If exists, redirect to it.
	/// </remarks>
	private void OnVmOpenClicked(object? sender, SharedDefinitions.VmGeneralDescriptor descriptor)
	{
		/* Check if a tab is already open for this VM */
		foreach (VmTabTemplate vm in VmTabs)
		{
			if (vm.Descriptor.Id == descriptor.Id)		/* If found a that for this VM, redirect to it. */
			{
				SelectedVmTab = vm;
				return;
			}
		}
		
		/* Here we know there is no open tab for this VM, so create one */
		VmTabs.Add(new VmTabTemplate(descriptor));
		SelectedVmTab = VmTabs.Last();
		SideMenuMode(true);
		CurrentSideMenuItem = SideMenuItems[2];
	}

	/// <summary>
	/// Handles when the CurrentSideMenuItem property is changed.
	/// </summary>
	/// <param name="value">
	/// The new side menu item that is selected. value != null.
	/// </param>
	/// <remarks>
	/// Precondition: User has selected another tab in the side menu. value != null. <br/>
	/// Postcondition: The current page view model (The main content) is changed to that of the new side menu tab. This results in the view changing.
	/// </remarks>
	partial void OnCurrentSideMenuItemChanged(SideMenuItemTemplate? value)
	{
		if (value == null)
			return;

		if (CurrentPageViewModel is VmScreenViewModel && value.ViewModel is not VmScreenViewModel)
		{
			_ = ((VmScreenViewModel)CurrentPageViewModel).UnfocusAsync();
		}
		else if (value.ViewModel is VmScreenViewModel && CurrentPageViewModel is not VmScreenViewModel)
		{
			_ = ((VmScreenViewModel)value.ViewModel).FocusAsync();
		}
		
		CurrentPageViewModel = value.ViewModel;
	}

	/// <summary>
	/// Handles when a VM tab is fully closed, after the animation has finished. (Called by the code-behind)
	/// </summary>
	/// <param name="value">
	/// The VM tab that was closed. value != null.
	/// </param>
	/// <remarks>
	/// Precondition: Animation of the tab closing has finished, code-behind calls this method. value != null. <br/>
	/// Postcondition: The VM tab is removed from the VmTabs collection. (The VM tab is officially closed)
	/// </remarks>
	public void CloseVmTab(VmTabTemplate? value)
	{
		if (value == null)
			return;

		if (SelectedVmTab == value)
		{
			CurrentSideMenuItem = SideMenuItems.First();	/* Redirect to home page. */
			SideMenuMode(false);
		}
		
		VmTabs.Remove(value);
	}

	/// <summary>
	/// Handles a click on the logout button - attempts to log out.
	/// </summary>
	/// <remarks>
	/// Precondition: User clicks on the logout button. <br/>
	/// Postcondition: On success, the user is logged out and redirected to the login page.
	/// (This includes the case in which the user was not considered as logged in by the server - still redirected to the login page) <br/>
	/// On failure, the click will have no effect, and the user will stay logged in.
	/// </remarks>
	[RelayCommand]
	private async Task LogoutAsync()
	{
		MessageResponseLogout.Status result = await ClientSvc.LogoutAsync();
		if (result == MessageResponseLogout.Status.Success || result == MessageResponseLogout.Status.UserNotLoggedIn)
		{
			NavigationSvc.NavigateToView(new LoginView() { DataContext = new LoginViewModel(NavigationSvc, ClientSvc) });
		}
	}

	/// <summary>
	/// Change the mode of the side menu. Not extended is for when there is no selected VM tab, and extended is for when there is a tab selected.
	/// The extended mode includes more side menu items, such as a screen view of the VM.
	/// </summary>
	/// <param name="extended">Whether to set the mode to extended (true) or not extended. (false)</param>
	/// <remarks>
	/// Precondition: The user has opened or selected a VM tab, or he has closed its currently selected tab. <br/>
	/// Postcondition: The side menu has removed or added side menu items based on whether extended is true or false.
	/// </remarks>
	private void SideMenuMode(bool extended)
	{
		if ((extended && SideMenuItems.Count != 2) || (!extended && SideMenuItems.Count == 2))
		{
			return;
		}

		if (extended)
		{
			SideMenuItems.Add(new SideMenuItemTemplate("Screen", new VmScreenViewModel(NavigationSvc, ClientSvc), "DesktopRegular"));
		}
		else
		{
			SideMenuItems.RemoveAt(SideMenuItems.Count - 1);
		}
	}

	/// <summary>
	/// Called when the currently selected tab has changed. Enables the extended side menu and redirects to the VM screen view page.
	/// </summary>
	/// <param name="value">The new VM tab that was selected. value != null.</param>
	/// <remarks>
	/// Precondition: User has selected another tab. value != null. <br/>
	/// Postcondition: The extended side menu is enabled (if not already) and the user is redirected to the VM screen view page.
	/// </remarks>
	partial void OnSelectedVmTabChanged(VmTabTemplate? value)
	{
		if (value == null)
		{
			return;
		}
		
		SideMenuMode(true);
		CurrentSideMenuItem = SideMenuItems[2];
		_ = ((VmScreenViewModel)CurrentPageViewModel).SwitchVirtualMachineAsync(value.Descriptor);
	}

	/// <summary>
	/// Handles a change in a virtual machines data.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="descriptor">The descriptor of the virtual machine of which the data was updated. descriptor != null.</param>
	/// <remarks>
	/// Precondition: The data of a virtual machine was updated. descriptor != null. <br/>
	/// Postcondition: The data change is handled.
	/// </remarks>
	private void OnVmDataUpdated(object? sender, SharedDefinitions.VmGeneralDescriptor descriptor)
	{
		foreach (VmTabTemplate tab in VmTabs)
		{
			if (tab.Descriptor.Id == descriptor.Id)
			{
				tab.Descriptor = descriptor;
				return;
			}
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

	/// <summary>
	/// Creates an instance of SideMenuItemTemplate.
	/// </summary>
	/// <param name="title">
	/// The title of the side menu item, for example, "Home" or "Create a New Virtual Machine". title != null.
	/// </param>
	/// <param name="viewModel">
	/// The view model of the page that this side menu item corresponds to. (What page will be shown when this side menu item is selected) viewModel != null.
	/// </param>
	/// <param name="iconGeometry">
	/// The name of the icon geometry resource - defined in /Client/Client/Resources/Geometries.axaml. (The icon of the side menu item) iconGeometry != null.
	/// </param>
	/// <remarks>
	/// Precondition: MainViewModel is created. <br/>
	/// title != null &amp;&amp; viewModel != null &amp;&amp; iconGeometry != null. <br/>
	/// Postcondition: An object of type SideMenuItemTemplate is created.
	/// </remarks>
	public SideMenuItemTemplate(string title, ViewModelBase viewModel, string iconGeometry)
	{
		Title = title;
		ViewModel = viewModel;
		IconKey = iconGeometry;
		Icon = null;
	}
}

public class VmTabTemplate
{
	public SharedDefinitions.VmGeneralDescriptor Descriptor { get; set; }

	public VmTabTemplate(SharedDefinitions.VmGeneralDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}