using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Iso9660;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Raw;
using DiscUtils.SquashFs;
using DiscUtils.Streams;
using Server.Drives;
using Shared;
using Shared.Drives;
using DriveType = Shared.Drives.DriveType;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Server.Services;

public class DriveService
{
	private DatabaseService _databaseService;

	public DriveService(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		DiscUtils.Complete.SetupHelper.SetupComplete();
	}

	/// <summary>
	/// Create a drive of the given size (MiB) with the given operating system, under the given user.
	/// </summary>
	/// <param name="userId">The ID of the user under which to create the drive on. userId >= 1.</param>
	/// <param name="driveName">The name of the new drive. driveName != null.</param>
	/// <param name="operatingSystem">The operating system to install on the drive. operatingSystem != OperatingSystem.Other.</param>
	/// <param name="size">The size of the new drive in MiB. size >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID must exist. There must not be a drive named under the given name. <br/>
	/// userId >= 1 &amp;&amp; driveName != null &amp;&amp; operatingSystem != OperatingSystem.Other &amp;&amp; size >= 1. <br/>
	/// Postcondition: On success, the new drive is created and registered, and the returned exit code indicates success. <br/>
	/// On failure, the drive is not created and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> CreateOperatingSystemDriveAsync(int userId, string driveName,
		Shared.VirtualMachines.OperatingSystem operatingSystem, int size)
	{
		if (!Enum.IsDefined(typeof(Shared.VirtualMachines.OperatingSystem), operatingSystem) ||
		    operatingSystem == Shared.VirtualMachines.OperatingSystem.Other || userId < 1 || size < 1)
		{
			return ExitCode.InvalidParameter;
		}

		if (!Common.IsOperatingSystemDriveSizeValid(operatingSystem, size) || size > 256 * 1024)
		{
			return ExitCode.InvalidDriveSize;
		}
		
		Shared.Drives.DriveType driveType = operatingSystem == Shared.VirtualMachines.OperatingSystem.MiniCoffeeOS
			? Shared.Drives.DriveType.Floppy
			: Shared.Drives.DriveType.Disk;
		
		ExitCode result = await _databaseService.CreateDriveAsync(userId, driveName, size, driveType);
		if (result != ExitCode.Success)
			return result;

		int driveId = await GetDriveIdAsync(userId, driveName);	/* Just successfully created the drive - this must succeed. */
		string driveFilePath = GetDriveFilePath(driveId);
	
		long driveSize = (long)size * 1024 * 1024;		/* The drive size in bytes */

		if (operatingSystem == Shared.VirtualMachines.OperatingSystem.MiniCoffeeOS)
		{
			result = await CreateMiniCoffeeOsDiskImageAsync(driveFilePath, size);
			if (result != ExitCode.Success)
			{
				await DeleteDriveAsync(driveId);
				return ExitCode.DiskImageCreationFailed;
			}
		}
		else
		{
			string osDiskImageName;
			switch (operatingSystem)
			{
				case Shared.VirtualMachines.OperatingSystem.Ubuntu:
					osDiskImageName = "Ubuntu.raw";
					break;
				case Shared.VirtualMachines.OperatingSystem.KaliLinux:
					osDiskImageName = "Kali.raw";
					break;
				case Shared.VirtualMachines.OperatingSystem.ManjaroLinux:
					osDiskImageName = "Manjaro.raw";
					break;
				default:
					return ExitCode.InvalidParameter;	/* This cant be reached because of the if statement above. Doing it for the C# compiler. */
			}

			/* This is faster than File.Copy */
			Process? copyProc = Process.Start(new ProcessStartInfo()
			{
				FileName = "/bin/cp",
				ArgumentList = { GetVmNexusFolderPath() + "/OsDiskImages/" + osDiskImageName, driveFilePath },
				UseShellExecute = false,
			});
			await copyProc!.WaitForExitAsync();

			if (copyProc.ExitCode != 0)
			{
				await DeleteDriveAsync(driveId);
				return ExitCode.DiskImageCreationFailed;
			}

			await using (FileStream fsResize = new FileStream(driveFilePath, FileMode.Open, FileAccess.Write))
			{
				if (driveSize > fsResize.Length)
				{
					fsResize.SetLength(driveSize);
					// fsResize.Flush(true);
				}
			}

			/* First partition is EFI, second is swap, third is root (ext4). */

			/* Delete the root partition (doesnt delete its content) because its ending sector is set to the old size. (we want to extend the partition) */
			Process? sgdiskDeletePartProc = Process.Start(new ProcessStartInfo()
			{
				FileName = "/bin/sgdisk",
				ArgumentList = { "--delete=3", driveFilePath },
				UseShellExecute = false,
			});
			await sgdiskDeletePartProc!.WaitForExitAsync();

			if (sgdiskDeletePartProc.ExitCode != 0)
			{
				await DeleteDriveAsync(driveId);
				return ExitCode.DiskImageCreationFailed;
			}

			/* Create the root partition and make it occupy all available space */
			Process? sgdiskNewPartProc = Process.Start(new ProcessStartInfo()
			{
				FileName = "/bin/sgdisk",
				ArgumentList = { "--largest-new=3", driveFilePath },
				UseShellExecute = false,
			});
			await sgdiskNewPartProc!.WaitForExitAsync();

			if (sgdiskNewPartProc.ExitCode != 0)
			{
				await DeleteDriveAsync(driveId);
				return ExitCode.DiskImageCreationFailed;
			}
		}

		return ExitCode.Success;
	}

	/// <summary>
	/// Creates a disk image with Mini Coffee OS installed on it.
	/// </summary>
	/// <param name="path">The path to create the disk image at. path != null.</param>
	/// <param name="sizeMiB">The size of the disk image to create, in MiB. sizeMiB >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The given path is valid and exists. The given size is within valid range. path != null &amp;&amp; sizeMiB >= 1. <br/>
	/// Postcondition: On success, the disk image is created and the returned exit code indicates success.
	/// On failure, the disk image is not created and the returned exit code indicates the error.
	/// </remarks>
	private async Task<ExitCode> CreateMiniCoffeeOsDiskImageAsync(string path, int sizeMiB)
	{
		if (!Common.IsOperatingSystemDriveSizeValid(OperatingSystem.MiniCoffeeOS, sizeMiB))
			return ExitCode.InvalidParameter;
		
		Directory.CreateDirectory("../../../MiniCoffeeOsBuilds");
		string[] builds = Directory.GetFiles("../../../MiniCoffeeOsBuilds");
		int highest = -1;
		foreach (string build in builds)
		{
			if (!int.TryParse(build, out int buildNumber))
			{
				Directory.Delete("../../../MiniCoffeeOsBuilds/" + build, true);
				continue;
			}
			
			if (buildNumber > highest)
				highest = buildNumber;
		}
		
		int buildId = highest + 1;
		string buildPath = "../../../MiniCoffeeOsBuilds/" + buildId;
		
		using Process? copyProc = Process.Start(new ProcessStartInfo()
		{
			FileName = "/usr/bin/cp",
			Arguments = $" -r ../../../MiniCoffeeOS {buildPath}"
		});
		if (copyProc == null)
			return ExitCode.DriveDiskImageCreationFailed;

		await copyProc.WaitForExitAsync();
		if (copyProc.ExitCode != 0)
			return ExitCode.DriveDiskImageCreationFailed;
		
		using Process? process = Process.Start(new ProcessStartInfo()
		{
			FileName = "/usr/bin/make",
			Arguments = $" -C {buildPath} FDA={path} FDA_SIZE={sizeMiB}",
		});

		if (process == null)
			return ExitCode.DriveDiskImageCreationFailed;
		
		await process.WaitForExitAsync();
		
		Directory.Delete(buildPath, true);
		
		if (process.ExitCode == 0)
			return ExitCode.Success;
		
		return ExitCode.DriveDiskImageCreationFailed;
	}

	/// <summary>
	/// Creates a drive formatted with the given filesystem. Currently only FAT32 is supported.
	/// </summary>
	/// <param name="userId">The ID of the user to create the drive under. userId >= 1.</param>
	/// <param name="name">The name of the new drive. name != null.</param>
	/// <param name="sizeMb">The size of the drive to create, in MiB. Must be in valid range, according to filesystem limits.</param>
	/// <param name="fileSystem">The filesystem to format the drive with.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. There is not such drive with the given name under the given user.
	/// userId >= 1 &amp;&amp; name != null &amp;&amp; sizeMb in range according to filesystem. <br/>
	/// Postcondition: On success, the drive is created and the returned exit code indicates success.
	/// On failure, the drive is not created and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> CreateFileSystemDriveAsync(int userId, string name, int sizeMb, FileSystemType fileSystem)
	{
		long sizeBytes = sizeMb * 1024L * 1024L;
		Geometry diskGeometry = Geometry.FromCapacity(sizeBytes);
		long sectors = sizeBytes / diskGeometry.BytesPerSector;
		sizeBytes = sectors * diskGeometry.BytesPerSector;
		
		if (userId < 1 || sizeMb > SharedDefinitions.DriveSizeMbMax || sizeBytes < fileSystem.DriveSizeMin() || fileSystem != FileSystemType.Fat32)
			return ExitCode.InvalidParameter;

		ExitCode result = await _databaseService.CreateDriveAsync(userId, name, sizeMb, DriveType.Disk);
		if (result != ExitCode.Success)
			return result;
		
		int driveId = await GetDriveIdAsync(userId, name);		/* Must succeed because the drive was created successfully. */

		FileStream image;
		try
		{
			image = File.Create(GetDriveFilePath(driveId));
		}
		catch (Exception)
		{
			await _databaseService.DeleteDriveAsync(driveId);
			return ExitCode.DriveDiskImageCreationFailed;
		}
		
		image.SetLength(sectors * diskGeometry.BytesPerSector);
		await image.FlushAsync();
		
		try
		{
			switch (fileSystem)
			{
				case FileSystemType.Fat32:
				{
					using FatFileSystem fs = FatFileSystem.FormatPartition(image, string.Empty, diskGeometry, 0, (int)sectors, 0);
					break;
				}
			}
		}
		catch (Exception)
		{
			await Task.WhenAll(
				image.DisposeAsync().AsTask(),
				_databaseService.DeleteDriveAsync(driveId)
			);
			
			return ExitCode.DriveFormattingFailed;
		}

		await image.DisposeAsync();
		return ExitCode.Success;
	}

	/// <summary>
	/// Get the ID of a drive called name, owned by the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to search the drive under. userId >= 1.</param>
	/// <param name="name">The name of the drive to search for. name != null.</param>
	/// <returns>The ID of the drive on success, -1 on failure.</returns>
	/// <remarks>
	/// Precondition: A user called by username exists. The given user has a drive called by name. userId != null &amp;&amp; name != null. <br/>
	/// Postcondition: On success, the ID of the drive is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<int> GetDriveIdAsync(int userId, string name) => 
		await _databaseService.GetDriveIdAsync(userId, name);

	/// <summary>
	/// Check if the given user has a drive called by name.
	/// </summary>
	/// <param name="userId">The ID of the user to search the drive on. userId >= 1.</param>
	/// <param name="name">The name of the drive to search for. name != null.</param>
	/// <returns>True if the drive exists, false if not or on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID must exist. userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: Returns whether a drive called by the given name exists under the given user. Returns false on failure.
	/// </remarks>
	public async Task<bool> IsDriveExistsAsync(int userId, string name) =>
		await _databaseService.IsDriveExistsAsync(userId, name);

	/// <summary>
	/// Deletes the given drive.
	/// </summary>
	/// <param name="id">The ID of the drive. id >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. id >= 1. <br/>
	/// Postcondition: On success, the drive is deleted and the returned exit code indicates success. <br/>
	/// On failure, the drive is not deleted and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DeleteDriveAsync(int id)
	{
		string filePath = GetDriveFilePath(id);
		try
		{
			File.Delete(filePath);
		}
		catch (Exception)
		{
			// ignored
		}

		return await _databaseService.DeleteDriveAsync(id);
	}

	/// <summary>
	/// Registers a drive-VM connection. (Means that when the VM starts, the drive will be connected to it.)
	/// </summary>
	/// <param name="driveId">The ID of the drive to connect. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine to connect the drive to. vmId >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists, a virtual machine with the given ID exists,
	/// and there is no such drive connection that already exists. <br/>
	/// driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: On success, the drive connection is registered and the returned exit code states success. <br/>
	/// On failure, the connection is not registered and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> ConnectDriveAsync(int driveId, int vmId) => 
		await _databaseService.ConnectDriveAsync(driveId, vmId);
	
	/// <summary>
	/// Removed the given drive-VM connection. (Means that when the virtual machine starts, this drive will not be connected.)
	/// </summary>
	/// <param name="driveId">The ID of the drive that is connected to the virtual machine. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine that the drive is connected to. vmId >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists, and a virtual machine with the given ID exists.
	/// The given drive is connected to the given virtual machine. (There is a connection between the two) <br/>
	/// Postcondition: On success, the drive-VM connection is removed and the returned exit code indicates success. <br/>
	/// On failure, the drive-VM connection is not affected and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DisconnectDriveAsync(int driveId, int vmId) =>
		await _databaseService.DisconnectDriveAsync(driveId, vmId);
	
	/// <summary>
	/// Get the filename of a drive (the drives disk image file) that the ID of is the given ID.
	/// </summary>
	/// <param name="driveId">The ID of the drive. driveId >= 1.</param>
	/// <returns>The filename of the disk image file of the drive.</returns>
	/// <remarks>
	/// Precondition: driveId >= 1. <br/>
	/// Postcondition: The filename of the disk image file of the drive is returned.
	/// </remarks>
	public string GetDriveFileName(int driveId) => $"{driveId}.img";
	
	/// <summary>
	/// Get the file path of a drive (the drives disk image file path) that the ID of is the given ID. <br/>
	/// The file path is relative to the root filesystem (starts with /).
	/// </summary>
	/// <param name="driveId">The ID of the drive. driveId >= 1.</param>
	/// <returns>The file path to the disk image file of the drive.</returns>
	/// <remarks>
	/// Precondition: driveId >= 1. <br/>
	/// Postcondition: The file path to the disk image file of the drive is returned.
	/// </remarks>
	public string GetDriveFilePath(int driveId) => GetVmNexusFolderPath() + "/DiskImages/" + GetDriveFileName(driveId);

	/// <summary>
	/// Lists items under the given path in the given drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive to use. driveId >= 1.</param>
	/// <param name="path">The path in the drive under which to list the items. path != null.</param>
	/// <returns>An array of path items, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. The given path exists and is in valid syntax. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: On success, an array of path items representing the items under the given path is returned. On failure, null is returned.
	/// </remarks>
	public PathItem[]? ListItems(int driveId, string path)
	{
		if (driveId < 1)
			return null;
		
		string pathTrimmed = path.Trim(SharedDefinitions.DirectorySeparators);
		string[] pathParts = pathTrimmed.Split(SharedDefinitions.DirectorySeparators);
		
		Disk drive;
		try
		{
			drive = new Disk(GetDriveFilePath(driveId), FileAccess.Read);
		}
		catch (Exception)
		{
			return null;
		}
		
		Stream filesystemStream;
		List<PathItem> items = new List<PathItem>();
		if (IsDrivePartitioned(drive))
		{
			if (pathParts.Length == 0 || (pathParts.Length == 1 && string.IsNullOrEmpty(pathParts[0])))
			{
				foreach (PartitionInfo partition in drive.Partitions.Partitions)
				{
					if (partition is GuidPartitionInfo gptPartition)
					{
						items.Add(new PathItemPartitionGpt(new PartitionGptDescriptor(
							gptPartition.Identity,
							gptPartition.FirstSector,
							gptPartition.LastSector,
							(PartitionGptDescriptor.Attribute)gptPartition.Attributes,
							gptPartition.Name,
							gptPartition.TypeAsString
						)));
					} 
					else if (partition is BiosPartitionInfo mbrPartition)
					{
						items.Add(new PathItemPartitionMbr(new PartitionMbrDescriptor(
							mbrPartition.IsActive,
							(PartitionMbrDescriptor.Type)mbrPartition.BiosType,
							mbrPartition.FirstSector,
							mbrPartition.SectorCount
						)));
					}
				}
				
				drive.Dispose();
				return items.ToArray();
			}

			/* First part of the path should contain the partition index if the drive is partitioned. */
			if (!int.TryParse(pathParts[0], out int partitionIndex))
			{
				drive.Dispose();
				return null;
			}
			
			PartitionInfo partitionInfo = drive.Partitions.Partitions[partitionIndex];
			filesystemStream = partitionInfo.Open();
			filesystemStream.Seek(partitionInfo.FirstSector * drive.SectorSize, SeekOrigin.Begin);
		}
		else
		{
			filesystemStream = drive.Content;
			filesystemStream.Seek(0, SeekOrigin.Begin);		/* Docs says not guaranteed to be at any position - set to beginning. */
			PathItem[]? resultItems = ListItemsOnFileSystemPath(filesystemStream, string.Join('\\', pathParts.AsSpan()!));
			drive.Dispose();
			return resultItems;
		}

		PathItem[]? listedItems = ListItemsOnFileSystemPath(filesystemStream, string.Join('\\', pathParts.AsSpan()[1..]!));
		drive.Dispose();
		return listedItems;
	}

	/// <summary>
	/// Get the type of partition table (if any) used in the given drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive to check the partition table type of.</param>
	/// <returns>The partition table type used in the drive, or -1 on failure.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. driveId >= 1. <br/>
	/// Postcondition: On success, the type of partition table used in the given drive is returned. On failure, -1 is returned.
	/// </remarks>
	public PartitionTableType GetDrivePartitionTableType(int driveId)
	{
		if (driveId < 1)
			return (PartitionTableType)(-1);
	
		Disk drive;
		try
		{
			drive = new Disk(GetDriveFilePath(driveId), FileAccess.Read);
		}
		catch (Exception)
		{
			return (PartitionTableType)(-1);
		}

		Stream filesystemStream = drive.Content;
		filesystemStream.Seek(0, SeekOrigin.Begin);
		
		PartitionTableType type = PartitionTableType.Unpartitioned;
		if (IsDrivePartitioned(drive))
		{
			if (drive.Partitions is GuidPartitionTable)
				type = PartitionTableType.GuidPartitionTable;
			
			if (drive.Partitions is BiosPartitionTable)
				type = PartitionTableType.MasterBootRecord;
		}
		
		drive.Dispose();
		return type;
	}

	/// <summary>
	/// Get the size (in bytes) of each sector in the given drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive to get the sector size of. driveId >= 1.</param>
	/// <returns>The sector size of the drive, in bytes.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. driveId >= 1. <br/>
	/// Postcondition: On success, the sector size of the drive is returned. (in bytes) On failure, -1 is returned.
	/// </remarks>
	public int GetDriveSectorSize(int driveId)
	{
		if (driveId < 1)
			return -1;

		try
		{
			using Disk drive = new Disk(GetDriveFilePath(driveId), FileAccess.Read);
			return drive.SectorSize;
		}
		catch (Exception)
		{
			return -1;
		}
	}

	/// <summary>
	/// Get a drive general descriptor of the drive identified by the given userId and drive name.
	/// </summary>
	/// <param name="userId">The ID of the user that the drive was created under. userId >= 1.</param>
	/// <param name="driveName">The name of the drive to search for. driveName != null.</param>
	/// <returns>A drive general descriptor of the drive, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service initialized, userId >= 1 &amp;&amp; driveName != null. <br/>
	/// Postcondition: On success, a drive general descriptor of the drive is returned. On failure, null is returned.
	/// </remarks>
	public async Task<DriveGeneralDescriptor?> GetDriveGeneralDescriptorAsync(int userId, string driveName)
	{
		if (userId < 1)
			return null;
		
		DriveDescriptor? descriptor = await _databaseService.GetDriveDescriptorAsync(userId, driveName);
		if (descriptor == null)
			return null;
		
		int sectorSize = GetDriveSectorSize(descriptor.Id);
		if (sectorSize < 0)
			return null;
		
		PartitionTableType partitionTableType = GetDrivePartitionTableType(descriptor.Id);
		if (partitionTableType == (PartitionTableType)(-1))
			return null;
		
		return new DriveGeneralDescriptor(descriptor.Id, driveName, descriptor.Size, sectorSize, descriptor.Type, partitionTableType);
	}
	
	/// <summary>
	/// Get general descriptors of all drives connected to the given virtual machine.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that the drives are attached to. vmId >= 1.</param>
	/// <returns>An array of general drive descriptors, describing each drive that is connected to the virtual machine. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists. vmId >= 1. <br/>
	/// Postcondition: On success, an array of general drive descriptors is returned, describing each drive that is connected to the virtual machine.
	/// On failure, null is returned.
	/// </remarks>
	public async Task<DriveGeneralDescriptor[]?> GetVmDriveGeneralDescriptorsAsync(int vmId)
	{
		DriveDescriptor[]? descriptors = await _databaseService.GetVmDriveDescriptorsAsync(vmId);
		if (descriptors == null)
			return null;

		DriveGeneralDescriptor[] generalDescriptors = new DriveGeneralDescriptor[descriptors.Length];
		for (int i = 0; i < descriptors.Length; ++i)
		{
			generalDescriptors[i] = new DriveGeneralDescriptor(
				descriptors[i].Id, 
				descriptors[i].Name, 
				descriptors[i].Size, 
				GetDriveSectorSize(descriptors[i].Id),
				descriptors[i].Type, 
				GetDrivePartitionTableType(descriptors[i].Id)
			);
		}

		return generalDescriptors;
	}

	/// <summary>
	/// Get general descriptors of all drives of the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to get the drive descriptors of. userId >= 1.</param>
	/// <returns>An array of drive general descriptors, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. userId >= 1. <br/>
	/// Postcondition: On success, an array of drive general descriptors is returned. (can be empty of user doesnt have drives) <br/>
	/// On failure, null is returned.
	/// </remarks>
	public async Task<DriveGeneralDescriptor[]?> GetDriveGeneralDescriptorsOfUserAsync(int userId)
	{
		DriveDescriptor[]? descriptors = await _databaseService.GetDriveDescriptorsOfUserAsync(userId);
		if (descriptors == null)
			return null;
		
		DriveGeneralDescriptor[] generalDescriptors = new DriveGeneralDescriptor[descriptors.Length];
		for (int i = 0; i < descriptors.Length; ++i)
		{
			generalDescriptors[i] = new DriveGeneralDescriptor(
				descriptors[i].Id,
				descriptors[i].Name,
				descriptors[i].Size,
				GetDriveSectorSize(descriptors[i].Id),
				descriptors[i].Type,
				GetDrivePartitionTableType(descriptors[i].Id)
			);
		}
		
		return generalDescriptors;
	}

	/// <summary>
	/// Get a read/write stream of an item in a drive, or the drive itself. (disk image) Directories are not supported.
	/// </summary>
	/// <param name="driveId">The ID of the drive. driveId >= 1.</param>
	/// <param name="path">
	/// The path on the drive, which points to the needed item. Set to an empty string to get the drive's disk image. path != null.
	/// </param>
	/// <param name="access">The access to the file. (read, write, readwrite)</param>
	/// <param name="createFileIfNotExists">
	/// Optional - Only valid for files. Set to true to create the file if it doesn't already exist, false otherwise. False by default.
	/// </param>
	/// <returns>A stream representing the item, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. The given path exists and is in valid syntax. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: On success, a stream representing the item is returned. On failure, null is returned.
	/// </remarks>
	public ItemStream? GetItemStream(int driveId, string path, FileAccess access, bool createFileIfNotExists = false)
	{
		if (driveId < 1)
			return null;

		string trimmedPath = path.Trim(SharedDefinitions.DirectorySeparators);
		string[] pathParts = trimmedPath.Split(SharedDefinitions.DirectorySeparators);
		
		/* Download disk image */
		if (pathParts.Length == 0 || (pathParts.Length == 1 && string.IsNullOrEmpty(pathParts[0])))
		{
			try
			{
				return new ItemStream(File.Open(GetDriveFilePath(driveId), FileMode.Open, access));
			}
			catch (Exception)
			{
				return null;
			}
		}
		
		Disk drive;
		try
		{
			drive = new Disk(GetDriveFilePath(driveId), access);
		}
		catch (Exception)
		{
			return null;
		}

		DiscFileSystem? fileSystem = null;
		string fileSystemPath;
		if (IsDrivePartitioned(drive))
		{
			/* First part of the path should contain the partition index if the drive is partitioned. */
			if (!int.TryParse(pathParts[0], out int partitionIndex) || partitionIndex < 0 || partitionIndex >= drive.Partitions.Count)
			{
				drive.Dispose();
				return null;
			}

			PartitionInfo partitionInfo = drive.Partitions[partitionIndex];
			Stream fileSystemStream = partitionInfo.Open();
			if (pathParts.Length == 1)		/* return stream of partition. */
			{
				fileSystemStream.Seek(0, SeekOrigin.Begin);
				return new ItemStream(fileSystemStream, drive);
			}
		
			fileSystemStream.Seek(partitionInfo.FirstSector * drive.SectorSize, SeekOrigin.Begin);
			fileSystem = GetStreamFileSystem(fileSystemStream);
			fileSystemPath = string.Join('\\', pathParts.AsSpan()[1..]!);
		}
		else
		{
			fileSystem = GetStreamFileSystem(drive.Content);
			fileSystemPath = string.Join('\\', pathParts);
		}
		
		if (fileSystem == null)
		{
			drive.Dispose();
			return null;
		}

		if (!fileSystem.DirectoryExists(Path.GetDirectoryName(fileSystemPath)) || (!createFileIfNotExists && !fileSystem.Exists(fileSystemPath)))
		{
			fileSystem.Dispose();
			drive.Dispose();
			return null;
		}

		try
		{
			Stream stream = fileSystem.OpenFile(fileSystemPath, createFileIfNotExists ? FileMode.OpenOrCreate : FileMode.Open, access);
			return new ItemStream(stream, drive, fileSystem);
		}
		catch (Exception)
		{
			fileSystem.Dispose();
			drive.Dispose();
			return null;
		}
	}

	/// <summary>
	/// Create a directory at the given path in the given drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive to create the directory in. driveId >= 1.</param>
	/// <param name="path">The path inside the drive, where to create the directory.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. The given path, except for its last part (the directory to create), exist.
	/// driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: On success, the directory is created and the returned exit code indicates success.
	/// On failure, the directory is not created, and the returned exit code indicates the error.
	/// </remarks>
	public ExitCode CreateDirectory(int driveId, string path)
	{
		if (driveId < 1)
			return ExitCode.InvalidParameter;

		string trimmedPath = Common.CleanPath(path);
		string[] pathParts = trimmedPath.Split(SharedDefinitions.DirectorySeparators);

		Disk drive;
		try
		{
			drive = new Disk(GetDriveFilePath(driveId));
		}
		catch (Exception)
		{
			return ExitCode.DriveDoesntExist;
		}

		DiscFileSystem? fileSystem = null;
		string fileSystemPath;
		if (IsDrivePartitioned(drive))
		{
			/* First part of the path should contain the partition index if the drive is partitioned. */
			if (Common.IsPathToDrive(trimmedPath) || !int.TryParse(pathParts[0], out int partitionIndex) || partitionIndex < 0 || partitionIndex >= drive.Partitions.Count)
			{
				drive.Dispose();
				return ExitCode.InvalidPath;
			}

			PartitionInfo partitionInfo = drive.Partitions[partitionIndex];
			Stream fileSystemStream = partitionInfo.Open();
			fileSystemStream.Seek(partitionInfo.FirstSector * drive.SectorSize, SeekOrigin.Begin);
			
			fileSystem = GetStreamFileSystem(fileSystemStream);
			fileSystemPath = string.Join('\\', pathParts.AsSpan()[1..]!);
		}
		else
		{
			fileSystem = GetStreamFileSystem(drive.Content);
			fileSystemPath = string.Join('\\', pathParts);
		}

		if (fileSystem == null)
		{
			drive.Dispose();
			return ExitCode.UnsupportedFileSystem;
		}
	
		ExitCode result = ExitCode.Success;

		try
		{
			if (fileSystem.Exists(fileSystemPath))
				result = ExitCode.ItemAlreadyExists;
			
			else if (!fileSystem.Exists(string.Join('\\', pathParts[..^1])))
				result = ExitCode.InvalidPath;

			else
				fileSystem.CreateDirectory(fileSystemPath);
		}
		catch (Exception)
		{
			result = ExitCode.InvalidPath;
		}
	
		fileSystem.Dispose();
		drive.Dispose();
		
		return result;
	}
	
	/// <summary>
	/// Checks whether the given item exists, be it a drive, partition, directory or a file.
	/// </summary>
	/// <param name="driveId">The ID of the drive to check on. driveId >= 1.</param>
	/// <param name="path">The path on the drive, points to the item to check. path != null.</param>
	/// <returns>True if the item exists, false otherwise or on failure.</returns>
	/// <remarks>
	/// Precondition: path != null. <br/>
	/// Postcondition: On success, returns whether the given item exists, be it a drive, partition, directory or a file.
	/// On failure, false is returned.
	/// </remarks>
	public async Task<bool> ItemExistsAsync(int driveId, string path)
	{
		if (driveId < 1 || !await _databaseService.IsDriveExistsAsync(driveId))
			return false;
		
		string trimmedPath = path.Trim(SharedDefinitions.DirectorySeparators);
		string[] pathParts = trimmedPath.Split(SharedDefinitions.DirectorySeparators);

		if (pathParts.Length == 0 || (pathParts.Length == 1 && string.IsNullOrEmpty(pathParts[0])))
			return true;
		
		Disk drive;
		try
		{
			drive = new Disk(GetDriveFilePath(driveId));
		}
		catch (Exception)
		{
			return false;
		}

		DiscFileSystem? fileSystem = null;
		string fileSystemPath;
		if (IsDrivePartitioned(drive))
		{
			if (!int.TryParse(pathParts[0], out int partitionIndex) || partitionIndex < 0 ||
			    partitionIndex >= drive.Partitions.Count)
			{
				drive.Dispose();
				return false;
			}

			if (pathParts.Length == 1)
			{
				drive.Dispose();
				return true;
			}
			
			PartitionInfo partitionInfo = drive.Partitions[partitionIndex];
			Stream fileSystemStream = partitionInfo.Open();
			fileSystemStream.Seek(partitionInfo.FirstSector * drive.SectorSize, SeekOrigin.Begin);
			fileSystem = GetStreamFileSystem(fileSystemStream);
			fileSystemPath = string.Join('\\', pathParts.AsSpan()[1..]!);
		}
		else
		{
			fileSystem = GetStreamFileSystem(drive.Content);
			fileSystemPath = string.Join('\\', pathParts);
		}
		
		if (fileSystem == null)
		{
			drive.Dispose();
			return false;
		}
		
		bool result = fileSystem.Exists(fileSystemPath);
		
		fileSystem.Dispose();
		drive.Dispose();
		
		return result;
	}

	/// <summary>
	/// Deletes the given item from the drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive that contains the item to delete. driveId >= 1.</param>
	/// <param name="path">The path on the drive, points to the item to delete. Must not point to a partition. path != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. The given path is valid, and does not point to a partition. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: On success, the given item is deleted and the returned exit code indicates success. <br/>
	/// On failure, the item is not deleted and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DeleteItemAsync(int driveId, string path)
	{
		if (driveId < 1)
			return ExitCode.InvalidParameter;
		
		string trimmedPath = path.Trim(SharedDefinitions.DirectorySeparators);
		string[] pathParts = trimmedPath.Split(SharedDefinitions.DirectorySeparators);

		if (pathParts.Length == 0 || (pathParts.Length == 1 && string.IsNullOrEmpty(pathParts[0])))
			return await DeleteDriveAsync(driveId);
		
		Disk drive;
		try
		{
			drive = new Disk(GetDriveFilePath(driveId));
		}
		catch (Exception)
		{
			return ExitCode.DriveDoesntExist;
		}
		
		DiscFileSystem? fileSystem = null;
		string fileSystemPath;
		if (IsDrivePartitioned(drive))
		{
			/* First part of the path should contain the partition index if the drive is partitioned. */
			if (!int.TryParse(pathParts[0], out int partitionIndex) || partitionIndex < 0 || partitionIndex >= drive.Partitions.Count)
			{
				drive.Dispose();
				return ExitCode.InvalidPath;
			}

			/* Deleting partitions is not supported. */
			if (pathParts.Length == 1)
			{
				drive.Dispose();
				return ExitCode.UnsupportedOperation;
			}

			PartitionInfo partitionInfo = drive.Partitions[partitionIndex];
			Stream fileSystemStream = partitionInfo.Open();
			fileSystemStream.Seek(partitionInfo.FirstSector * drive.SectorSize, SeekOrigin.Begin);
			
			fileSystem = GetStreamFileSystem(fileSystemStream);
			fileSystemPath = string.Join('\\', pathParts.AsSpan()[1..]!);
		}
		else
		{
			fileSystem = GetStreamFileSystem(drive.Content);
			fileSystemPath = string.Join('\\', pathParts);
		}

		if (fileSystem == null)
		{
			drive.Dispose();
			return ExitCode.UnsupportedFileSystem;
		}

		ExitCode result = ExitCode.Success;
		try
		{
			if (fileSystem.FileExists(fileSystemPath))
				fileSystem.DeleteFile(fileSystemPath);
			
			else if (fileSystem.DirectoryExists(fileSystemPath))
				fileSystem.DeleteDirectory(fileSystemPath, true);
			
			else
				result = ExitCode.ItemDoesntExist;
		}
		catch (NotSupportedException)
		{
			result = ExitCode.UnsupportedFileSystem;
		}
		catch (Exception)
		{
			result = ExitCode.InvalidPath;
		}

		fileSystem.Dispose();
		drive.Dispose();
		
		return result;
	}

	/// <summary>
	/// Checks if the given drive is partitioned or not. <br/>
	/// Do NOT use Disk.IsPartitioned - There is a bug in that, that is the reason this method exists.
	/// </summary>
	/// <param name="drive">The drive to check if partitioned. drive != null.</param>
	/// <returns>True if the given drive is partitioned, false otherwise.</returns>
	/// <remarks>
	/// Precondition: drive != null. <br/>
	/// Postcondition: Returns true if the given drive is partitioned, false otherwise.
	/// </remarks>
	private bool IsDrivePartitioned(Disk drive)
	{
		Stream filesystem = drive.Content;
		filesystem.Seek(0, SeekOrigin.Begin);
		return drive.IsPartitioned && GetStreamFileSystem(filesystem) == null;
	}
	
	/// <summary>
	/// Lists items under the given path in the given filesystem. (which is the given stream)
	/// </summary>
	/// <param name="stream">The stream representing the filesystem on the disk. stream != null.</param>
	/// <param name="path">The path under which to list the items on. path != null.</param>
	/// <returns>An array of path items representing the items under the given path, or null on failure.</returns>
	/// <remarks>
	/// Precondition: The given stream represents a valid and supported filesystem. The given path exists and is in valid syntax.
	/// stream != null &amp;&amp; path != null <br/>
	/// Postcondition: On success, an array of path items representing the items under the given path is returned. On failure, null is returned.
	/// </remarks>
	private PathItem[]? ListItemsOnFileSystemPath(Stream stream, string path)
	{
		string pathTrimmed = path.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		
		DiscFileSystem? fileSystem = GetStreamFileSystem(stream);
		if (fileSystem == null)		/* Unsupported filesystem. */
			return null;

		if (!fileSystem.DirectoryExists(pathTrimmed))
			return null;
		
		string[] filePaths = fileSystem.GetFiles(pathTrimmed);
		string[] directoryPaths = fileSystem.GetDirectories(pathTrimmed);
		PathItem[] items = new PathItem[filePaths.Length + directoryPaths.Length];
		int index = 0;

		foreach (string filePath in filePaths)
		{
			DateTime accessed = DateTime.MaxValue;
			DateTime modified = DateTime.MaxValue; 
			DateTime created = DateTime.MaxValue;
			
			try { accessed = fileSystem.GetLastAccessTime(filePath); } catch (Exception) { }
			try { modified = fileSystem.GetLastWriteTime(filePath); } catch (Exception) { }
			try { created = fileSystem.GetCreationTime(filePath); } catch (Exception) { }

			ulong sizeBytes = ulong.MaxValue;

			try { sizeBytes = (ulong)fileSystem.GetFileLength(filePath); } catch (Exception) { }
			
			items[index++] = new PathItemFile(
				filePath.Split(SharedDefinitions.DirectorySeparators).Last(),
				sizeBytes, accessed, modified, created
			);
		}

		foreach (string directoryPath in directoryPaths)
		{
			DateTime modified = DateTime.MaxValue;
			DateTime created = DateTime.MaxValue;
			
			try { modified = fileSystem.GetLastWriteTime(directoryPath); } catch (Exception) { }
			try { created = fileSystem.GetCreationTime(directoryPath); } catch (Exception) { }
			
			items[index++] = new PathItemDirectory(
				directoryPath.Split(SharedDefinitions.DirectorySeparators).Last(),
				modified, created
			);
		}

		fileSystem.Dispose();
		return items;
	}

	/// <summary>
	/// Get the filesystem object of a filesystem under the given stream. (if any)
	/// </summary>
	/// <param name="stream">The stream that represents a filesystem. stream != null.</param>
	/// <returns>A filesystem object representing the underlying filesystem in the given stream, or null if the filesystem is unsupported.</returns>
	/// <remarks>
	/// Precondition: The given stream represents a valid and supported filesystem. stream != null. <br/>
	/// Postcondition: On success, a filesystem object representing the underlying filesystem is returned. On failure, null is returned.
	/// </remarks>
	private DiscFileSystem? GetStreamFileSystem(Stream stream)
	{
		DiscFileSystem? fileSystem;
		if (FatFileSystem.Detect(stream))
		{
			fileSystem = new FatFileSystem(stream, Ownership.None);
		}	
		else if (CDReader.Detect(stream))
		{
			fileSystem = new CDReader(stream, true);
		}
		else if (SquashFileSystemReader.Detect(stream))
		{
			fileSystem = new SquashFileSystemReader(stream);
		}
		else
		{
			try
			{
				fileSystem = new ExtFileSystem(stream, new FileSystemParameters());
			}
			catch (Exception)
			{
				fileSystem = null;
			}
		}

		if (fileSystem == null)
		{
			try
			{
				fileSystem = new HfsPlusFileSystem(stream);
			}
			catch (Exception)
			{
				fileSystem = null;
			}
		}

		return fileSystem;
	}
	
	private string GetVmNexusFolderPath() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.VM-Nexus";
}