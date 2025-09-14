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
}