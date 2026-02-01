using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Server.Services;
using Shared;

namespace Server.ViewModels;

public partial class UsersViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;
	private readonly AccountService _accountService;
	
	public ObservableCollection<UserItemTemplate> Users { get; }
	
	[ObservableProperty]
	private string _query = string.Empty;
	
	[ObservableProperty]
	private bool _userDeletePopupIsOpen = false;
	
	[ObservableProperty]
	private string _userDeletePopupConfirmation = string.Empty;

	private User? _userDeletePopupUser = null;

	public UsersViewModel(DatabaseService databaseService, AccountService accountService)
	{
		_databaseService = databaseService;
		_accountService = accountService;
		Users = new ObservableCollection<UserItemTemplate>();
		_ = RefreshAsync();
	}

	/* Use for IDE preview only. */
	public UsersViewModel()
	{
		_databaseService = null!;
		_accountService = null!;
		Users = new ObservableCollection<UserItemTemplate>()
		{
			new UserItemTemplate(new User(1, "d", "d@gmail.com", DateTime.Now)),
			
			new UserItemTemplate(new SubUser(100, 1, UserPermissions.VirtualMachineCreate,"d", 
				"d@gmail.com", "child", "child@gmail.com", DateTime.Now)),
		};
		
		foreach (var user in Users)
			user.DeleteClicked += OnUserDeleteClicked;
	}

	/// <summary>
	/// Refreshes the current users list according to the set query. Handles a click on the refresh button too.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Either the user has clicked on the refresh button, or refreshing the users list is needed. <br/>
	/// Postcondition: On success, the list is updated according to latest data. On failure, the list is cleared.
	/// </remarks>
	[RelayCommand]
	public async Task<ExitCode> RefreshAsync()
	{
		Users.Clear();
		User[]? users = await _databaseService.SearchUsersAsync(Query);
		if (users == null)
			return ExitCode.DatabaseOperationFailed;

		foreach (User user in users)
		{
			Users.Add(new UserItemTemplate(user));
			Users.Last().DeleteClicked += OnUserDeleteClicked;
		}
		
		return ExitCode.Success;
	}

	/// <summary>
	/// Handles a change in the query field. Updates the users list according to the set query.
	/// </summary>
	/// <param name="value">Unused.</param>
	/// <remarks>
	/// Precondition: The query field has changed - the user has changed its content. <br/>
	/// Postcondition: A refresh of the users list is started according to the set query.
	/// </remarks>
	partial void OnQueryChanged(string value) => _ = RefreshAsync();

	/// <summary>
	/// Handles a click on a users delete button. Displays the user deletion popup.
	/// </summary>
	/// <param name="user">The user that the delete button was clicked upon. user != null.</param>
	/// <remarks>
	/// Precondition: The server user has clicked on the delete button of a user. user != null.<br/>
	/// Postcondition: The user deletion popup is displayed.
	/// </remarks>
	private void OnUserDeleteClicked(User user)
	{
		_userDeletePopupUser = user;
		UserDeletePopupConfirmation = $"Are you sure you want to delete {user.Username}?";
		UserDeletePopupIsOpen = true;
	}
}

public partial class UserItemTemplate
{
	public Action<User>? DeleteClicked;
	public bool IsSubUser => SubUser != null;
	public User User { get; }
	public SubUser? SubUser { get; }
	public UserPermissionItemTemplate[]? Permissions { get; }

	public UserItemTemplate(User user)
	{
		User = user;
		if (user is not SubUser subUser)
			return;
	
		SubUser = subUser;
		UserPermissions[] prms = subUser.OwnerPermissions.AddIncluded().ToArray();
		Permissions = new UserPermissionItemTemplate[Math.Max(prms.Length, 1)];
		for (int i = 0; i < prms.Length; ++i)
			Permissions[i] = new UserPermissionItemTemplate(prms[i]);

		if (prms.Length == 0)
			Permissions[0] = new UserPermissionItemTemplate(UserPermissions.None);
	}

	/// <summary>
	/// Handles a click on the delete button on a user. Displays the user deletion popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The server user has clicked on the delete button of a user. <br/>
	/// Postcondition: The user deletion popup is displayed.
	/// </remarks>
	[RelayCommand]
	private void DeleteClick() => DeleteClicked?.Invoke(User);
}

public class UserPermissionItemTemplate
{
	public string Permission { get; }
	public string Description { get; }

	public UserPermissionItemTemplate(UserPermissions permission)
	{
		Permission = Common.SeparateStringWords(permission.ToString());
		Description = permission.Description();
	}
}