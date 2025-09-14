using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Shared;

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
	/// <param name="username">The username of the user under which to create the drive on. username != null.</param>
	/// <param name="driveName">The name of the new drive. driveName != null.</param>
	/// <param name="operatingSystem">The operating system to install on the drive. operatingSystem != SharedDefinitions.OperatingSystem.Other.</param>
	/// <param name="size">The size of the new drive in MiB. size >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given username must exist. There must not be a drive named under the given name. <br/>
	/// username != null &amp;&amp; driveName != null &amp;&amp; operatingSystem != SharedDefinitions.OperatingSystem.Other &amp;&amp; size >= 1. <br/>
	/// Postcondition: On success, the new drive is created and registered, and the returned exit code indicates success. <br/>
	/// On failure, the drive is not created and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> CreateOperatingSystemDriveAsync(string username, string driveName,
		SharedDefinitions.OperatingSystem operatingSystem, int size)
	{
		if (!Enum.IsDefined(typeof(SharedDefinitions.OperatingSystem), operatingSystem) || operatingSystem == SharedDefinitions.OperatingSystem.Other)
		{
			return ExitCode.InvalidParameter;
		}

		SharedDefinitions.DriveType driveType = operatingSystem == SharedDefinitions.OperatingSystem.MiniCoffeeOS
			? SharedDefinitions.DriveType.Floppy
			: SharedDefinitions.DriveType.Disk;
		
		ExitCode result = await _databaseService.CreateDriveAsync(username, driveName, size, driveType);
		if (result != ExitCode.Success)
		{
			return result;
		}

		int driveId = await GetDriveIdAsync(username, driveName);	/* Just successfully created the drive - this must succeed. */
		string driveFileName = GetDriveFileName(driveId);
		string driveFilePath = GetDriveFilePath(driveId);
	
		long driveSize = (long)size * 1024 * 1024;		/* The drive size in bytes */

		if (operatingSystem == SharedDefinitions.OperatingSystem.MiniCoffeeOS)
		{
			Process process = new Process()
			{
				StartInfo = new ProcessStartInfo()
				{
					FileName = "/usr/bin/make",
					Arguments = $" -C ../../../MiniCoffeeOS FDA=../DiskImages/{driveFileName} FDA_SIZE={size}",
				},
			};
			process.Start();
			await process.WaitForExitAsync();
			int exitCode = process.ExitCode;
			process.Dispose();

			if (exitCode != 0)
			{
				await DeleteDriveAsync(username, driveName);
				return ExitCode.DiskImageCreationFailed;
			}
		}
		else
		{
			string osDiskImageName;
			switch (operatingSystem)
			{
				case SharedDefinitions.OperatingSystem.Ubuntu:
					osDiskImageName = "Ubuntu.raw";
					break;
				case SharedDefinitions.OperatingSystem.FedoraLinux:
					osDiskImageName = "Fedora.raw";
					break;
				case SharedDefinitions.OperatingSystem.KaliLinux:
					osDiskImageName = "Kali.raw";
					break;
				case SharedDefinitions.OperatingSystem.ManjaroLinux:
					osDiskImageName = "Manjaro.raw";
					break;
				default:
					return ExitCode.InvalidParameter;	/* This cant be reached because of the if statement above. Doing it for the C# compiler. */
			}

			/* This is faster than File.Copy */
			Process? copyProc = Process.Start(new ProcessStartInfo()
			{
				FileName = "/bin/cp",
				ArgumentList = { "../../../OsDiskImages/" + osDiskImageName, driveFilePath },
				UseShellExecute = false,
			});
			await copyProc!.WaitForExitAsync();

			if (copyProc.ExitCode != 0)
			{
				await DeleteDriveAsync(username, driveName);
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
				await DeleteDriveAsync(username, driveName);
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
				await DeleteDriveAsync(username, driveName);
				return ExitCode.DiskImageCreationFailed;
			}
		}

		return ExitCode.Success;
	}

	/// <summary>
	/// Get the ID of a drive called name, owned by the given user.
	/// </summary>
	/// <param name="username">The username of the user to search the drive under. username != null.</param>
	/// <param name="name">The name of the drive to search for. name != null.</param>
	/// <returns>The ID of the drive on success, -1 on failure.</returns>
	/// <remarks>
	/// Precondition: A user called by username exists. The given user has a drive called by name. username != null &amp;&amp; name != null. <br/>
	/// Postcondition: On success, the ID of the drive is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<int> GetDriveIdAsync(string username, string name)
	{
		return await _databaseService.GetDriveIdAsync(username, name);
	}

	/// <summary>
	/// Check if the given user has a drive called by name.
	/// </summary>
	/// <param name="username">The username of the user to search the drive on. username != null.</param>
	/// <param name="name">The name of the drive to search for. name != null.</param>
	/// <returns>True if the drive exists, false if not or on failure.</returns>
	/// <remarks>
	/// Precondition: A user which name is username must exist. username != null &amp;&amp; name != null. <br/>
	/// Postcondition: Returns whether a drive called by the given name exists under the given user. Returns false on failure.
	/// </remarks>
	public async Task<bool> IsDriveExistsAsync(string username, string name)
	{
		return await _databaseService.IsDriveExistsAsync(username, name);
	}

	/// <summary>
	/// Deletes the given drive.
	/// </summary>
	/// <param name="username">The username of the user who owns the drive. username != null.</param>
	/// <param name="name">The name of the drive. name != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user whos username is the given username - exists. The user has a drive which name is the given name.
	/// username != null &amp;&amp; name != null. <br/>
	/// Postcondition: On success, the drive is deleted and the returned exit code indicates success. <br/>
	/// On failure, the drive is not deleted and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DeleteDriveAsync(string username, string name)
	{
		string? filePath = await GetDriveFilePathAsync(username, name);
		if (filePath == null)
		{
			return ExitCode.DriveDoesntExist;
		}
		
		File.Delete(filePath);
	
		return await _databaseService.DeleteDriveAsync(username, name);
	}

	/// <summary>
	/// Registers a drive-VM connection. (Means that when the VM starts, the drive will be connected to it.)
	/// </summary>
	/// <param name="username">The username on which to register the connection. username != null.</param>
	/// <param name="driveName">The name of the drive to connect. driveName != null.</param>
	/// <param name="vmName">The name of the virtual machine to connect the drive to. vmName != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given username exists, a drive with the given name exists,
	/// a virtual machine with the given name exists, and there is no such drive connection that already exists. <br/>
	/// username != null &amp;&amp; driveName != null &amp;&amp; vmName != null. <br/>
	/// Postcondition: On success, the drive connection is registered and the returned exit code states success. <br/>
	/// On failure, the connection is not registered and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> ConnectDriveAsync(string username, string driveName, string vmName)
	{
		return await _databaseService.ConnectDriveAsync(username, driveName, vmName);
	}
	
	/// <summary>
	/// Get the filename (disk image filename) of a drive that is owned by the given user and named by the given name.
	/// </summary>
	/// <param name="username">The username of the user that owns the drive. username != null.</param>
	/// <param name="name">The name of the drive. name != null.</param>
	/// <returns>The filename of the drives disk image file. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given username must exist. The user owns a drive named by the given name.
	/// username != null &amp;&amp; name != null. <br/>
	/// Postcondition: On success, the filename of the drives disk image file is returned. On failure, null is returned.
	/// </remarks>
	public async Task<string?> GetDriveFileNameAsync(string username, string name)
	{
		int driveId = await GetDriveIdAsync(username, name);
		if (driveId == -1)
		{
			return null;
		}
		
		return GetDriveFileName(driveId);
	}

	/// <summary>
	/// Get the file path (path to the disk image) of a drive that is owned by the given user and named by the given name. <br/>
	/// Note: The file path is relative to the path of the final server executable
	/// </summary>
	/// <param name="username">The username of the user that owns the drive. username != null.</param>
	/// <param name="name">The name of the drive. name != null.</param>
	/// <returns>The file path of the drives disk image file. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given username must exist. The user owns a drive named by the given name.
	/// username != null &amp;&amp; name != null. <br/>
	/// Postcondition: On success, the file path of the drives disk image file is returned. On failure, null is returned.
	/// </remarks>
	public async Task<string?> GetDriveFilePathAsync(string username, string name)
	{
		int driveId = await GetDriveIdAsync(username, name);
		if (driveId == -1)
		{
			return null;
		}

		return GetDriveFilePath(driveId);
	}

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
	/// The file path is relative to the path of the final server executable.
	/// </summary>
	/// <param name="driveId">The ID of the drive. driveId >= 1.</param>
	/// <returns>The file path to the disk image file of the drive.</returns>
	/// <remarks>
	/// Precondition: driveId >= 1. <br/>
	/// Postcondition: The file path to the disk image file of the drive is returned.
	/// </remarks>
	public string GetDriveFilePath(int driveId) => "../../../DiskImages/" + GetDriveFileName(driveId);
}