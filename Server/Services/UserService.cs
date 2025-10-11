using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Server.Models;
using Shared;

namespace Server.Services;

/* Manages logged-in users */
public class UserService
{
	private readonly ConcurrentDictionary<int, ClientConnection> _users;		/* By user ID */
	private readonly ConcurrentDictionary<int, HashSet<int>> _usersByVmId;
	private readonly ConcurrentDictionary<int, HashSet<int>> _usersByDriveId;
	
	public UserService()
	{
		_users = new ConcurrentDictionary<int, ClientConnection>();
		_usersByVmId = new ConcurrentDictionary<int, HashSet<int>>();
		_usersByDriveId = new ConcurrentDictionary<int, HashSet<int>>();
	}

	public ExitCode Close()
	{
		foreach (ClientConnection user in _users.Values)
		{
			user.Disconnect();
		}
		return ExitCode.Success;
	}

	public ExitCode Login(string username, string password)
	{
		throw new NotImplementedException();
	}
	
	public void Disconnect(int userId)
	{
		throw new NotImplementedException();
	}

	public void Logout(int userId)
	{
		throw new NotImplementedException();
	}
}