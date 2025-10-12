using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Server.Models;
using Shared;

namespace Server.Services;

/* Manages logged-in users */
public class UserService
{
	private readonly ConcurrentDictionary<int, List<ClientConnection>> _users;		/* By user ID */
	private readonly ConcurrentDictionary<int, HashSet<int>> _userIdsByVmId;
	private readonly ConcurrentDictionary<int, HashSet<int>> _userIdsByDriveId;
	
	private readonly DatabaseService _databaseService;
	
	public UserService(DatabaseService databaseService)
	{
		_users = new ConcurrentDictionary<int, List<ClientConnection>>();
		_userIdsByVmId = new ConcurrentDictionary<int, HashSet<int>>();
		_userIdsByDriveId = new ConcurrentDictionary<int, HashSet<int>>();
		
		_databaseService = databaseService;
	}

	public ExitCode Close()
	{
		foreach (List<ClientConnection> userConnections in _users.Values)
		{
			foreach (ClientConnection connection in userConnections)
			{
				connection.Disconnect();
			}
		}
		
		return ExitCode.Success;
	}

	public async Task<ExitCode> LoginAsync(string username, string password, ClientConnection connection)
	{
		int userId = await _databaseService.GetUserIdAsync(username);

		if (userId == -1)
			return ExitCode.UserDoesntExist;
		
		if (IsUserLoggedIn(userId)) 
			return ExitCode.UserAlreadyLoggedIn;
		
		bool valid = await _databaseService.IsValidLoginAsync(username, password);

		if (valid)
		{
			await AddUserConnectionAsync(connection, userId);
			return ExitCode.Success;
		}
		
		return ExitCode.InvalidLoginCredentials;
	}
	
	public void Disconnect(int userId)
	{
		throw new NotImplementedException();
	}

	public void Logout(int userId)
	{
		throw new NotImplementedException();
	}

	private bool IsUserLoggedIn(int userId) => _users.ContainsKey(userId);

	private async Task AddUserConnectionAsync(ClientConnection connection, int userId)
	{
		if (userId < 1)
			return;
		
		if (!_users.TryGetValue(userId, out List<ClientConnection>? userConnections))
		{
			userConnections = new List<ClientConnection>();
			_users[userId] = userConnections;
		}
		userConnections.Add(connection);

		Task initUserVmsTask = InitializeUserVmsAsync(userId);
		Task initUserDrivesTask = InitializeUserDrivesAsync(userId);
		
		await Task.WhenAll(initUserVmsTask, initUserDrivesTask);
	}

	private async Task InitializeUserVmsAsync(int userId)
	{
		if (userId < 1)
			return;
		
		int[]? vmIds = await _databaseService.GetVmIdsOfUserAsync(userId);
	
		if (vmIds == null)
			return;
		
		foreach (int vmId in vmIds)
		{
			AddVirtualMachine(userId, vmId);
		}
	}

	private async Task InitializeUserDrivesAsync(int userId)
	{
		if (userId < 1)
			return;
		
		int[]? driveIds = await _databaseService.GetDriveIdsOfUserAsync(userId);
		
		if (driveIds == null)
			return;

		foreach (int driveId in driveIds)
		{
			AddDrive(userId, driveId);
		}
	}

	private void AddVirtualMachine(int userId, int vmId)
	{
		if (userId < 1 || vmId < 1)
			return;
		
		if (!_userIdsByVmId.TryGetValue(vmId, out HashSet<int>? userIds))
		{
			userIds = new HashSet<int>();
			_userIdsByVmId[vmId] = userIds;
		}
		
		userIds.Add(userId);
	}

	private void AddDrive(int userId, int driveId)
	{
		if (userId < 1 || driveId < 1)
			return;
		
		if (!_userIdsByDriveId.TryGetValue(driveId, out HashSet<int>? userIds))
		{
			userIds = new HashSet<int>();
			_userIdsByDriveId[driveId] = userIds;
		}
		
		userIds.Add(userId);
	}
}