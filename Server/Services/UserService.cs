using System;
using System.Collections.Concurrent;
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
	
	private readonly DatabaseService _databaseService;
	
	public UserService(DatabaseService databaseService)
	{
		_users = new ConcurrentDictionary<int, ConcurrentDictionary<Guid, ClientConnection>>();
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
			AddUserConnection(connection, userId);
			UserLoggedIn?.Invoke(this, connection);
			return ExitCode.Success;
		}
		
		return ExitCode.InvalidLoginCredentials;
	}

	public void Logout(ClientConnection connection)
	{
		RemoveUserConnection(connection);
		UserLoggedOut?.Invoke(this, connection);
	}
	
	public async Task NotifyVirtualMachineCreatedAsync(SharedDefinitions.VmGeneralDescriptor descriptor)
	{
		int[]? relatedUsers = await _databaseService.GetUserIdsRelatedToVmAsync(descriptor.Id);
		if (relatedUsers == null) 
			return;

		foreach (int userId in relatedUsers)
		{
			if (_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
			{
				foreach (ClientConnection connection in userConnections.Values)
				{
					connection.NotifyVirtualMachineCreated(descriptor);
				}
			}
		}
	}

	public async Task NotifyVirtualMachineDeletedAsync(int vmId)
	{
		int[]? relatedUsers = await _databaseService.GetUserIdsRelatedToVmAsync(vmId);
		if (relatedUsers == null)
			return;

		foreach (int userId in relatedUsers)
		{
			if (_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
			{
				foreach (ClientConnection connection in userConnections.Values)
				{
					connection.NotifyVirtualMachineDeleted(vmId);
				}
			}
		}
	}

	public async Task NotifyDriveCreatedAsync(SharedDefinitions.DriveGeneralDescriptor descriptor)
	{
		int[]? relatedUsers = await _databaseService.GetUserIdsRelatedToDriveAsync(descriptor.Id);
		if (relatedUsers == null)
			return;

		foreach (int userId in relatedUsers)
		{
			if (_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
			{
				foreach (ClientConnection connection in userConnections.Values)
				{
					connection.NotifyDriveCreated(descriptor);
				}
			}
		}
	}

	public async Task NotifyDriveDeletedAsync(int driveId)
	{
		int[]? relatedUsers = await _databaseService.GetUserIdsRelatedToDriveAsync(driveId);
		if (relatedUsers == null)
			return;

		foreach (int id in relatedUsers)
		{
			if (_users.TryGetValue(id, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
			{
				foreach (ClientConnection connection in userConnections.Values)
				{
					connection.NotifyDriveDeleted(driveId);
				}
			}
		}
	}
	
	private void AddUserConnection(ClientConnection connection, int userId)
	{
		if (userId < 1)
			return;
		
		if (!_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
		{
			userConnections = new ConcurrentDictionary<Guid, ClientConnection>();
			_users[userId] = userConnections;
		}
		userConnections.TryAdd(connection.ClientId, connection);
		
		connection.Disconnected += OnUserDisconnected;
	}
	
	private void RemoveUserConnection(ClientConnection connection)
	{
		connection.Disconnected -= OnUserDisconnected;
		
		if (_users.TryGetValue(connection.UserId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
		{
			userConnections.TryRemove(connection.ClientId, out _);
			if (userConnections.IsEmpty)
			{
				_users.TryRemove(connection.UserId, out _);
			}
		}
	}

	private void OnUserDisconnected(object? sender, EventArgs e)
	{
		if (sender == null || sender is not ClientConnection connection)
			return;
		
		RemoveUserConnection(connection);
	}
}