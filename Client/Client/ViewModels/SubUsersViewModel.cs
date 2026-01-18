using System;
using System.Collections.ObjectModel;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Networking;

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

	[ObservableProperty]
	private bool _newSubUserPopupCreateIsEnabled = false;
	
	public UserPermissionItemTemplate[] NewSubUserPopupPermissions { get; }
	
	public SubUsersViewModel(NavigationService navigationSvc, ClientService clientSvc) 
		: base(navigationSvc, clientSvc)
	{
		SubUsers = new ObservableCollection<SubUserItemTemplate>();
		ClientSvc.SubUserCreated += (sender, user) => SubUsers.Add(new SubUserItemTemplate(user));

		UserPermissions[] permissions = (Enum.GetValues(typeof(UserPermissions)) as UserPermissions[])!;
		NewSubUserPopupPermissions = new UserPermissionItemTemplate[permissions.Length - 1];	/* The "None" permission is not included. */
		for (int i = 0; i < NewSubUserPopupPermissions.Length; ++i)
		{
			NewSubUserPopupPermissions[i] = new UserPermissionItemTemplate(permissions[i + 1]);
			NewSubUserPopupPermissions[i].Checked += OnNewSubUserPopupPermissionChecked;
			NewSubUserPopupPermissions[i].UnChecked += OnNewSubUserPopupPermissionUnChecked;
		}
		
		_ = InitializeAsync();
	}

	public SubUsersViewModel()
	{
		SubUsers = new ObservableCollection<SubUserItemTemplate>()
		{
			new SubUserItemTemplate(new SubUser(2, 1, 
				(UserPermissions.VirtualMachineList | UserPermissions.DriveItemList).AddIncluded(),
				"owner", "owner@gmail.com", "user2", "user2@gmail.com", DateTime.Now)),
			
			new SubUserItemTemplate(new SubUser(3, 1, 
				(UserPermissions.VirtualMachineList | UserPermissions.DriveItemDelete).AddIncluded(),
				"owner", "owner@gmail.com", "user3", "user3@gmail.com", DateTime.Now)),
			
			new SubUserItemTemplate(new SubUser(4, 1, 
				(UserPermissions.VirtualMachineWatch | UserPermissions.DriveList).AddIncluded(),
				"owner", "owner@gmail.com", "user4", "some.long.email@gmail.com", DateTime.Now)),
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
		SubUser[]? subUsers = await ClientSvc.GetSubUsersAsync();
		if (subUsers == null)
			return;

		SubUsers.Clear();
		foreach (SubUser subUser in subUsers)
			SubUsers.Add(new SubUserItemTemplate(subUser));
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
	
	/// <summary>
	/// Handles a permission check event. Checks all required permissions for the checked permission.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has checked some permission in the permissions in the new sub-user creation popup. <br/>
	/// Postcondition: All required permissions for the checked permission are checked.
	/// </remarks>
	private void OnNewSubUserPopupPermissionChecked()
	{
		foreach (UserPermissionItemTemplate permission in NewSubUserPopupPermissions)
		{
			if (!permission.IsChecked)
				continue;
			
			UserPermissions[] included = permission.Permission.GetIncluded().ToArray();
			for (int i = 0; i < included.Length; ++i)
			{
				foreach (UserPermissionItemTemplate p in NewSubUserPopupPermissions)
				{
					if (p.Permission == included[i])
						p.IsChecked = true;
				}
			}
		}
	}
	
	/// <summary>
	/// Handles a permission uncheck event. Unchecks all permissions that require the unchecked permission.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has unchecked some permission in the permissions in the new sub-user creation popup. <br/>
	/// Postcondition: All permissions that require the unchecked permission are unchecked.
	/// </remarks>
	private void OnNewSubUserPopupPermissionUnChecked()
	{
		foreach (UserPermissionItemTemplate permission in NewSubUserPopupPermissions)
		{
			if (permission.IsChecked)
				continue;
			
			foreach (UserPermissionItemTemplate p in NewSubUserPopupPermissions)
			{
				if (p.Permission == permission.Permission || !p.IsChecked)
					continue;

				if (p.Permission.GetIncluded().HasPermission(permission.Permission))
					p.IsChecked = false;
			}
		}
	}

	/// <summary>
	/// Handles a change in the username field of the sub-user creation popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has changed the value of the username field. <br/>
	/// Postcondition: Errors and messages are displayed as needed. The create sub-user button is enabled if everything is valid.
	/// </remarks>
	public async Task OnNewSubUserPopupUsernameChangedAsync()
	{
		string username = NewSubUserPopupUsername.Trim();
		NewSubUserPopupUsernameMessage = string.Empty;
		NewSubUserPopupUsernameValid = false;
		
		if (string.IsNullOrEmpty(username))
			NewSubUserPopupUsernameMessage = "Username cannot be empty.";
		
		else if (!Common.IsValidUsername(username))
		{
			string invalidChars = string.Empty;
			for(int i = 0; i < SharedDefinitions.InvalidUsernameCharacters.Length; i++)
			{
				invalidChars += SharedDefinitions.InvalidUsernameCharacters[i];
				if (i == SharedDefinitions.InvalidUsernameCharacters.Length - 1)
					invalidChars += '.';
				else
					invalidChars += ", ";
			}
			NewSubUserPopupUsernameMessage = "Username cannot contain: " + invalidChars;
		}
		else
		{
			bool? available = await ClientSvc.IsUsernameAvailableAsync(username);
			
			if (available.HasValue && available.Value)
			{
				NewSubUserPopupUsernameValid = true;
				NewSubUserPopupUsernameMessage = $"Username {username} is available.";
			}
			else if(available.HasValue && !available.Value)
				NewSubUserPopupUsernameMessage = $"Username {username} is not available.";
		}
		
		SetupNewSubUserPopupCreateIsEnabled();
	}
	
	/// <summary>
	/// Handles a change in the email field of the sub-user creation popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has changed the value of the email field. <br/>
	/// Postcondition: Errors and messages are displayed as needed. The create sub-user button is enabled if everything is valid.
	/// </remarks>
	public void OnNewSubUserPopupEmailChanged()
	{
		string email = NewSubUserPopupEmail.Trim();
		NewSubUserPopupEmailValid = false;
		NewSubUserPopupEmailMessage = string.Empty;
		
		if (string.IsNullOrEmpty(email))
			NewSubUserPopupEmailMessage = "Email cannot be empty.";
		
		else if (!Common.IsValidEmail(email))
			NewSubUserPopupEmailMessage = "Invalid email address.";
		
		else
			NewSubUserPopupEmailValid = true;
		
		SetupNewSubUserPopupCreateIsEnabled();
	}

	/// <summary>
	/// Handles a change in the both password and password confirmation fields of the sub-user creation popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has changed the value of the password or password confirm fields. <br/>
	/// Postcondition: Errors and messages are displayed as needed. The create sub-user button is enabled if everything is valid.
	/// </remarks>
	public void OnNewSubUserPopupPasswordChanged()
	{
		NewSubUserPopupPasswordValid = false;
		NewSubUserPopupPasswordMessage = string.Empty;
		
		if (string.IsNullOrEmpty(NewSubUserPopupPassword))
			NewSubUserPopupPasswordMessage = "Password cannot be empty.";
		
		else if (string.IsNullOrEmpty(NewSubUserPopupPasswordConfirm) || NewSubUserPopupPassword != NewSubUserPopupPasswordConfirm)
			NewSubUserPopupPasswordMessage = "Passwords are not the same.";

		else
			NewSubUserPopupPasswordValid = true;
		
		SetupNewSubUserPopupCreateIsEnabled();
	}

	/// <summary>
	/// Checks if all input fields are valid, and enables the create sub-user button if everything is valid.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: If all input fields in the sub-user creation popup are valid, the create sub-user button is enbaled.
	/// Otherwise, the button is not enabled.
	/// </remarks>
	private void SetupNewSubUserPopupCreateIsEnabled()
	{
		NewSubUserPopupCreateIsEnabled = NewSubUserPopupUsernameValid && NewSubUserPopupEmailValid
		                                                              && NewSubUserPopupPasswordValid
		                                                              && !string.IsNullOrEmpty(NewSubUserPopupPassword)
		                                                              && !string.IsNullOrEmpty(NewSubUserPopupPasswordConfirm)
		                                                              && NewSubUserPopupPassword == NewSubUserPopupPasswordConfirm;
	}

	/// <summary>
	/// Handles a click on the create button in the sub-user creation popup. Creates the sub-user.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the create button in the sub-user creation popup. <br/>
	/// Postcondition: The sub-user is created according to the given data. (username, email, etc…)
	/// Failure should not happen as the button is only enabled if everything is valid.
	/// </remarks>
	[RelayCommand]
	private async Task CreateNewSubUserPopupClickAsync()
	{
		UserPermissions permissions = UserPermissions.None;
		foreach (UserPermissionItemTemplate p in NewSubUserPopupPermissions)
			if (p.IsChecked)
				permissions = permissions.AddPermission(p.Permission.AddIncluded());

		if (!permissions.IsValid())
		{
			CloseNewSubUserPopup();
			return;
		}
		
		/* Should always succeed because the button is only enabled if everything is valid. */
		await ClientSvc.CreateSubUserAsync(NewSubUserPopupUsername, NewSubUserPopupEmail, NewSubUserPopupPassword, permissions);
		CloseNewSubUserPopup();
	}
}

public class SubUserItemTemplate : ObservableObject
{
	public string UserName { get; }
	public string Email { get; }
	public UserPermissionItemTemplate[] Permissions { get; }		/* Owner's permissions over this sub-user. */
	public string Created { get; }
	
	public SubUserItemTemplate(SubUser user)
	{
		UserName = user.Username;
		Email = user.Email;
		Created = user.CreatedAt.ToString("dd/MM/yyyy");

		UserPermissions[] prms = user.OwnerPermissions.AddIncluded().ToArray();
		Permissions = new UserPermissionItemTemplate[Math.Max(prms.Length, 1)];
		for (int i = 0; i < prms.Length; ++i)
			Permissions[i] = new UserPermissionItemTemplate(prms[i]);

		if (prms.Length == 0)
			Permissions[0] = new UserPermissionItemTemplate(UserPermissions.None);
	}
}

public partial class UserPermissionItemTemplate : ObservableObject
{
	public Action? Checked;
	public Action? UnChecked;
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

	/// <summary>
	/// Handles the event that the permission was checked or unchecked. Raises events.
	/// </summary>
	/// <param name="value">The new value of the IsChecked property.</param>
	/// <remarks>
	/// Precondition: Either the user has checked or unchecked this permission. <br/>
	/// Postcondition: Event is handled, events are raised.
	/// </remarks>
	partial void OnIsCheckedChanged(bool value)
	{
		if (value)
			Checked?.Invoke();
		else
			UnChecked?.Invoke();
	}
}