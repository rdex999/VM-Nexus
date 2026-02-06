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
using Shared.VirtualMachines;
using OperatingSystem = System.OperatingSystem;

namespace Client.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
	private DriveService _driveService;
	public SplitViewDisplayMode MenuDisplayMode { get; }
	
	public ObservableCollection<SideMenuItemTemplate> SideMenuItems { get; }
	public ObservableCollection<VmTabTemplate> VmTabs { get; }
	public ObservableCollection<PermissionItemTemplate> GrantPermissions { get; }

	public bool LoggedInAsUser { get; private set; }			/* If logged in as the user itself, not into a sub-user. */
	
	private const int SideMenuIdxHome = 0;
	private const int SideMenuIdxCreateVm = 1;
	private const int SideMenuIdxDrives = 2;
	private const int SideMenuIdxSubUsers = 3;
	private const int SideMenuIdxVmScreen = 4;
	
	[ObservableProperty]
	private VmTabTemplate? _selectedVmTab;
	
	[ObservableProperty] 
	private ViewModelBase _currentPageViewModel;

	[ObservableProperty]
	private SideMenuItemTemplate? _currentSideMenuItem;

	[ObservableProperty] 
	private string _accountMenuTitle;

	[ObservableProperty] 
	private bool _isSubUser;
	
	[ObservableProperty]
	private SubUser? _subUser = null;

	[ObservableProperty] 
	private UserPermissionItemTemplate[]? _ownerPermissions;

	[ObservableProperty] 
	private bool _resetPswdPopupResetEnabled = false;
	
	[ObservableProperty] 
	private bool _resetPswdPopupIsOpen = false;

	[ObservableProperty] 
	private bool _resetPswdPopupPasswordValid = false;
	
	[ObservableProperty] 
	private string _resetPswdPopupPassword = string.Empty;
	
	[ObservableProperty] 
	private string _resetPswdPopupPasswordMessage = string.Empty;
	
	[ObservableProperty] 
	private bool _resetPswdPopupNewPasswordValid = false;
	
	[ObservableProperty] 
	private string _resetPswdPopupNewPassword = string.Empty;
	
	[ObservableProperty] 
	private string _resetPswdPopupNewPasswordConfirm = string.Empty;
	
	[ObservableProperty] 
	private string _resetPswdPopupNewPasswordMessage = string.Empty;
	
	[ObservableProperty]
	private bool _canDeleteAccount;
	
	[ObservableProperty] 
	private bool _deleteAccountPopupIsOpen = false;
	
	[ObservableProperty] 
	private string _deleteAccountPopupEffects = string.Empty;
	
	[ObservableProperty] 
	private string _deleteAccountPopupConfirmation = string.Empty;

	[ObservableProperty] 
	private bool _prmsPopupIsOpen = false;
	
	/// <summary>
	/// Creates the MainViewModel object. Initializes UI.
	/// </summary>
	/// <param name="navigationSvc">
	/// An instance of the navigation service. navigationService != null.
	/// </param>
	/// <param name="clientSvc">
	/// an instance of the client service. clientService != null.
	/// </param>
	/// <remarks>
	/// Precondition: User has logged in or created an account successfully.
	/// navigationService != null &amp;&amp; clientService != null &amp;&amp; username != null. <br/>
	/// Postcondition: MainView UI is ready and initialized.
	/// </remarks>
	public MainPageViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.VmPoweredOn += OnVmPoweredOn;
		ClientSvc.VmPoweredOff += OnVmPoweredOff;
		ClientSvc.VmCrashed += OnVmCrashed;

		_driveService = new DriveService(ClientSvc);
		VmTabs = new ObservableCollection<VmTabTemplate>();
		GrantPermissions = new ObservableCollection<PermissionItemTemplate>();

		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
			MenuDisplayMode = SplitViewDisplayMode.Overlay;
		
		else
			MenuDisplayMode = SplitViewDisplayMode.CompactInline;
		
		SideMenuItems = new ObservableCollection<SideMenuItemTemplate>()
		{
			new SideMenuItemTemplate("Home", new HomeViewModel(NavigationSvc, ClientSvc, _driveService), "HomeRegular"),
			new SideMenuItemTemplate("Create a New Virtual Machine", new CreateVmViewModel(NavigationSvc,  ClientSvc, _driveService), "AddRegular"),
			new SideMenuItemTemplate("Drives", new DriveExplorerViewModel(NavigationSvc, ClientSvc, _driveService), "StorageRegular"),
			new SideMenuItemTemplate("Sub Users", new SubUsersViewModel(NavigationSvc, ClientSvc), "PeopleCommunityRegular"),
		};

		Initialize();
		
		((HomeViewModel)CurrentPageViewModel).VmOpenClicked += OnVmOpenClicked;
	}

	/* Use for IDE preview only. */
	public MainPageViewModel()
	{
		_driveService = null!;
		IsSubUser = true;
		SubUser = new SubUser(2, 1, 
		(UserPermissions.DriveItemList | UserPermissions.VirtualMachineList).AddIncluded(),
		"owner", "owner@gmail.com", "sub_user", "user@gmail.com", DateTime.Now);
		AccountMenuTitle = $"Welcome, {SubUser.Username}.";
		
		UserPermissions[] permissions = SubUser.OwnerPermissions.ToArray();
		OwnerPermissions = new UserPermissionItemTemplate[permissions.Length];
		for (int i = 0; i < permissions.Length; ++i)
			OwnerPermissions[i] = new UserPermissionItemTemplate(permissions[i]);

		GrantPermissions = new ObservableCollection<PermissionItemTemplate>();
		SideMenuItems = new ObservableCollection<SideMenuItemTemplate>()
		{
			new SideMenuItemTemplate("Home", new HomeViewModel(), "HomeRegular"),
			new SideMenuItemTemplate("Create a New Virtual Machine", new CreateVmViewModel(), "AddRegular"),
			new SideMenuItemTemplate("Drives", new DriveExplorerViewModel(), "StorageRegular"),
			new SideMenuItemTemplate("Sub Users", new SubUsersViewModel(), "PeopleCommunityRegular"),
		};
		CurrentSideMenuItem = SideMenuItems[SideMenuIdxHome];
		CurrentPageViewModel = SideMenuItems.First().ViewModel;

		VmTabs = new ObservableCollection<VmTabTemplate>()
		{
			new VmTabTemplate(new VmGeneralDescriptor(1, "test_vm1", Shared.VirtualMachines.OperatingSystem.MiniCoffeeOS, CpuArchitecture.X86_64,  VmState.Running, 5, BootMode.Uefi)),
			new VmTabTemplate(new VmGeneralDescriptor(2, "test_vm2", Shared.VirtualMachines.OperatingSystem.ManjaroLinux, CpuArchitecture.X86_64, VmState.Running, 4096, BootMode.Uefi)),
		};
		MenuDisplayMode = SplitViewDisplayMode.CompactInline;
	}

	/// <summary>
	/// Initializes this view model. <br/>
	/// Refreshes user related information and re-initializes the drive service.
	/// </summary>
	/// <remarks>
	/// Precondition: Either this view model was just created (called from constructor) or it was placed as the current page. (re-initialization is needed) <br/>
	/// Postcondition: This view model is initialized.
	/// </remarks>
	public void Initialize()
	{
		AccountMenuTitle = $"Welcome, {ClientSvc.User!.Username}.";
		IsSubUser = ClientSvc.User is SubUser;
		CanDeleteAccount = !ClientSvc.IsLoggedInAsSubUser || (ClientSvc.IsLoggedInAsSubUser &&
		                                                      ((SubUser)ClientSvc.User).OwnerPermissions.HasPermission(UserPermissions.UserDelete.AddIncluded()));
		LoggedInAsUser = !ClientSvc.IsLoggedInAsSubUser;
		
		if (IsSubUser)
		{
			SubUser = (SubUser)ClientSvc.User;
			
			UserPermissions[] permissions = SubUser.OwnerPermissions.ToArray();
			OwnerPermissions = new UserPermissionItemTemplate[permissions.Length];
			for (int i = 0; i < permissions.Length; ++i)
				OwnerPermissions[i] = new UserPermissionItemTemplate(permissions[i]);
		}
		CurrentSideMenuItem = SideMenuItems[SideMenuIdxHome];
		CurrentPageViewModel = SideMenuItems.First().ViewModel;
		
		VmTabs.Clear();
		
		_ = _driveService.InitializeAsync();
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
	private void OnVmOpenClicked(object? sender, VmGeneralDescriptor descriptor)
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
		CurrentSideMenuItem = SideMenuItems[SideMenuIdxVmScreen];
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
			CurrentSideMenuItem = SideMenuItems[SideMenuIdxHome];	/* Redirect to home page. */
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
			if (CurrentPageViewModel is VmScreenViewModel vmScreenViewModel)
			{
				await vmScreenViewModel.UnfocusAsync();
			}

			if (ClientSvc.User == null)
				NavigationSvc.NavigateToLogin();
			else
				NavigationSvc.NavigateToMainPage();
		}
	}

	/// <summary>
	/// Handles a click on the delete account button in the account sub-menu. Opens the account deletion popup.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the delete account button in the account sub-menu. <br/>
	/// Postcondition: The account deletion popup is displayed.
	/// </remarks>
	[RelayCommand]
	private void DeleteAccountClick()
	{
		DeleteAccountPopupEffects = $"{ClientSvc.User!.Username}'s sub-users will not be deleted, ";
		if (ClientSvc.User is SubUser subUser)
			DeleteAccountPopupEffects += $"their ownership will be transferred to {subUser.OwnerUsername}.";
		else
			DeleteAccountPopupEffects += "instead, they will be orphaned.";
		
		DeleteAccountPopupConfirmation = $"Are you sure you want to delete {ClientSvc.User!.Username}?";
		DeleteAccountPopupIsOpen = true;
	}

	/// <summary>
	/// Closes the account deletion popup.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the account deletion popup is closing or closing it is needed. <br/>
	/// Postcondition: The account deletion popup is closed.
	/// </remarks>
	[RelayCommand]
	private void CloseDeleteAccountPopup() => DeleteAccountPopupIsOpen = false;

	/// <summary>
	/// Handles a click on the delete button on the account deletion popup. Attempts to delete the account.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the delete button on the account deletion popup. <br/>
	/// Postcondition: An attempt to delete the account is performed. On success, the account is deleted.
	/// On failure, the account is not deleted.
	/// </remarks>
	[RelayCommand]
	private async Task DeleteAccountPopupDeleteAsync() => await ClientSvc.DeleteAccountAsync(ClientSvc.User!.Id);

	/// <summary>
	/// Handles a click on the reset password menu button. Opens the password reset popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the password reset button. <br/>
	/// Postcondition: The password reset popup is opened.
	/// </remarks>
	[RelayCommand]
	private void ResetPswdClick() => ResetPswdPopupIsOpen = true;

	/// <summary>
	/// Closes the password reset popup.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the password reset popup is closing or closing it is needed. <br/>
	/// Postcondition: The password reset popup is closed.
	/// </remarks>
	[RelayCommand]
	private void CloseResetPswdPopup()
	{
		ResetPswdPopupIsOpen = false;
		
		/* Clear password from memory. */
		ResetPswdPopupPassword = string.Empty;
		ResetPswdPopupNewPassword = string.Empty;
		ResetPswdPopupNewPasswordConfirm = string.Empty;
	}

	/// <summary>
	/// Handles a change in the current password field in the password reset popup. Validates the field and displays errors.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has changed the content of the current password field in the password reset popup. <br/>
	/// Postcondition: Success and error, messages and indications are displayed as needed.
	/// </remarks>
	public void OnResetPswdPopupPasswordChanged()
	{
		ResetPswdPopupPasswordValid = false;
		ResetPswdPopupPasswordMessage = string.Empty;

		if (string.IsNullOrEmpty(ResetPswdPopupPassword))
			ResetPswdPopupPasswordMessage = "Password cannot be empty.";
		else
			ResetPswdPopupPasswordValid = true;
		
		ResetPswdPopupValidate();
	}
	
	/// <summary>
	/// Handles a change in the new password and new password confirmation fields in the password reset popup. Validates the field and displays errors.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has changed the content of either the new password or new password confirmation fields in the password reset popup. <br/>
	/// Postcondition: Success and error, messages and indications are displayed as needed.
	/// </remarks>
	public void OnResetPswdPopupNewPasswordChanged()
	{
		ResetPswdPopupNewPasswordValid = false;
		ResetPswdPopupNewPasswordMessage = string.Empty;
		
		if (string.IsNullOrEmpty(ResetPswdPopupNewPassword))
			ResetPswdPopupNewPasswordMessage = "New password cannot be empty.";
		
		else if (string.IsNullOrEmpty(ResetPswdPopupNewPasswordConfirm) || ResetPswdPopupNewPassword != ResetPswdPopupNewPasswordConfirm)
			ResetPswdPopupNewPasswordMessage = "Passwords are not the same.";

		else
		{
			int strength = Common.PasswordStrength(ResetPswdPopupNewPassword);
			if (strength >= 5)
				ResetPswdPopupNewPasswordValid = true;
			
			else
				ResetPswdPopupNewPasswordMessage = $"Password is too weak. Password must be at least {SharedDefinitions.PasswordMinLength} characters long, " +
				                                   "include at least one symbol, number, upper-case, and lower-case characters.";
		}

		ResetPswdPopupValidate();
	}

	/// <summary>
	/// Validates the password reset fields, and enables the reset button if everything is valid.
	/// </summary>
	/// <remarks>
	/// Precondition: Validation of the password reset fields is needed. (For example, one of the fields has changed) <br/>
	/// Postcondition: If everything is valid, the reset button is enabled. Otherwise, the reset button is disabled.
	/// </remarks>
	private void ResetPswdPopupValidate()
	{
		ResetPswdPopupResetEnabled = !string.IsNullOrEmpty(ResetPswdPopupPassword)
		                             && !string.IsNullOrEmpty(ResetPswdPopupNewPassword) 
		                             && !string.IsNullOrEmpty(ResetPswdPopupNewPasswordConfirm)
		                             && ResetPswdPopupNewPassword == ResetPswdPopupNewPasswordConfirm;
	}

	/// <summary>
	/// Handles a click on the reset button, on the password reset popup. Attempts to reset the password.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the reset password button, on the password reset popup. <br/>
	/// Postcondition: An attempt to reset the password is performed. On success, the password is reset and the password reset popup is closed.
	/// On failure, the password is not reset and an error message is displayed.
	/// </remarks>
	[RelayCommand]
	private async Task ResetPswdPopupResetClick()
	{
		ResetPswdPopupNewPasswordValid = true;
		ResetPswdPopupPasswordValid = true;
		ResetPswdPopupPasswordMessage = string.Empty;
		ResetPswdPopupNewPasswordMessage = string.Empty;
		
		MessageResponseResetPassword.Status result = await ClientSvc.ResetPasswordAsync(ResetPswdPopupPassword, ResetPswdPopupNewPassword);

		if (result == MessageResponseResetPassword.Status.Success)
			CloseResetPswdPopup();
			
		else if (result == MessageResponseResetPassword.Status.InvalidPassword)
		{
			ResetPswdPopupPasswordValid = false;
			ResetPswdPopupResetEnabled = false;
			ResetPswdPopupPasswordMessage = "Invalid password.";
		}
	}

	/// <summary>
	/// Handles a click on the grant permissions button, in the permissions drop down.
	/// Opens the permission granting popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the grant permissions button, in the permissions drop down. <br/>
	/// Postcondition: The permission granting popup is open.
	/// </remarks>
	[RelayCommand]
	private void GrantPermissionsClick() => PrmsPopupIsOpen = true;
	
	/// <summary>
	/// Closes the permission granting popup.
	/// </summary>
	/// <remarks>
	/// Precondition: Either closing the permission granting popup is needed, or it is closing. <br/>
	/// Postcondition: The permission granting popup is closed.
	/// </remarks>
	[RelayCommand]
	private void ClosePrmsPopup() => PrmsPopupIsOpen = false;

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
		bool isExtended = SideMenuItems.Count >= SideMenuIdxVmScreen + 1;
		if (isExtended == extended)
			return;

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
		CurrentSideMenuItem = SideMenuItems[SideMenuIdxVmScreen];
		_ = ((VmScreenViewModel)CurrentPageViewModel).SwitchVirtualMachineAsync(value.Descriptor);
	}

	/// <summary>
	/// Handles the event of the virtual machine being powered on.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was powered on. id >= 1.</param>
	/// <remarks>
	/// Precondition: The virtual machine was powered on. id >= 1. <br/>
	/// Postcondition: Tabs updated accordingly.
	/// </remarks>
	private void OnVmPoweredOn(object? sender, int id)
	{
		foreach (VmTabTemplate tab in VmTabs)
		{
			if (tab.Descriptor.Id == id)
			{
				tab.Descriptor.State = VmState.Running;
				return;
			}
		}
	}
	
	/// <summary>
	/// Handles the event of the virtual machine being shut down
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was shut down. id >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was shut down. id >= 1. <br/>
	/// Postcondition: Tabs updated accordingly.
	/// </remarks>
	private void OnVmPoweredOff(object? sender, int id)
	{
		foreach (VmTabTemplate tab in VmTabs)
		{
			if (tab.Descriptor.Id == id)
			{
				tab.Descriptor.State = VmState.ShutDown;
				return;
			}
		}
	}

	/// <summary>
	/// Handles the event of the virtual machine crashing
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that has crashed. id >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine has crashed. id >= 1. <br/>
	/// Postcondition: Tabs updated accordingly.
	/// </remarks>
	private void OnVmCrashed(object? sender, int id) => OnVmPoweredOff(sender, id);
}

public partial class SideMenuItemTemplate : ObservableObject
{
	public string Title { get; }
	public ViewModelBase ViewModel { get; }
	public string IconKey { get; }
	public bool DefaultBackground { get; }

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
	public VmGeneralDescriptor Descriptor { get; set; }

	public VmTabTemplate(VmGeneralDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}

public partial class PermissionItemTemplate : ObservableObject
{
	public UserPermissions Permission { get; }
	public string PermissionString { get; }
	public string Description { get; }

	[ObservableProperty] 
	private bool _isChecked = false;
	
	public PermissionItemTemplate(UserPermissions permission)
	{
		Permission = permission;
		PermissionString = Common.SeparateStringWords(permission.ToString());
		Description = permission.Description();
	}
}