using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Networking;

namespace Client.ViewModels;

public partial class SubUsersViewModel : ViewModelBase
{
	public ObservableCollection<SubUserItemTemplate> SubUsers { get; set; }

	public bool SubUserLoginIsEnabled { get; }
	
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
	
	[ObservableProperty]
	private bool _deleteSubUserPopupIsOpen = false;
	
	[ObservableProperty]
	private string _deleteSubUserPopupEffects = string.Empty;
	
	[ObservableProperty]
	private string _deleteSubUserPopupConfirmation = string.Empty;

	private int _deleteSubUserPopupUserId = -1;
	
	[ObservableProperty]
	private bool _removePrmsPopupIsOpen = false;
	
	private int _removePrmsPopupUserId = -1;
	
	public ObservableCollection<UserPermissionItemTemplate> RemovePrmsPopupPermissions { get; }
	
	public SubUsersViewModel(NavigationService navigationSvc, ClientService clientSvc) 
		: base(navigationSvc, clientSvc)
	{
		SubUsers = new ObservableCollection<SubUserItemTemplate>();
		RemovePrmsPopupPermissions = new ObservableCollection<UserPermissionItemTemplate>();
		ClientSvc.UserDeleted += OnUserDeleted;
		ClientSvc.SubUserCreated += OnSubUserCreated;
		ClientSvc.OwnerPermissionsChanged += OnOwnerPermissionsChanged;
		SubUserLoginIsEnabled = !ClientSvc.IsLoggedInAsSubUser;

		UserPermissions[] permissions = (Enum.GetValues(typeof(UserPermissions)) as UserPermissions[])!;
		NewSubUserPopupPermissions = new UserPermissionItemTemplate[permissions.Length - 1];	/* The "None" permission is not included. */
		for (int i = 0; i < NewSubUserPopupPermissions.Length; ++i)
			NewSubUserPopupPermissions[i] = new UserPermissionItemTemplate(permissions[i + 1], NewSubUserPopupPermissions);
		
		_ = InitializeAsync();
	}

	/* Use for IDE preview only. */
	public SubUsersViewModel()
	{
		SubUserLoginIsEnabled = true;
		RemovePrmsPopupPermissions = new ObservableCollection<UserPermissionItemTemplate>();
		SubUsers = new ObservableCollection<SubUserItemTemplate>()
		{
			new SubUserItemTemplate(new SubUser(2, 1, 
				(UserPermissions.UserDelete | UserPermissions.VirtualMachineUse | 
				 UserPermissions.DriveConnect | UserPermissions.DriveDisconnect | UserPermissions.DriveDelete | 
				 UserPermissions.DriveCreate | UserPermissions.DriveItemList | UserPermissions.DriveItemCreate | UserPermissions.DriveItemDelete |
				 UserPermissions.DriveItemDownload).AddIncluded(),
				
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
			NewSubUserPopupPermissions[i] = new UserPermissionItemTemplate(permissions[i + 1], NewSubUserPopupPermissions);
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
			OnSubUserCreated(null, subUser);
	}

	/// <summary>
	/// Handles a sub-user creation event, adds the new sub-user to the sub-users list and displaying him.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="subUser">The new sub-user. subUser != null.</param>
	/// <remarks>
	/// Precondition: A new sub-user was created. subUser != null. <br/>
	/// Postcondition: The new sub-user is added to the sub-users list and is displayed.
	/// </remarks>
	private void OnSubUserCreated(object? sender, SubUser subUser)
	{
		SubUsers.Add(new SubUserItemTemplate(subUser));
		SubUsers.Last().LoginClick += async void (user) => await LoginToSubUserAsync(user);
		SubUsers.Last().RemoveOwnerPermissionsClick += OnSubUserRemovePermissionsClick;
		SubUsers.Last().DeleteClick += OnSubUserDeleteClicked;
	}

	/// <summary>
	/// Handles a user deletion event.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="userId">The ID of the user that was deleted. userId >= 1.</param>
	/// <remarks>
	/// Precondition: Some user was deleted. userId >= 1. <br/>
	/// Postcondition: If the deleted user is a sub-user, the sub-user is removed.
	/// </remarks>
	private void OnUserDeleted(object? sender, int userId)
	{
		Dispatcher.UIThread.Post(() =>
		{
			for (int i = 0; i < SubUsers.Count; ++i)
			{
				if (SubUsers[i].SubUser.Id == userId)
				{
					SubUsers.RemoveAt(i);
					return;
				}
			}
		});
	}

	/// <summary>
	/// Handles the event that the owner's permissions over a user have changed.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="info">The permission change information. info != null.</param>
	/// <remarks>
	/// Precondition: The owner's permissions over the given user (in info) have changed. info != null. <br/>
	/// Postcondition: If the given user is one of the sub-users of the currently logged-in user, the sub-user data is updated.
	/// </remarks>
	private void OnOwnerPermissionsChanged(object? sender, MessageInfoOwnerPermissions info)
	{
		Dispatcher.UIThread.Post(() =>
		{
			for (int i = 0; i < SubUsers.Count; ++i)
			{
				if (SubUsers[i].SubUser.Id == info.UserId)
				{
					SubUser subUser = SubUsers[i].SubUser;
					SubUsers.RemoveAt(i);
					
					OnSubUserCreated(null, new SubUser(subUser.Id, subUser.OwnerId, info.Permissions, 
						subUser.OwnerUsername, subUser.OwnerEmail, subUser.Username, subUser.Email, subUser.CreatedAt
					));

					return;
				}
			}
		});
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
		{
			int strength = Common.PasswordStrength(NewSubUserPopupPassword);
			if (strength >= 5)
				NewSubUserPopupPasswordValid = true;
			
			else
				NewSubUserPopupPasswordMessage = $"Password is too weak. Password must be at least {SharedDefinitions.PasswordMinLength} characters long, " +
				                                   "include at least one symbol, number, upper-case, and lower-case characters.";
		}
		
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

	/// <summary>
	/// Close the sub-user deletion popup.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the sub-user deletion popup is closing, or closing it is needed. <br/>
	/// Postcondition: The sub-user deletion popup is closed.
	/// </remarks>
	[RelayCommand]
	private void CloseDeleteSubUserPopup() => DeleteSubUserPopupIsOpen = false;

	/// <summary>
	/// Handles a click on the remove permissions button of a sub-user. Displays the permission removing popup.
	/// </summary>
	/// <param name="subUser">The sub-user on which the remove permissions button was clicked. subUser != null.</param>
	/// <remarks>
	/// Precondition: The user has clicked on the remove permissions button on the sub-user. subUser != null. <br/>
	/// Postcondition: Displays the permission removing popup.
	/// </remarks>	
	private void OnSubUserRemovePermissionsClick(SubUser subUser)
	{
		RemovePrmsPopupPermissions.Clear();
		_removePrmsPopupUserId = subUser.Id;
		
		UserPermissions[] permissions = subUser.OwnerPermissions.ToArray();
		foreach (UserPermissions permission in permissions)
			RemovePrmsPopupPermissions.Add(new UserPermissionItemTemplate(permission, RemovePrmsPopupPermissions, true));
		
		RemovePrmsPopupIsOpen = true;
	}

	/// <summary>
	/// Closes the permission removing popup.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the user has closed the permission removing popup or it is closing. <br/>
	/// Postcondition: The permission removing popup is closed.
	/// </remarks>
	[RelayCommand]
	private void CloseRemovePrmsPopup() => RemovePrmsPopupIsOpen = false;

	/// <summary>
	/// Handles a click on the apply button in the permission removing popup.
	/// Removes unchecked permissions.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the apply button in the permission removing popup. <br/>
	/// Postcondition: The permissions are set.
	/// </remarks>
	[RelayCommand]
	private async Task ApplyRemovePrmsPopupClickAsync()
	{
		UserPermissions permissions = UserPermissions.None;
		foreach (UserPermissionItemTemplate pr in RemovePrmsPopupPermissions)
		{
			if (pr.IsChecked)
				permissions = permissions.AddPermission(pr.Permission);
		}

		await ClientSvc.SetOwnerPermissionsAsync(_removePrmsPopupUserId, permissions);
		
		CloseRemovePrmsPopup();
	}
	
	/// <summary>
	/// Handles a click on a sub-users delete button. Displays the sub-user deletion popup.
	/// </summary>
	/// <param name="subUser">The sub-user on which the delete button was clicked. subUser != null.</param>
	/// <remarks>
	/// Precondition: The user has clicked on the delete button of a sub-user. <br/>
	/// Postcondition: The sub-user deletion popup is displayed.
	/// </remarks>
	private void OnSubUserDeleteClicked(SubUser subUser)
	{
		DeleteSubUserPopupEffects = $"{subUser.Username}'s sub-users will not be deleted, " +
		                            $"their ownership will be transferred to you, {ClientSvc.User!.Username}.";
		
		DeleteSubUserPopupConfirmation = $"Are you sure you want to delete {subUser.Username}?";
		DeleteSubUserPopupIsOpen = true;
		_deleteSubUserPopupUserId = subUser.Id;
	}

	/// <summary>
	/// Attempts to delete the sub-user.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the delete button, on the sub-user deletion popup. <br/>
	/// Postcondition: An attempt to delete the sub-user is performed. On success, the sub-user is deleted.
	/// On failure, (which should not happen) the sub-user is not deleted.
	/// </remarks>
	[RelayCommand]
	private async Task DeleteSubUserPopupDeleteClickAsync() => await ClientSvc.DeleteAccountAsync(_deleteSubUserPopupUserId);
	
	/// <summary>
	/// Attempts to log in into the given sub-user's account.
	/// </summary>
	/// <param name="subUser">The sub-user to log in to. subUser != null.</param>
	/// <remarks>
	/// Precondition: The given sub-user is valid and is a sub-user of the current user, the current user is not logged in as a sub-user. subUser != null. <br/>
	/// Postcondition: On success, the user is logged in to the sub-user and is redirected to its account.
	/// On failure, the user is not logged in as the sub-user.
	/// </remarks>
	private async Task LoginToSubUserAsync(SubUser subUser)
	{
		bool accepted = await ClientSvc.LoginToSubUserAsync(subUser.Id);
		if (!accepted)
			return;
		
		NavigationSvc.NavigateToMainPage();
	}
}

public partial class SubUserItemTemplate : ObservableObject
{
	public Action<SubUser>? LoginClick;
	public Action<SubUser>? RemoveOwnerPermissionsClick;
	public Action<SubUser>? DeleteClick;
	public string UserName { get; }
	public string Email { get; }
	public UserPermissionItemTemplate[] Permissions { get; }		/* Owner's permissions over this sub-user. */
	public string Created { get; }
	public bool CanBeDeleted { get; }
	
	public readonly SubUser SubUser;
	
	public SubUserItemTemplate(SubUser user)
	{
		SubUser = user;
		UserName = user.Username;
		Email = user.Email;
		Created = user.CreatedAt.ToString("dd/MM/yyyy");
		CanBeDeleted = SubUser.OwnerPermissions.HasPermission(UserPermissions.UserDelete);

		UserPermissions[] prms = user.OwnerPermissions.AddIncluded().ToArray();
		Permissions = new UserPermissionItemTemplate[Math.Max(prms.Length, 1)];
		for (int i = 0; i < prms.Length; ++i)
			Permissions[i] = new UserPermissionItemTemplate(prms[i]);

		if (prms.Length == 0)
			Permissions[0] = new UserPermissionItemTemplate(UserPermissions.None);
	}

	/// <summary>
	/// Handles a click on the login button of a sub-user. Attempts to log in to the sub-user.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the login button on the sub-user. <br/>
	/// Postcondition: On success, the user is logged in as the sub-user. On failure, the user is not logged in as the sub-user.
	/// </remarks>
	[RelayCommand]
	private void LoginToSubUserClick() => LoginClick?.Invoke(SubUser);
	
	/// <summary>
	/// Handles a click on the remove permissions button of a sub-user. Displays the permission removing popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the remove permissions button on the sub-user. <br/>
	/// Postcondition: Displays the permission removing popup.
	/// </remarks>	
	[RelayCommand]
	private void RemovePermissionsClick() => RemoveOwnerPermissionsClick?.Invoke(SubUser);
	
	/// <summary>
	/// Handles a click on the delete button of a sub-user. Displays the sub-user deletion popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the delete button on the sub-user. <br/>
	/// Postcondition: Displays the sub-user deletion popup.
	/// </remarks>
	[RelayCommand]
	private void DeleteSubUserClick() => DeleteClick?.Invoke(SubUser);
}

public partial class UserPermissionItemTemplate : ObservableObject
{
	private readonly IEnumerable<UserPermissionItemTemplate>? _permissions;
	public UserPermissions Permission { get; }
	public string PermissionString { get; }
	public string Description { get; }

	[ObservableProperty] 
	private bool _isChecked = false;

	public UserPermissionItemTemplate(UserPermissions permission)
	{
		_permissions = null;
		Permission = permission;
		PermissionString = Common.SeparateStringWords(permission.ToString());
		Description = permission.Description();
	}
	
	public UserPermissionItemTemplate(UserPermissions permission, IEnumerable<UserPermissionItemTemplate> permissions, bool isChecked = false)
	{
		_permissions = permissions;
		Permission = permission;
		PermissionString = Common.SeparateStringWords(permission.ToString());
		Description = permission.Description();
		IsChecked = isChecked;
	}

	/// <summary>
	/// Handles the event that the permission was checked or unchecked.
	/// </summary>
	/// <param name="value">The new value of the IsChecked property.</param>
	/// <remarks>
	/// Precondition: Either the user has checked or unchecked this permission. <br/>
	/// Postcondition: Event is handled. Other permissions are checked and unchecked if needed.
	/// </remarks>
	partial void OnIsCheckedChanged(bool value)
	{
		if (_permissions == null)
			return;
		
		if (value)
		{
			foreach (UserPermissionItemTemplate permission in _permissions)
			{
				if (!permission.IsChecked)
					continue;
			
				UserPermissions[] included = permission.Permission.GetIncluded().ToArray();
				foreach (UserPermissions pr in included)
				{
					foreach (UserPermissionItemTemplate p in _permissions)
					{
						if (p.Permission == pr)
							p.IsChecked = true;
					}
				}
			}
		}
		else
		{
			foreach (UserPermissionItemTemplate permission in _permissions)
			{
				if (permission.IsChecked)
					continue;
			
				foreach (UserPermissionItemTemplate p in _permissions)
				{
					if (p.Permission == permission.Permission || !p.IsChecked)
						continue;

					if (p.Permission.GetIncluded().HasPermission(permission.Permission))
						p.IsChecked = false;
				}
			}
		}
	}
}