using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Server.Services;
using Shared;
using Shared.VirtualMachines;

namespace Server.ViewModels;

public partial class VirtualMachinesViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;

	public ObservableCollection<VirtualMachineItemTemplate> VirtualMachines { get; }

	[ObservableProperty]
	private string _query = string.Empty;
	
	public VirtualMachinesViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		VirtualMachines = new ObservableCollection<VirtualMachineItemTemplate>();
		_ = RefreshAsync();
	}
	
	/* Use for IDE preview only. */
	public VirtualMachinesViewModel()
	{
		_databaseService = null!;
	
		VirtualMachines = new ObservableCollection<VirtualMachineItemTemplate>()
		{
			new VirtualMachineItemTemplate(new DatabaseService.SearchedVirtualMachine(1, 5, "d", "test_vm0", OperatingSystem.MiniCoffeeOS, 
				CpuArchitecture.X86, 5, BootMode.Bios, VmState.ShutDown)),
			
			new VirtualMachineItemTemplate(new DatabaseService.SearchedVirtualMachine(2, 6, "d", "test_vm1", OperatingSystem.ManjaroLinux, 
				CpuArchitecture.X86_64, 4096, BootMode.Uefi, VmState.ShutDown)),
		};
	}

	/// <summary>
	/// Refreshes the current virtual machines list according to the set query. Handles a click on the refresh button too.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Either the user has clicked on the refresh button, or refreshing the virtual machines list is needed. <br/>
	/// Postcondition: On success, the list is updated according to latest data. On failure, the list is cleared.
	/// </remarks>
	[RelayCommand]
	public async Task<ExitCode> RefreshAsync()
	{
		VirtualMachines.Clear();
		DatabaseService.SearchedVirtualMachine[]? virtualMachines = await _databaseService.SearchVirtualMachinesAsync(Query);
		if (virtualMachines == null)
			return ExitCode.DatabaseOperationFailed;

		foreach (DatabaseService.SearchedVirtualMachine virtualMachine in virtualMachines)
			VirtualMachines.Add(new VirtualMachineItemTemplate(virtualMachine));
		
		return ExitCode.Success;
	}
	
	/// <summary>
	/// Handles a change in the query field. Updates the virtual machines list according to the set query.
	/// </summary>
	/// <param name="value">Unused.</param>
	/// <remarks>
	/// Precondition: The query field has changed - the user has changed its content. <br/>
	/// Postcondition: A refresh of the virtual machines list is started according to the set query.
	/// </remarks>
	partial void OnQueryChanged(string value) => _ = RefreshAsync();
}

public class VirtualMachineItemTemplate
{
	public int Id { get; }
	public int OwnerId { get; }
	public string OwnerUsername { get; }
	public string Name { get; }
	public string OperatingSystem { get; }
	public string CpuArchitecture { get; }
	public string RamSize { get; }
	public string BootMode { get; }
	public string State { get; }	
	public Brush StateColor { get; }
	
	public VirtualMachineItemTemplate(DatabaseService.SearchedVirtualMachine virtualMachine)
	{
		Id = virtualMachine.Id;
		OwnerId = virtualMachine.OwnerId;
		OwnerUsername = virtualMachine.OwnerUsername;
		Name = virtualMachine.Name;
		OperatingSystem = Common.SeparateStringWords(virtualMachine.OperatingSystem.ToString());
		CpuArchitecture = virtualMachine.CpuArchitecture.ToString();
		RamSize = virtualMachine.RamSizeMiB < 1024
			? $"{virtualMachine.RamSizeMiB} MiB"
			: $"{(virtualMachine.RamSizeMiB / 1024.0):0.##} GiB";
		
		BootMode = virtualMachine.BootMode.ToString().ToUpper();
		State = Common.SeparateStringWords(virtualMachine.State.ToString());
		StateColor = virtualMachine.State == VmState.Running
			? SolidColorBrush.Parse("#64d670")
			: SolidColorBrush.Parse("#202020");
	}
}
