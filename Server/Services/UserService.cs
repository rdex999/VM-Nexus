using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Server.Models;
using Shared;
using Shared.Drives;
using Shared.VirtualMachines;

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

	/// <summary>
	/// Closes the service. Disconnects all logged-in clients.
	/// </summary>
	/// <remarks>
	/// Precondition: Service initialized. <br/>
	/// Postcondition: Service uninitialized. All logged-in clients are disconnected.
	/// </remarks>
	public void Close()
	{
		foreach (ConcurrentDictionary<Guid, ClientConnection> userConnections in _users.Values)
		{
			foreach (ClientConnection connection in userConnections.Values)
			{
				connection.Disconnect();
			}
		}
	}

	/// <summary>
	/// Attempts to log in a user with a given username and password.
	/// </summary>
	/// <param name="username">The username of the user to log in. username != null.</param>
	/// <param name="password">The password to use when logging-in the user. password != null.</param>
	/// <param name="connection">The connection to the user. connection != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service initialized. username != null &amp;&amp; password != null &amp;&amp; connection != null. <br/>
	/// Postcondition: If the login credentials are correct and no error has occurred,
	/// the user is logged in and the returned exit code indicates success. <br/>
	/// Otherwise, the returned exit code will indicate the result of the login attempt, or the type of failure that occured. (if any)
	/// </remarks>
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

	/// <summary>
	/// Logs-out the given user connection.
	/// </summary>
	/// <param name="connection">The user connection to log out. connection != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. The given user connection is logged-in. connection != null. <br/>
	/// Postcondition: The user is logged out.
	/// </remarks>
	public void Logout(ClientConnection connection)
	{
		RemoveUserConnection(connection);
		UserLoggedOut?.Invoke(this, connection);
	}

	/// <summary>
	/// Notifies all connections (ClientConnection) of the owner user of the new sub-user.
	/// </summary>
	/// <param name="subUser">The new sub-user that was created. subUser != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. A new sub-user was created. subUser != null. <br/>
	/// Postcondition: All connections (ClientConnection) of the owner user are notified of the new sub-user.
	/// </remarks>
	public void NotifySubUserCreated(SubUser subUser)
	{
		if (!_users.TryGetValue(subUser.OwnerId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
			return;
		
		foreach (ClientConnection connection in userConnections.Values)
			connection.NotifySubUserCreated(subUser);
	}
	
	/// <summary>
	/// Notifies related users of a virtual machine creation event.
	/// </summary>
	/// <param name="descriptor">A descriptor of the new virtual machine. descriptor != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. The given virtual machine was created. descriptor != null. <br/>
	/// Postcondition: Related users are notified of the new virtual machine.
	/// </remarks>
	public async Task NotifyVirtualMachineCreatedAsync(VmGeneralDescriptor descriptor)
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
					if (await connection.HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineList, descriptor.Id))
						connection.NotifyVirtualMachineCreated(descriptor);
				}
			}
		}
	}

	/// <summary>
	/// Notifies related users of a virtual machine deletion event.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that was deleted.</param>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine with the given ID exists,
	/// and will be deleted from the database after this method is called.
	/// descriptor != null. <br/>
	/// Postcondition: Related users are notified that the virtual machine was deleted.
	/// </remarks>
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
					if (await connection.HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineList, vmId))
						connection.NotifyVirtualMachineDeleted(vmId);
				}
			}
		}
	}

	/// <summary>
	/// Notifies related users that a virtual machine was powered on.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that was powered on. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine with the given ID exists, and was powered on. vmId >= 1. <br/>
	/// Postcondition: Related users are notified that the virtual machine was powered on.
	/// </remarks>
	public async Task NotifyVirtualMachinePoweredOnAsync(int vmId)
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
					if (await connection.HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineList, vmId))
						connection.NotifyVirtualMachinePoweredOn(vmId);
				}
			}
		}
	}

	/// <summary>
	/// Notifies related users that a virtual machine was powered off.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that was powered off. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine with the given ID exists, and was powered off. vmId >= 1. <br/>
	/// Postcondition: Related users are notified that the virtual machine was powered off.
	/// </remarks>
	public async Task NotifyVirtualMachinePoweredOffAsync(int vmId)
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
					if (await connection.HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineList, vmId))
						connection.NotifyVirtualMachinePoweredOff(vmId);
				}
			}
		}
	}

	/// <summary>
	/// Notifies related users that a virtual machine has crashed.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that has crashed. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine with the given ID exists, and has crashed. vmId >= 1. <br/>
	/// Postcondition: Related users are notified that the virtual machine has crashed.
	/// </remarks>
	public async Task NotifyVirtualMachineCrashedAsync(int vmId)
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
					if (await connection.HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineList, vmId))
						connection.NotifyVirtualMachineCrashed(vmId);
				}
			}
		}
	}

	/// <summary>
	/// Notifies related users that a drive was created.
	/// </summary>
	/// <param name="descriptor">A general descriptor of the new drive. descriptor != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. The given drive was created. descriptor != null. <br/>
	/// Postcondition: Related users are notified that the drive was created.
	/// </remarks>
	public async Task NotifyDriveCreatedAsync(DriveGeneralDescriptor descriptor)
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
					if (await connection.HasPermissionDriveOwnerAsync(UserPermissions.DriveList, descriptor.Id))
						connection.NotifyDriveCreated(descriptor);
				}
			}
		}
	}

	/// <summary>
	/// Notifies related users that an item (file/directory in a drive) was created.
	/// </summary>
	/// <param name="driveId">The ID of the drive that contains the new item. driveId >= 1.</param>
	/// <param name="path">A path to the new item, including the item's name. (filename/directory name). path != null</param>
	/// <remarks>
	/// Precondition: Service initialized. The given item exists. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: Related users are notified that the item was created.
	/// </remarks>
	public async Task NotifyItemCreatedAsync(int driveId, string path)
	{
		int[]? relatedUsers = await _databaseService.GetUserIdsRelatedToDriveAsync(driveId);
		if (relatedUsers == null)
			return;

		foreach (int id in relatedUsers)
		{
			if (!_users.TryGetValue(id, out ConcurrentDictionary<Guid, ClientConnection>? userConnections)) 
				continue;
			
			foreach (ClientConnection connection in userConnections.Values)
			{
				if (await connection.HasPermissionDriveOwnerAsync(UserPermissions.DriveItemList, driveId))
					connection.NotifyItemCreated(driveId, path);
			}
		}
	}
	
	/// <summary>
	/// Notifies related users that an item (file in a drive, or the drive itself) was deleted.
	/// </summary>
	/// <param name="driveId">The ID of the drive that contains the (will be) deleted item. driveId >= 1.</param>
	/// <param name="path">A path to the (will be) deleted item. path != null</param>
	/// <remarks>
	/// Precondition: Service initialized. The given item exists, and will be deleted after this method is called.
	/// driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: Related users are notified of the item deletion event. 
	/// </remarks>
	public async Task NotifyItemDeletedAsync(int driveId, string path)
	{
		int[]? relatedUsers = await _databaseService.GetUserIdsRelatedToDriveAsync(driveId);
		if (relatedUsers == null)
			return;

		foreach (int id in relatedUsers)
		{
			if (!_users.TryGetValue(id, out ConcurrentDictionary<Guid, ClientConnection>? userConnections)) 
				continue;
			
			foreach (ClientConnection connection in userConnections.Values)
			{
				if (await connection.HasPermissionDriveOwnerAsync(UserPermissions.DriveItemList, driveId))
					connection.NotifyItemDeleted(driveId, path);
			}
		}
	}

	/// <summary>
	/// Notifies related users that a new drive connection was created. (drive connected to virtual machine)
	/// </summary>
	/// <param name="driveId">The ID of the drive that is now connected to a virtual machine. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine that the drive is now connected to. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. The given drive and virtual machine exist, and are connected. driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: Related users are notified of the new drive connection.
	/// </remarks>
	public async Task NotifyDriveConnected(int driveId, int vmId)
	{
		int[]? relatedUsers = await _databaseService.GetUserIdsRelatedToVmAsync(vmId);
		if (relatedUsers == null)
			return;

		foreach (int userId in relatedUsers)
		{
			if (!_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
				continue;
			
			foreach (ClientConnection connection in userConnections.Values)
			{
				if (await connection.HasPermissionDriveOwnerAsync(UserPermissions.DriveConnectionList, driveId))
					connection.NotifyDriveConnected(driveId, vmId);
			}
		}
	}

	/// <summary>
	/// Notifies related users that a drive connection was removed. (drive disconnected from virtual machine)
	/// </summary>
	/// <param name="driveId">The ID of the drive that is now disconnected from the virtual machine. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine that the drive was disconnected from. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. The given drive and virtual machine exist, and are disconnected. driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: Related users are notified of the drive disconnection.
	/// </remarks>
	public async Task NotifyDriveDisconnected(int driveId, int vmId)
	{
		int[]? relatedUsers = await _databaseService.GetUserIdsRelatedToVmAsync(vmId);
		if (relatedUsers == null)
			return;

		foreach (int userId in relatedUsers)
		{
			if (!_users.TryGetValue(userId, out ConcurrentDictionary<Guid, ClientConnection>? userConnections))
				continue;
			
			foreach (ClientConnection connection in userConnections.Values)
			{
				if (await connection.HasPermissionDriveOwnerAsync(UserPermissions.DriveConnectionList, driveId))
					connection.NotifyDriveDisconnected(driveId, vmId);
			}
		}
	}

	/// <summary>
	/// Adds the given connection as a logged-in user, and keeps track of it.
	/// </summary>
	/// <param name="connection">The connection to the user. connection != null.</param>
	/// <param name="userId">The user ID of the now logged-in user. userId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. The user is logged-in. connection != null &amp;&amp; userId >= 1. <br/>
	/// Postcondition: The user is added as a logged-in user.
	/// </remarks>
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

	/// <summary>
	/// Removes a user connection from logged-in users.
	/// </summary>
	/// <param name="connection">The user connection. connection != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. The given user connection is in local data structures.
	/// (AddUserConnection was called on it) connection != null. <br/>
	/// Postcondition: The user connection is removed.
	/// </remarks>
	private void RemoveUserConnection(ClientConnection connection)
	{
		connection.Disconnected -= OnUserDisconnected;
		
		if (!connection.IsLoggedIn)
			return;

		if (!_users.TryGetValue(connection.ActualUser!.Id, out ConcurrentDictionary<Guid, ClientConnection>? userConnections)) 
			return;
		
		userConnections.TryRemove(connection.ClientId, out _);
		if (userConnections.IsEmpty)
		{
			_users.TryRemove(connection.ActualUser.Id, out _);
		}
	}

	/// <summary>
	/// Handles a user disconnection. Removes the user.
	/// </summary>
	/// <param name="sender">The user connection that was disconnected. sender != null &amp;&amp; sender is ClientConnection.</param>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: Service initialized. A user connection has disconnected. sender != null &amp;&amp; sender is ClientConnection. <br/>
	/// Postcondition: The user connection is removed.
	/// </remarks>
	private void OnUserDisconnected(object? sender, EventArgs e)
	{
		if (sender == null || sender is not ClientConnection connection)
			return;
		
		RemoveUserConnection(connection);
	}
}