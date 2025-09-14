using System.IO;
using System.Net;
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
	public string GetDriveFilePath(int driveId) => "../../../" + GetDriveFileName(driveId);
}