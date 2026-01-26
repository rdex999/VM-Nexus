using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Server.Services;
using Shared;

namespace Server.ViewModels;

public partial class DriveConnectionsViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;

	[ObservableProperty]
	private string _query = string.Empty;
	
	public ObservableCollection<DriveConnectionItemTemplate> DriveConnections { get; }
	
	public DriveConnectionsViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		DriveConnections = new ObservableCollection<DriveConnectionItemTemplate>();
		_ = RefreshAsync();
	}

	/* Use for IDE preview only. */
	public DriveConnectionsViewModel()
	{
		_databaseService = null!;
		DriveConnections = new ObservableCollection<DriveConnectionItemTemplate>()
		{
			new DriveConnectionItemTemplate(1, "d", "test_vm0 - MiniCoffeeOS", 
				"test_vm0", 5, 6, DateTime.Now),
			
			new DriveConnectionItemTemplate(1, "d", "test_vm1 - Manjaro", 
				"test_vm1", 8, 9, DateTime.Now),
		};
	}
	
	/// <summary>
	/// Refreshes the current drive connections list according to the set query. Handles a click on the refresh button too.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Either the user has clicked on the refresh button, or refreshing the drive connections list is needed. <br/>
	/// Postcondition: On success, the list is updated according to latest data. On failure, the list is cleared.
	/// </remarks>
	[RelayCommand]
	public async Task<ExitCode> RefreshAsync()
	{
		DriveConnections.Clear();
		DatabaseService.SearchedDriveConnection[]? connections = await _databaseService.SearchDriveConnectionsAsync(Query);
		if (connections == null)
			return ExitCode.DatabaseOperationFailed;
		
		foreach (DatabaseService.SearchedDriveConnection connection in connections)
			DriveConnections.Add(new DriveConnectionItemTemplate(connection));
		
		return ExitCode.Success;
	}
	
	/// <summary>
	/// Handles a change in the query field. Updates the drive connections list according to the set query.
	/// </summary>
	/// <param name="value">Unused.</param>
	/// <remarks>
	/// Precondition: The query field has changed - the user has changed its content. <br/>
	/// Postcondition: A refresh of the drive connections list is started according to the set query.
	/// </remarks>
	partial void OnQueryChanged(string value) => _ = RefreshAsync();
}

public class DriveConnectionItemTemplate : DatabaseService.SearchedDriveConnection
{
	public DriveConnectionItemTemplate(int ownerId, string ownerUsername, string driveName, string virtualMachineName, 
		int driveId, int virtualMachineId, DateTime connectedAt) 
		: base(ownerId, ownerUsername, driveName, virtualMachineName, driveId, virtualMachineId, connectedAt)
	{
	}

	public DriveConnectionItemTemplate(DatabaseService.SearchedDriveConnection connection)
		: base(connection.OwnerId, connection.OwnerUsername, connection.DriveName, connection.VirtualMachineName,
			connection.DriveId, connection.VirtualMachineId, connection.ConnectedAt)
	{
	}
}