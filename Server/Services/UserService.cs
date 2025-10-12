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
		throw new NotImplementedException();
		
		int userId = await _databaseService.GetUserIdAsync(username);

		if (userId == -1)
			return ExitCode.UserDoesntExist;
		
		if (IsUserLoggedIn(userId)) 
			return ExitCode.UserAlreadyLoggedIn;
		
		bool valid = await _databaseService.IsValidLoginAsync(username, password);

		if (valid)
		{
			/* TODO: Add user */
			
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

	private async Task AddUserConnectionAsync(ClientConnection connection)
	{
		throw new NotImplementedException();
		
		if (IsUserLoggedIn(connection.UserId))
			return;

		if (!_users.TryGetValue(connection.UserId, out List<ClientConnection>? userConnections))
		{
			userConnections = new List<ClientConnection>();
			_users[connection.UserId] = userConnections;
		}
		userConnections.Add(connection);

		Task initUserVmsTask = InitializeUserVmsAsync(connection.UserId);
		
		await Task.WhenAll(initUserVmsTask);
	}

	private async Task InitializeUserVmsAsync(int userId)
	{
		int[]? vmIds = await _databaseService.GetVmIdsOfUserAsync(userId);
		if (vmIds != null)
		{
			foreach (int vmId in vmIds)
			{
				if (!_userIdsByVmId.TryGetValue(vmId, out HashSet<int>? userIdsByVmId))
				{
					userIdsByVmId = new HashSet<int>();
					_userIdsByVmId[vmId] = userIdsByVmId;
				}
				userIdsByVmId.Add(vmId);
			}
		}
	}
}