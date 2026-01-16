using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;

namespace Client.ViewModels;

public partial class SubUsersViewModel : ViewModelBase
{
	public ObservableCollection<SubUserItemTemplate> SubUsers { get; set; }

	[ObservableProperty]
	private bool _newSubUserPopupIsOpen = false;
	
	[ObservableProperty]
	private string _newSubUserPopupUsername = string.Empty;
	
	[ObservableProperty]
	private bool _newSubUserPopupUsernameValid = false;	
	
	[ObservableProperty]	
	private string _newSubUserPopupUsernameMessage = string.Empty;
	
	[ObservableProperty]
	private string _newSubUserPopupEmail = string.Empty;
	
	[ObservableProperty]
	private bool _newSubUserPopupEmailValid = false;
	
	[ObservableProperty]
	private string _newSubUserPopupEmailMessage = string.Empty;
	
	[ObservableProperty]
	private string _newSubUserPopupPassword = string.Empty;
	
	[ObservableProperty]
	private string _newSubUserPopupPasswordConfirm = string.Empty;
	
	[ObservableProperty]
	private bool _newSubUserPopupPasswordValid = false;	
	
	[ObservableProperty]
	private string _newSubUserPopupPasswordMessage = string.Empty;
	
	public UserPermissionItemTemplate[] NewSubUserPopupPermissions { get; }
	
	public SubUsersViewModel(NavigationService navigationSvc, ClientService clientSvc) 
		: base(navigationSvc, clientSvc)
	{
		SubUsers = new ObservableCollection<SubUserItemTemplate>();

		UserPermissions[] permissions = (Enum.GetValues(typeof(UserPermissions)) as UserPermissions[])!;
		NewSubUserPopupPermissions = new UserPermissionItemTemplate[permissions.Length - 1];	/* The "None" permission is not included. */
		for (int i = 0; i < NewSubUserPopupPermissions.Length; ++i)
			NewSubUserPopupPermissions[i] = new UserPermissionItemTemplate(permissions[i + 1]);
		
		_ = InitializeAsync();
	}

	public SubUsersViewModel()
	{
		SubUsers = new ObservableCollection<SubUserItemTemplate>()
		{
			new SubUserItemTemplate("hey0", UserPermissions.DriveList, new DateOnly(1999, 12, 31)),
			new SubUserItemTemplate("hey1", UserPermissions.DriveItemDownload,  new DateOnly(1999, 12, 31)),
			new SubUserItemTemplate("user2", UserPermissions.VirtualMachineUse,  new DateOnly(1999, 5, 4)),
		};
		
		UserPermissions[] permissions = (Enum.GetValues(typeof(UserPermissions)) as UserPermissions[])!;
		NewSubUserPopupPermissions = new UserPermissionItemTemplate[permissions.Length - 1];	/* The "None" permission is not included. */
		for (int i = 0; i < NewSubUserPopupPermissions.Length; ++i)
			NewSubUserPopupPermissions[i] = new UserPermissionItemTemplate(permissions[i + 1]);
	}

	/// <summary>
	/// Initializes this view model. Fetches sub-users and displays them.
	/// </summary>
	/// <remarks>
	/// Precondition: User is logged in. <br/>
	/// Postcondition: On success, the users are fetched and displayed. On failure, the users are not displayed. (Failure should not happen)
	/// </remarks>
	private async Task InitializeAsync()
	{
		User[]? subUsers = await ClientSvc.GetSubUsersAsync();
		if (subUsers == null)
			return;

		SubUsers.Clear();
		foreach (var subUser in subUsers)
			SubUsers.Add(new SubUserItemTemplate(subUser.Username, subUser.OwnerPermissions, DateOnly.FromDateTime(subUser.CreatedAt)));
	}

	/// <summary>
	/// Handles a click on the create sub user button. Opens the sub-user creation popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the create sub-user button. <br/>
	/// Postcondition: The sub-user creation popup is opened.
	/// </remarks>
	[RelayCommand]
	private void CreateSubUserClick()
	{
		NewSubUserPopupUsername = string.Empty;
		NewSubUserPopupUsernameValid = false;
		NewSubUserPopupUsernameMessage = "Username cannot be empty.";
		
		NewSubUserPopupEmail = string.Empty;
		NewSubUserPopupEmailValid = false;
		NewSubUserPopupEmailMessage = "Email cannot be empty.";
		
		NewSubUserPopupPassword = string.Empty;
		NewSubUserPopupPasswordConfirm = string.Empty;
		NewSubUserPopupPasswordValid = false;
		NewSubUserPopupPasswordMessage = "Password cannot be empty.";
	
		foreach (UserPermissionItemTemplate permission in NewSubUserPopupPermissions)
			permission.IsChecked = false;
		
		NewSubUserPopupIsOpen = true;
	}

	/// <summary>
	/// Handles both closing the sub-user creation popup, and the event that it was closed.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the user has closed the popup, or closing it is required. <br/>
	/// Postcondition: The sub-user creation popup is closed.
	/// </remarks>
	[RelayCommand]
	private void CloseNewSubUserPopup() => NewSubUserPopupIsOpen = false;
}

public class SubUserItemTemplate : ObservableObject
{
	public string UserName { get; }
	public UserPermissionItemTemplate[] Permissions { get; }		/* Owner's permissions over this sub-user. */
	public string Created { get; }
	
	public SubUserItemTemplate(string userName, UserPermissions permissions, DateOnly created)
	{
		UserName = userName;
		Created = created.ToString("dd/MM/yyyy");

		UserPermissions[] prms = permissions.AddIncluded().ToArray();
		Permissions = new UserPermissionItemTemplate[Math.Max(prms.Length, 1)];
		for (int i = 0; i < prms.Length; ++i)
			Permissions[i] = new UserPermissionItemTemplate(prms[i]);

		if (prms.Length == 0)
			Permissions[0] = new UserPermissionItemTemplate(UserPermissions.None);
	}
}

public partial class UserPermissionItemTemplate : ObservableObject
{
	public UserPermissions Permission { get; }
	public string PermissionString { get; }
	public string Description { get; }

	[ObservableProperty] 
	private bool _isChecked = false;

	public UserPermissionItemTemplate(UserPermissions permission)
	{
		Permission = permission;
		PermissionString = Common.SeparateStringWords(permission.ToString());
		Description = permission.Description();
	}
}