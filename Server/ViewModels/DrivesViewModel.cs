using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Server.Services;
using Shared;
using Shared.Drives;

namespace Server.ViewModels;

public partial class DrivesViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;
	
	public ObservableCollection<DriveItemTemplate> Drives { get; }

	[ObservableProperty]
	private string _query = string.Empty;

	public DrivesViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		Drives = new ObservableCollection<DriveItemTemplate>();
		_ = RefreshAsync();
	}

	/* Use for IDE preview only. */
	public DrivesViewModel()
	{
		_databaseService = null!;

		Drives = new ObservableCollection<DriveItemTemplate>()
		{
			new DriveItemTemplate(new DatabaseService.SearchedDrive(1, 5, "d", "test_vm0 - MiniCoffeeOS", 13, DriveType.Floppy)),
			new DriveItemTemplate(new DatabaseService.SearchedDrive(2, 6, "david", "test_vm1 - Manjaro", 30000, DriveType.Disk)),
		};
	}
	
	/// <summary>
	/// Refreshes the current drives list according to the set query. Handles a click on the refresh button too.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Either the user has clicked on the refresh button, or refreshing the drives list is needed. <br/>
	/// Postcondition: On success, the list is updated according to latest data. On failure, the list is cleared.
	/// </remarks>
	[RelayCommand]
	public async Task<ExitCode> RefreshAsync()
	{
		DatabaseService.SearchedDrive[]? drives = await _databaseService.SearchDrivesAsync(Query);
		if (drives == null)
			return ExitCode.DatabaseOperationFailed;
		
		foreach (DatabaseService.SearchedDrive drive in drives)
			Drives.Add(new DriveItemTemplate(drive));

		return ExitCode.Success;
	}
	
	/// <summary>
	/// Handles a change in the query field. Updates the drives list according to the set query.
	/// </summary>
	/// <param name="value">Unused.</param>
	/// <remarks>
	/// Precondition: The query field has changed - the user has changed its content. <br/>
	/// Postcondition: A refresh of the drives list is started according to the set query.
	/// </remarks>
	partial void OnQueryChanged(string value) => _ = RefreshAsync();
}

public class DriveItemTemplate
{
	public int Id { get; }
	public int OwnerId { get; }
	public string OwnerUsername { get; }
	public string Name { get; }
	public string Size { get; }
	public DriveType DriveType { get; }

	public DriveItemTemplate(DatabaseService.SearchedDrive drive)
	{
		Id = drive.Id;
		OwnerId = drive.OwnerId;
		OwnerUsername = drive.OwnerUsername;
		Name = drive.Name;
		Size = 	drive.SizeMiB < 1024
			? $"{drive.SizeMiB} MiB"
			: $"{(drive.SizeMiB / 1024.0):0.##} GiB";
		
		DriveType = drive.DriveType;
	}
}