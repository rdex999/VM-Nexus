using System.Collections.ObjectModel;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared.Drives;

namespace Client.ViewModels.DriveExplorerModes;

public partial class PartitionsViewModel : DriveExplorerMode
{
	private readonly DriveService _driveService;
	public ObservableCollection<PartitionItemTemplate> Partitions { get; }
	private DriveGeneralDescriptor _driveDescriptor;

	[ObservableProperty] 
	private bool _isGptPartitioned;
	
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
		}
	}
}

public class PartitionItemTemplate
{
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
}