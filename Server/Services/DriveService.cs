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
using DiscUtils.Partitions;
using DiscUtils.Raw;
using DiscUtils.SquashFs;
using DiscUtils.Streams;
using Server.Drives;
using Shared;
using Shared.Drives;

namespace Server.Services;

public class DriveService
{
	private DatabaseService _databaseService;

	public DriveService(DatabaseService databaseService)
	{
		_databaseService = databaseService;
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
		{
			return result;
		}

		int driveId = await GetDriveIdAsync(userId, driveName);	/* Just successfully created the drive - this must succeed. */
		string driveFilePath = GetDriveFilePath(driveId);
	
		long driveSize = (long)size * 1024 * 1024;		/* The drive size in bytes */

		if (operatingSystem == Shared.VirtualMachines.OperatingSystem.MiniCoffeeOS)
		{
			Process process = new Process()
			{
				StartInfo = new ProcessStartInfo()
				{
					FileName = "/usr/bin/make",
					Arguments = $" -C ../../../MiniCoffeeOS FDA={driveFilePath} FDA_SIZE={size}",
				},
			};
			process.Start();
			await process.WaitForExitAsync();
			int exitCode = process.ExitCode;
			process.Dispose();

			if (exitCode != 0)
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
			drive = new Disk(GetDriveFilePath(driveId));
		}
		catch (Exception)
		{
			return null;
		}
		
		Stream filesystemStream;
		List<PathItem> items = new List<PathItem>();
		if (drive.IsPartitioned)
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
			drive = new Disk(GetDriveFilePath(driveId));
		}
		catch (Exception)
		{
			return (PartitionTableType)(-1);
		}

		PartitionTableType type = PartitionTableType.Unpartitioned;
		if (drive.IsPartitioned)
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
			using Disk drive = new Disk(GetDriveFilePath(driveId));
			return drive.SectorSize;
		}
		catch (Exception)
		{
			return -1;
		}
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
			DateTime accessed = fileSystem.GetLastAccessTime(filePath);
			DateTime modified = fileSystem.GetLastWriteTime(filePath);
			DateTime created = fileSystem.GetCreationTime(filePath);
			long sizeBytes = fileSystem.GetFileLength(filePath);
			items[index++] = new PathItemFile(
				filePath.Split(SharedDefinitions.DirectorySeparators).Last(),
				sizeBytes, accessed, modified, created
			);
		}

		foreach (string directoryPath in directoryPaths)
		{
			DateTime modified = fileSystem.GetLastWriteTime(directoryPath);
			DateTime created = fileSystem.GetCreationTime(directoryPath);
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