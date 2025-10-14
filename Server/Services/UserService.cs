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
	public event EventHandler<ClientConnection>? UserLoggedIn;
	public event EventHandler<ClientConnection>? UserLoggedOut;
	
	private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, ClientConnection>> _users;		/* By user ID, then by client ID */
	private readonly ConcurrentDictionary<int, HashSet<int>> _userIdsByVmId;
	private readonly ConcurrentDictionary<int, HashSet<int>> _userIdsByDriveId;
	
	private readonly DatabaseService _databaseService;
	
	public UserService(DatabaseService databaseService)
	{
		_users = new ConcurrentDictionary<int, ConcurrentDictionary<Guid, ClientConnection>>();
		_userIdsByVmId = new ConcurrentDictionary<int, HashSet<int>>();
		_userIdsByDriveId = new ConcurrentDictionary<int, HashSet<int>>();
		
		_databaseService = databaseService;
	}

	public ExitCode Close()
	{
		foreach (ConcurrentDictionary<Guid, ClientConnection> userConnections in _users.Values)
		{
			foreach (ClientConnection connection in userConnections.Values)
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
		
		bool valid = await _databaseService.IsValidLoginAsync(username, password);

		if (valid)
		{
			await AddUserConnectionAsync(connection, userId);
			UserLoggedIn?.Invoke(this, connection);
			return ExitCode.Success;
		}
		
		return ExitCode.InvalidLoginCredentials;
	}

	public async Task LogoutAsync(ClientConnection connection)
	{
		await RemoveUserConnectionAsync(connection);
		UserLoggedOut?.Invoke(this, connection);
	}
	
	public async Task NotifyVirtualMachineCreatedAsync(SharedDefinitions.VmGeneralDescriptor descriptor)
	{
		/* Get users related to the virtual machine */
		/* For now assume the owner is the only related user */
		int[] relatedUsers = [-1];
		relatedUsers[0] = await _databaseService.GetVmOwnerIdAsync(descriptor.Id);

		foreach (int userId in relatedUsers)
		{
			AddVirtualMachine(userId, descriptor.Id);

			if (_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
			{
				foreach (ClientConnection connection in userConnections.Values)
				{
					connection.NotifyVirtualMachineCreated(descriptor);
				}
			}
		}
	}

	public void NotifyVirtualMachineDeleted(int vmId)
	{
		if (_userIdsByVmId.TryRemove(vmId, out HashSet<int>? userIds))
		{
			foreach (int id in userIds)
			{
				if (_users.TryGetValue(id, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
				{
					foreach (ClientConnection connection in userConnections.Values)
					{
						connection.NotifyVirtualMachineDeleted(vmId);
					}
				}
			}
		}
	}

	public async Task NotifyDriveCreated(SharedDefinitions.DriveGeneralDescriptor descriptor)
	{
		int[] relatedUsers = [-1];
		relatedUsers[0] = await _databaseService.GetDriveOwnerIdAsync(descriptor.Id);

		foreach (int userId in relatedUsers)
		{
			AddDrive(userId, descriptor.Id);

			if (_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
			{
				foreach (ClientConnection connection in userConnections.Values)
				{
					connection.NotifyDriveCreated(descriptor);
				}
			}
		}
	}
	
	private async Task AddUserConnectionAsync(ClientConnection connection, int userId)
	{
		if (userId < 1)
			return;
		
		if (_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
		{
			userConnections.TryAdd(connection.ClientId, connection);
		}
		else
		{
			userConnections = new ConcurrentDictionary<Guid, ClientConnection>();
			userConnections.TryAdd(connection.ClientId, connection);
			_users[userId] = userConnections;
			
			Task initUserVmsTask = InitializeUserVmsAsync(userId);
			Task initUserDrivesTask = InitializeUserDrivesAsync(userId);
			await Task.WhenAll(initUserVmsTask, initUserDrivesTask);
		}
		
		connection.Disconnected += OnUserDisconnected;
	}
	
	private async Task RemoveUserConnectionAsync(ClientConnection connection)
	{
		connection.Disconnected -= OnUserDisconnected;
		
		if (_users.TryGetValue(connection.UserId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
		{
			userConnections.TryRemove(connection.ClientId, out _);
			if (userConnections.IsEmpty)
			{
				Task removeUserFromVmsTask = RemoveUserFromVmsAsync(connection.UserId);
				Task removeUserFromDrivesTask = RemoveUserFromDrivesAsync(connection.UserId);
					
				await Task.WhenAll(removeUserFromVmsTask, removeUserFromDrivesTask);
				
				_users.TryRemove(connection.UserId, out _);
			}
		}
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

	private async Task RemoveUserFromVmsAsync(int userId)
	{
		if (userId < 1)
			return;
		
		int[]? vmIds = await _databaseService.GetVmIdsOfUserAsync(userId);
		if (vmIds != null)
		{
			foreach (int vmId in vmIds)
			{
				if (_userIdsByVmId.TryGetValue(vmId, out HashSet<int>? userIds))
				{
					userIds.Remove(userId);

					if (userIds.Count == 0)
					{
						_userIdsByVmId.TryRemove(vmId, out _);
					}
				}
			}
		}
	}

	private async Task RemoveUserFromDrivesAsync(int userId)
	{
		if (userId < 1)
			return;
		
		int[]? driveIds = await _databaseService.GetDriveIdsOfUserAsync(userId);
		if (driveIds != null)
		{
			foreach (int driveId in driveIds)
			{
				if (_userIdsByDriveId.TryGetValue(driveId, out HashSet<int>? userIds))
				{
					userIds.Remove(userId);

					if (userIds.Count == 0)
					{
						_userIdsByDriveId.TryRemove(driveId, out _);
					}
				}
			}
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
	
	private void OnUserDisconnected(object? sender, EventArgs e)
	{
		if (sender == null || sender is not ClientConnection connection)
			return;
		
		_ = RemoveUserConnectionAsync(connection);
	}
}