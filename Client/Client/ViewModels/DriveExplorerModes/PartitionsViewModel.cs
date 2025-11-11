using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.Input;
using OpenTK.Platform.Windows;
using Shared.Drives;

namespace Client.ViewModels.DriveExplorerModes;

public class PartitionsViewModel : DriveExplorerMode
{
	private readonly DriveService _driveService;
	public ObservableCollection<PartitionItemTemplate> Partitions { get; }
	private DriveGeneralDescriptor _driveDescriptor;
	
	public PartitionsViewModel(NavigationService navigationService, ClientService clientService, 
		DriveService driveService, DriveGeneralDescriptor driveDescriptor, PathItem[] partitions) 
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		_driveDescriptor = driveDescriptor;
		Partitions = new ObservableCollection<PartitionItemTemplate>();
		
		for (int i = 0; i < partitions.Length; ++i)
		{
			if (partitions[i] is PathItemPartitionGpt gptPartition)
			{
				Partitions.Add(new PartitionItemTemplate(
						i,
						(int)((gptPartition.Descriptor.EndLba - gptPartition.Descriptor.StartLba) * _driveDescriptor.SectorSize / (1024 * 1024)),
						gptPartition.Descriptor.Label,
						gptPartition.Descriptor.Type
					)
				);
			}
			else if (partitions[i] is PathItemPartitionMbr mbrPartition)
			{
				Partitions.Add(new PartitionItemTemplate(
						i,
						(int)(mbrPartition.Descriptor.Sectors * _driveDescriptor.SectorSize / (1024 * 1024)),
						string.Empty,
						string.Empty
					)
				);
			}
			
			Partitions.Last().Opened += OnPartitionOpened;
		}
	}

	private async Task OpenPartitionAsync(int partitionIndex)
	{
		if (partitionIndex < 0 || partitionIndex >= Partitions.Count)
			return;

		PathItem[]? items = await _driveService.ListItemsOnDrivePathAsync(_driveDescriptor.Id, partitionIndex.ToString());
		if (items == null)
			return;
		
		ChangeMode?.Invoke(new FileSystemItemsViewModel(NavigationSvc, ClientSvc, _driveService, _driveDescriptor, 
				partitionIndex.ToString(), items
			)
		);
	}

	/// <summary>
	/// Handles both a double click on the partition and a click on its open button. Attempts to open the partition.
	/// </summary>
	/// <remarks>
	/// Precondition: User has either double-clicked on the partition or has clicked on its open button. <br/>
	/// Postcondition: An attempt to open the partition is performed.
	/// </remarks>
	private void OnPartitionOpened(int partitionIndex) => _ = OpenPartitionAsync(partitionIndex);
}

public partial class PartitionItemTemplate
{
	public Action<int>? Opened;
	public int Index { get; }
	public int SizeMiB { get; }
	public string SizeMiBString =>
		SizeMiB >= 1024 
			? $"{(SizeMiB/1024.0):0.##} GiB" 
			: $"{SizeMiB} MiB";

	public string Label { get; }
	public string Type { get; }

	public PartitionItemTemplate(int index, int sizeMiB, string label, string type)
	{
		Index = index;
		SizeMiB = sizeMiB;
		Label = label;
		Type = type;
	}

	/// <summary>
	/// Handles both a double click on the partition and a click on its open button. Attempts to open the partition.
	/// </summary>
	/// <remarks>
	/// Precondition: User has either double-clicked on the partition or has clicked on its open button. <br/>
	/// Postcondition: An attempt to open the partition is performed.
	/// </remarks>
	[RelayCommand]
	public void Open() => Opened?.Invoke(Index);
}