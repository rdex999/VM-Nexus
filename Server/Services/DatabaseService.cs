using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using Serilog;
using Server.Drives;
using Server.VirtualMachines;
using Shared;
using Shared.Drives;
using Shared.VirtualMachines;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Server.Services;

public class DatabaseService
{
	private readonly ILogger _logger;
	private const string DatabaseConnection = "Server=localhost;Port=5432;User Id=postgres;Password=postgres;Database=VM_Nexus_DB;";
	private const int EncryptedPasswordSize = 64;
	private const int SaltSize = 32;
	private const int Argon2MemorySize = 1024 * 512;	/* 512 MiB */
	private const int Argon2Iterations = 4;
	private const int Argon2Threads = 2;
	
	public DatabaseService(ILogger logger)
	{
		_logger = logger;
	}
	
	/// <summary>
	/// Initializes the database service and establishes a connection tp the database.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: On success, the service is connected to the database and fully initialized, and the returned exit code indicates success. <br/>
	/// On failure, the service is not initialized and the returned exit code indicates the failure.
	/// </remarks>
	public async Task<ExitCode> InitializeAsync()
	{
		int? rows;
		
		/* Because these tables depend upon each other, I can not create them at the same time. */
		rows = await ExecuteNonQueryAsync($"""
		                                   CREATE TABLE IF NOT EXISTS users (
		                                       id SERIAL PRIMARY KEY,
		                                       owner_id INT REFERENCES users(id) ON DELETE SET NULL,
		                                       owner_permissions INT NOT NULL DEFAULT 0,
		                                       username VARCHAR({SharedDefinitions.CredentialsMaxLength}) NOT NULL, 
		                                       email VARCHAR(254) NOT NULL,
		                                       password_hashed BYTEA NOT NULL, 
		                                       password_salt BYTEA NOT NULL,
		                                       created_at TIMESTAMP NOT NULL DEFAULT now(),
		                                       bad_login_count INT NOT NULL DEFAULT 0,
		                                       login_blocked_at TIMESTAMP DEFAULT NULL
		                                   )
		                                   """);

		if (rows == null)
			return ExitCode.DatabaseStartupFailed;

		rows = await ExecuteNonQueryAsync($"""
		                                   CREATE TABLE IF NOT EXISTS virtual_machines (
		                                       id SERIAL PRIMARY KEY,
		                                       name VARCHAR({SharedDefinitions.CredentialsMaxLength}) NOT NULL,
		                                       owner_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
		                                       operating_system INT NOT NULL,
		                                       cpu_architecture INT NOT NULL,
		                                       ram_size INT NOT NULL,
		                                       boot_mode INT NOT NULL,
		                                       state INT NOT NULL
		                                   )
		                                   """);
		
		if (rows == null)
			return ExitCode.DatabaseStartupFailed;
		
		rows = await ExecuteNonQueryAsync($"""
		                                   CREATE TABLE IF NOT EXISTS drives (
		                                       id SERIAL PRIMARY KEY,
		                                       name VARCHAR({SharedDefinitions.CredentialsMaxLength}) NOT NULL,
		                                       owner_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
		                                       size INT NOT NULL,
		                                       type INT NOT NULL
		                                   )
		                                   """);
		
		if (rows == null)
			return ExitCode.DatabaseStartupFailed;

		rows = await ExecuteNonQueryAsync($"""
		                                   CREATE TABLE IF NOT EXISTS drive_connections (
		                                       drive_id INT NOT NULL REFERENCES drives(id) ON DELETE CASCADE,
		                                       vm_id INT NOT NULL REFERENCES virtual_machines(id) ON DELETE CASCADE,
		                                       connected_at TIMESTAMP NOT NULL DEFAULT now(),
		                                       PRIMARY KEY (drive_id, vm_id)
		                                   ) 
		                                   """);
		
		if (rows == null)
			return ExitCode.DatabaseStartupFailed;

		ExitCode c = await UpdateOwnerPermissionsAsync(1, UserPermissions.VirtualMachineUse.AddIncluded());
		return ExitCode.Success;
	}

	/// <summary>
	/// Closes the database connection.
	/// </summary>
	/// <remarks>
	/// Precondition: Service connected to the database. <br/>
	/// Postcondition: The connection to the database is closed.
	/// </remarks>
	public void Close()
	{
	}

	/// <summary>
	/// Checks if there is at least one user with the given username.
	/// </summary>
	/// <param name="username">
	/// The username to check for. username != null.
	/// </param>
	/// <returns>
	/// True if there is at least one user with the given username, false otherwise.
	/// </returns>
	/// <remarks>
	/// Precondition: Service connected to the database. username != null. <br/>
	/// Postcondition: True is returned if there is at least one user with the given username,
	/// false is returned if there is no user with the given username.
	/// </remarks>
	public async Task<bool> IsUserExistAsync(string username)
	{
		object? res = await ExecuteScalarAsync($"SELECT EXISTS (SELECT 1 FROM users WHERE username = @username)",
			new NpgsqlParameter("@username", username)
		);
		
		return res != null && res is bool exists && exists;
	}

	/// <summary>
	/// Creates an account with the given username and password.
	/// </summary>
	/// <param name="ownerId">The ID of the owner user of the new user. Optional, set to null to create without an owner. owner_id == null || owner_id >= 1.</param>
	/// <param name="ownerPermissions">The owner's permissions over this new user. When creating without an owner, the value of this parameter has no effect.</param>
	/// <param name="username">
	/// The username for the new account. username != null.
	/// </param>
	/// <param name="email">
	/// The email for the new user. email != null, email must be valid.
	/// </param>
	/// <param name="password">
	/// The password for the new account. password != null.
	/// </param>
	/// <returns>
	/// An exit code indicating the result of the operation.
	/// </returns>
	/// <remarks>
	/// Precondition: Service is connected to the database, no user with the given username exists, the given email must be valid.
	/// (owner_id == null || owner_id >= 1) &amp;&amp; username != null &amp;&amp; email != null &amp;&amp; password != null. <br/>
	/// Postcondition: On success, the returned exit code will indicate success, and a new account is created with the given username and password. <br/>
	/// On failure, the returned exit code indicates the error, and no account is created.
	/// </remarks>
	public async Task<ExitCode> RegisterUserAsync(int? ownerId, UserPermissions ownerPermissions, string username, string email, string password)
	{
		if (await IsUserExistAsync(username))
			return ExitCode.UserAlreadyExists;
		
		byte[] salt = GenerateSalt();
		byte[] passwordHash = await EncryptPasswordAsync(password, salt);
		
		int? rowCount = await ExecuteNonQueryAsync($"""
		                                           INSERT INTO users (owner_id, owner_permissions, username, email, password_hashed, password_salt)
		                                           		VALUES (@owner_id, @owner_permissions, @username, @email, @password_hashed, @password_salt)
		                                           """,
			
			new NpgsqlParameter("@owner_id", ownerId.HasValue ? ownerId : DBNull.Value), 
			new NpgsqlParameter("@owner_permissions", ownerId.HasValue ? (int)ownerPermissions : 0), 
			new NpgsqlParameter("@username", username), 
			new NpgsqlParameter("@email", email),
			new NpgsqlParameter("@password_hashed", passwordHash), 
			new NpgsqlParameter("@password_salt",  salt)
		);

		if (rowCount == 1) 
			return ExitCode.Success;
		
		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Creates an account with the given username and password.
	/// </summary>
	/// <param name="username">
	/// The username for the new account. username != null.
	/// </param>
	/// <param name="email">
	/// The email for the new user. email != null, email must be valid.
	/// </param>
	/// <param name="password">
	/// The password for the new account. password != null.
	/// </param>
	/// <returns>
	/// An exit code indicating the result of the operation.
	/// </returns>
	/// <remarks>
	/// Precondition: Service is connected to the database, no user with the given username exists, the given email must be valid.
	/// username != null &amp;&amp; email != null &amp;&amp; password != null. <br/>
	/// Postcondition: On success, the returned exit code will indicate success, and a new account is created with the given username and password. <br/>
	/// On failure, the returned exit code indicates the error, and no account is created.
	/// </remarks>
	public async Task<ExitCode> RegisterUserAsync(string username, string email, string password) =>
		await RegisterUserAsync(null, 0, username, email, password);

	/// <summary>
	/// Delete the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to delete. userId >= 1.</param>
	/// <returns>An exit code indicating the result of operation.</returns>
	/// <remarks>
	/// Precondition: Service connected to database, a user with the given ID exists,
	/// and their virtual machines and drives were deleted. userId >= 1. <br/>
	/// Postcondition: On success, the user is deleted and the returned exit code indicates success.
	/// On failure, the user is not deleted and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DeleteUserAsync(int userId)
	{
		if (userId < 1)
			return ExitCode.InvalidParameter;
		
		int? rows = await ExecuteNonQueryAsync("DELETE FROM users WHERE id = @id",
			new NpgsqlParameter("@id", userId)
		);

		if (rows == 1)
			return ExitCode.Success;

		return ExitCode.DatabaseOperationFailed;
	}
	
	/// <summary>
	/// Checks if a login attempt with the given username and password is valid.
	/// </summary>
	/// <param name="username">
	/// The username of the user to check login validation for. username != null.
	/// </param>
	/// <param name="password">
	/// The password of the user to check login validation for. password != null.
	/// </param>
	/// <returns>
	/// True if it is a valid login, false otherwise.
	/// </returns>
	/// <remarks>
	/// Precondition: Service connected to the database. There exists a user with the given username. username != null &amp;&amp; password != null. <br/>
	/// Postcondition: True is returned if a login with the given username and password is valid, false is returned if the login is invalid.
	/// </remarks>
	public async Task<bool> IsValidLoginAsync(string username, string password)
	{
		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
			return false;

		await using NpgsqlDataReader? reader = await ExecuteReaderAsync(
				"SELECT password_hashed, password_salt FROM users WHERE username = @username",
				new NpgsqlParameter("@username", username)
		);
		
		if (reader == null || !await reader.ReadAsync())
			return false;
		
		byte[] dbPasswordHash = (byte[])reader.GetValue(0);
		byte[] passwordSalt = (byte[])reader.GetValue(1);
		
		byte[] passwordHash = await EncryptPasswordAsync(password, passwordSalt);

		return dbPasswordHash.SequenceEqual(passwordHash);
	}

	/// <summary>
	/// Checks if the given user can log in, according to its current bad login count and block time.
	/// </summary>
	/// <param name="userId">The ID of the user to check if can log in. userId >= 1.</param>
	/// <returns>
	/// null if the user can log in, TimeSpan.MaxValue on error.
	/// If the user has exceeded the bad log in count limit, returns the time left for the user to wait for him to be able to log in.
	/// </returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. <br/>
	/// Postcondition: Returns null if the user can log in, TimeSpan.MaxValue on error.
	/// If the user has exceeded the bad log in count limit, returns the time left for the user to wait for him to be able to log in.
	/// </remarks>
	public async Task<TimeSpan?> CanUserLoginAsync(int userId)
	{
		if (userId < 1)
			return TimeSpan.MaxValue;
		
		await using NpgsqlDataReader? reader = await ExecuteReaderAsync(
			"SELECT bad_login_count, login_blocked_at FROM users WHERE id = @id",
			new NpgsqlParameter("@id", userId)
		);
		
		if (reader == null || !await reader.ReadAsync())
			return TimeSpan.MaxValue;
		
		int badLoginCount = reader.GetInt32(0);
		if (badLoginCount >= SharedDefinitions.BadLoginBlockCount)
		{
			if (reader.IsDBNull(1))
				return TimeSpan.MaxValue;
			
			DateTime loginBlockedAt = reader.GetDateTime(1);
			if (DateTime.Now - loginBlockedAt < SharedDefinitions.BadLoginBlock)
				return SharedDefinitions.BadLoginBlock - (DateTime.Now - loginBlockedAt);
		}

		return null;
	}

	/// <summary>
	/// Increases the bad login count of a user. Blocks user login for some time if needed.
	/// </summary>
	/// <param name="userId">The ID of the user to increase the bad login count of. userId >= 1.</param>
	/// <returns>True if the user is now blocked from logging in, false otherwise.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists, and has attempted a bad login. userId >= 1.<br/>
	/// Postcondition: The bad login count is increased, login blocked for some time if needed.
	/// If log in is blocked, true is returned. Otherwise, false is returned.
	/// </remarks>
	public async Task<bool> UserBadLoginAsync(int userId)
	{
		if (userId < 1)
			return false;

		object? res = await ExecuteScalarAsync("SELECT bad_login_count FROM users WHERE id = @id", 
			new NpgsqlParameter("@id", userId)
		);

		if (res is not int badLoginCount)
			return false;
	
		DateTime? loginBlockedAt = null;
		if (++badLoginCount >= SharedDefinitions.BadLoginBlockCount)
			loginBlockedAt = DateTime.Now;
		
		await ExecuteNonQueryAsync(
			"UPDATE users SET bad_login_count = @bad_login_count, login_blocked_at = @login_blocked_at WHERE id = @id",
			new NpgsqlParameter("@id", userId),
			new NpgsqlParameter("@bad_login_count", badLoginCount),
			new NpgsqlParameter("@login_blocked_at", (object?)loginBlockedAt ?? DBNull.Value)
		);

		return loginBlockedAt.HasValue;
	}

	/// <summary>
	/// Resets the bad login count and removes login time block for a given user.
	/// </summary>
	/// <param name="userId">The ID of the user to reset the login count and time block. userId >= 1.</param>
	/// <remarks>
	/// Precondition: A user with the given ID exists, and has successfully logged in. <br/>
	/// Postcondition: The user's bad login count and login time block are reset.
	/// </remarks>
	public async Task UserGoodLoginAsync(int userId)
	{
		if (userId < 1)
			return;

		await ExecuteNonQueryAsync("UPDATE users SET bad_login_count = 0, login_blocked_at = null WHERE id = @id",
			new NpgsqlParameter("@id", userId)
		);
	}

	/// <summary>
	/// Update the password of a user.
	/// </summary>
	/// <param name="userId">The ID of the user of which to change the password. userId >= 1.</param>
	/// <param name="password">The new password to set to the user. password != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. userId >= 1 &amp;&amp; password != null. <br/>
	/// Postcondition: On success, the user's password is updated and the returned exit code indicates success.
	/// On failure, the password is not updated and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> ResetUserPasswordAsync(int userId, string password)
	{
		if (userId < 1)
			return ExitCode.InvalidParameter;

		byte[] passwordSalt = GenerateSalt();
		byte[] passwordHash = await EncryptPasswordAsync(password, passwordSalt);

		int? rows = await ExecuteNonQueryAsync(
			"UPDATE users SET password_hashed = @password_hashed, password_salt = @password_salt WHERE id = @id",
			new NpgsqlParameter("@password_hashed", passwordHash),
			new NpgsqlParameter("@password_salt", passwordSalt),
			new NpgsqlParameter("@id", userId)
		);

		if (rows == 1)
			return ExitCode.Success;
		
		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Get the ID of a user with the given username.
	/// </summary>
	/// <param name="username">The username of the user. username != null.</param>
	/// <returns>The ID of the user, -1 on failure or if there is no user with the given username.</returns>
	/// <remarks>
	/// Precondition: Service connected to database. There should be a user with the given username. username != null. <br/>
	/// Postcondition: On success, the ID of the user is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<int> GetUserIdAsync(string username)
	{
		object? res = await ExecuteScalarAsync("SELECT id FROM users WHERE username = @username", new NpgsqlParameter("@username", username));
		
		if (res is int id)
			return id;

		return -1;
	}

	/// <summary>
	/// Get a user by its username.
	/// </summary>
	/// <param name="username">The username of the user to get. username != null.</param>
	/// <returns>The found user, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service connected to database. A user with the given username exists. username != null. <br/>
	/// Postcondition: On success, the found user is returned. On failure, (database failure, or if there is no such user) null is returned.
	/// </remarks>
	public async Task<User?> GetUserAsync(string username)
	{
		await using NpgsqlDataReader? reader = await ExecuteReaderAsync(
			"SELECT id, owner_id, owner_permissions, username, email, created_at FROM users WHERE username = @username",
			new NpgsqlParameter("@username", username)
		);

		if (reader == null || !await reader.ReadAsync())
			return null;

		return await GetUserByReaderAsync(reader);
	}

	/// <summary>
	/// Get a user by its ID.
	/// </summary>
	/// <param name="userId">The ID of the user to get. userId >= 1.</param>
	/// <returns>The found user, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service connected to database. A user with the given ID exists. userId >= null. <br/>
	/// Postcondition: On success, the found user is returned. On failure, (database failure, or if there is no such user) null is returned.
	/// </remarks>
	public async Task<User?> GetUserAsync(int userId)
	{
		if (userId < 1)
			return null;

		await using NpgsqlDataReader? reader = await ExecuteReaderAsync(
			"SELECT id, owner_id, owner_permissions, username, email, created_at FROM users WHERE id = @id",
			new NpgsqlParameter("@id", userId)
		);

		if (reader == null || !await reader.ReadAsync())
			return null;
		
		return await GetUserByReaderAsync(reader);
	}

	/// <summary>
	/// Get a user by an SQL data reader, which reads id, owner_id, owner_permissions, username, email, created_at.
	/// </summary>
	/// <param name="reader">The SQL data reader which is used to read the found use row. reader != null.</param>
	/// <returns>The user, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service connected to database. The given reader is valid. reader != null. <br/>
	/// Postcondition: On success, the found user is returned. On failure, (database failure, or if there is no such user) null is returned.
	/// </remarks>
	private async Task<User?> GetUserByReaderAsync(NpgsqlDataReader reader)
	{
		if (reader.IsDBNull(1))
		{
			return new User(
				reader.GetInt32(0),
				reader.GetString(3),
				reader.GetString(4),
				reader.GetDateTime(5)
			);
		}

		int ownerId = reader.GetInt32(1);
		await using NpgsqlDataReader? r = await ExecuteReaderAsync(
			"SELECT username, email FROM users WHERE id = @owner_id",
			new NpgsqlParameter("@owner_id", ownerId)
		);

		if (r == null || !await r.ReadAsync())
			return null;

		return new SubUser(
			reader.GetInt32(0),
			reader.GetInt32(1),
			(UserPermissions)reader.GetInt32(2),
			r.GetString(0),
			r.GetString(1),
			reader.GetString(3),
			reader.GetString(4),
			reader.GetDateTime(5)
		);
	}

	/// <summary>
	/// Get all sub-users of the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to get the sub-users of. userId >= 1.</param>
	/// <returns>An array of users, describing all sub-users of the given user. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. userId >= 1. <br/>
	/// Postcondition: An array of users is returned, describing all sub-users of the given user. Returns null on failure.
	/// </remarks>
	public async Task<SubUser[]?> GetSubUsersAsync(int userId)
	{
		if (userId < 1)
			return null;

		Task<NpgsqlDataReader?> rTask = ExecuteReaderAsync(
			"SELECT username, email FROM users WHERE id = @userId",
			new NpgsqlParameter("@userId", userId)
		);
		Task<NpgsqlDataReader?> readerTask = ExecuteReaderAsync(
			"SELECT id, owner_permissions, username, email, created_at FROM users WHERE owner_id = @owner_id",
			new NpgsqlParameter("@owner_id", userId)
		);

		await Task.WhenAll(rTask, readerTask);

		await using NpgsqlDataReader? r = rTask.Result;
		await using NpgsqlDataReader? reader = readerTask.Result;
		if (r == null || reader == null || !await r.ReadAsync())
			return null;
		
		string username = r.GetString(0);
		string email = r.GetString(1);
		
		List<SubUser> users = new List<SubUser>();
		while(await reader.ReadAsync())
		{
			SubUser user = new SubUser(
				reader.GetInt32(0),
				userId,
				(UserPermissions)reader.GetInt32(1),
				username,
				email,
				reader.GetString(2),
				reader.GetString(3),
				reader.GetDateTime(4)
			);
			
			users.Add(user);
		}
		
		return users.ToArray();
	}

	/// <summary>
	/// Get the IDs of all sub-users of the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to get the sub-users of. userId >= 1.</param>
	/// <returns>An array of IDs, specifying the IDs of all sub-users of the given user. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. userId >= 1. <br/>
	/// Postcondition: On success, an array of IDs is returned, specifying the IDs of all sub-users of the given user.
	/// On failure, null is returned.
	/// </remarks>
	public async Task<int[]?> GetSubUserIdsAsync(int userId)
	{
		if (userId < 1)
			return null;
		
		await using NpgsqlDataReader? reader = await ExecuteReaderAsync(
			"SELECT id FROM users WHERE owner_id = @owner_id",
			new NpgsqlParameter("@owner_id", userId)
		);

		if (reader == null)
			return null;
		
		List<int> ids = new List<int>();
		while (await reader.ReadAsync())
			ids.Add(reader.GetInt32(0));
		
		return ids.ToArray();
	}

	/// <summary>
	/// Get the ID of the owner of the given user.
	/// </summary>
	/// <param name="userId">The user ID to get the owner of. userId >= 1.</param>
	/// <returns>The owner's ID, or -1 on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists and is a sub-user. userId >= 1. <br/>
	/// Postcondition: On success, the owner's ID is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<int> GetOwnerUserIdAsync(int userId)
	{
		if (userId < 1)
			return -1;
		
		object? res = await ExecuteScalarAsync("SELECT owner_id FROM users WHERE id = @user_id",
			new NpgsqlParameter("@user_id", userId)
		);

		if (res is int id)
			return id;

		return -1;
	}

	/// <summary>
	/// Update the owner of the given user. (Change owner_id field)
	/// </summary>
	/// <param name="userId">The user to update the owner of. userId >= 1.</param>
	/// <param name="newOwnerId">The ID of the new owner of the given user. Set to null to orphan user. newOwnerId >= 1 || newOwnerId == null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. userId >= 1 &amp;&amp; (newOwnerId >= 1 || newOwnerId == null). <br/>
	/// Postcondition: On success, the owner of the given user is updated to the given user, and the returned exit code indicates success. <br/>
	/// On failure, the owner is not updated and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> UpdateUserOwnerAsync(int userId, int? newOwnerId)
	{
		if (userId < 1 || newOwnerId < 1)
			return ExitCode.InvalidParameter;

		int? rows = await ExecuteNonQueryAsync("UPDATE users SET owner_id = @new_owner_id WHERE id = @user_id",
			new NpgsqlParameter("@user_id", userId),
			new NpgsqlParameter("@new_owner_id", newOwnerId)
		);
		
		if (rows == 1)
			return ExitCode.Success;

		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Update permissions the owner has over the given user.
	/// </summary>
	/// <param name="userId">The user to update the owner's permissions of. userId >= 1.</param>
	/// <param name="permissions">The new permissions the owner has over the given user.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists, and is a sub-user. userId >= 1. <br/>
	/// Postcondition: On success, the owner's permissions over the given user are updated,
	/// and the returned exit code indicates success. <br/>
	/// On failure, the owner's permissions are not updated and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> UpdateOwnerPermissionsAsync(int userId, UserPermissions permissions)
	{
		if (userId < 1)
			return ExitCode.InvalidParameter;

		int? rows = await ExecuteNonQueryAsync("UPDATE users SET owner_permissions = @permissions WHERE id = @user_id",
			new NpgsqlParameter("@user_id", userId),
			new NpgsqlParameter("@permissions", (int)permissions)
		);

		if (rows == 1)
			return ExitCode.Success;
		
		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Searches for users using the given query.
	/// </summary>
	/// <param name="query">The query to search for users with. query != null.</param>
	/// <returns>An array of users describing the found users, or null on failure.</returns>
	/// <remarks>
	/// Precondition: query != null. <br/>
	/// Postcondition: An array of users describing the found users is returned. Returns or null on failure.
	/// </remarks>
	public async Task<User[]?> SearchUsersAsync(string query)
	{
		string q = query.Trim();
		NpgsqlDataReader? reader;
		if (int.TryParse(q, out int id))
		{
			reader = await ExecuteReaderAsync($@"SELECT id, owner_id, owner_permissions, username, email, created_at 
															FROM users WHERE id = @id 
															OR STRPOS(LOWER(username), LOWER(@query)) > 0 
															OR STRPOS(LOWER(email), LOWER(@query)) > 0",
				new NpgsqlParameter("@id", id),
				new NpgsqlParameter("@query", q)
			);
		}
		else
		{
			reader = await ExecuteReaderAsync($@"SELECT id, owner_id, owner_permissions, username, email, created_at 
															FROM users WHERE STRPOS(LOWER(username), LOWER(@query)) > 0
															OR STRPOS(LOWER(email), LOWER(@query)) > 0",
				new NpgsqlParameter("@query", q)
			);
		}

		if (reader == null)
			return null;

		List<User> users = new List<User>();
		while (await reader.ReadAsync())
		{
			User? user = await GetUserByReaderAsync(reader);
			if (user != null)
				users.Add(user);
		}

		await reader.DisposeAsync();
		
		return users.ToArray();
	}

	/// <summary>
	/// Creates a virtual machine in the database.
	/// </summary>
	/// <param name="userId">The ID of the owner user of the virtual machine. userId >= 1.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <param name="operatingSystem">The operating system of the virtual machine.</param>
	/// <param name="cpuArchitecture">The CPU architecture (x86, x86-64, etc..) of the virtual machine.</param>
	/// <param name="ramSizeMiB">The amount of RAM storage for the virtual machine. Must be in valid range.
	/// ramSizeMiB > 0 &amp;&amp; ramSizeMiB &lt;= SharedDefinitions.VmRamSizeMbMax.</param>
	/// <param name="bootMode">The boot mode for the virtual machine. (UEFI or BIOS)</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service connected to database, a user with the given username must exist,
	/// there should not be a virtual machine with the given name under this user. (name is unique).
	/// username != null &amp;&amp; name != null. ramSizeMiB > 0 &amp;&amp; ramSizeMiB &lt;= SharedDefinitions.VmRamSizeMbMax. <br/>
	/// Postcondition: On success, a virtual machine with the given parameters is created. On failure, the returned exit code will indicate the error.
	/// </remarks>
	public async Task<ExitCode> CreateVmAsync(int userId, string name, OperatingSystem operatingSystem, 
		CpuArchitecture cpuArchitecture, int ramSizeMiB, BootMode bootMode)
	{
		if (userId < 1 || ramSizeMiB <= 1 || ramSizeMiB > SharedDefinitions.VmRamSizeMbMax) 
			return ExitCode.InvalidParameter;
		
		bool vmExists = await IsVmExistsAsync(userId, name);
		
		if (vmExists) 
			return ExitCode.VmAlreadyExists;

		int state = (int)VmState.ShutDown;
		int? rows = await ExecuteNonQueryAsync($"""
		                                      INSERT INTO virtual_machines (name, owner_id, operating_system, cpu_architecture, ram_size, boot_mode, state) 
		                                      	VALUES (@name, @owner_id, @operating_system, @cpu_architecture,  @ram_size, @boot_mode, @state)
		                                      """,
			new NpgsqlParameter("@name", name),
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@operating_system", (int)operatingSystem) { NpgsqlDbType = NpgsqlDbType.Integer },
			new NpgsqlParameter("@cpu_architecture", (int)cpuArchitecture) { NpgsqlDbType = NpgsqlDbType.Integer },
			new NpgsqlParameter("@ram_size", ramSizeMiB),
			new NpgsqlParameter("@boot_mode", (int)bootMode) { NpgsqlDbType = NpgsqlDbType.Integer },
			new NpgsqlParameter("@state", state) { NpgsqlDbType = NpgsqlDbType.Integer }
		);
		
		if (rows == 1) 
			return ExitCode.Success;
		
		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Deletes the given virtual machine from the database. (Does not stop the VM if running, does not delete disk images.)
	/// </summary>
	/// <param name="id">The ID of the virtual machine to delete. id >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service initialized and connected to the database, A virtual machine with the given ID exists. id >= 1. <br/>
	/// Postcondition: On success, the virtual machine is deleted from the database, and the returned exit code indicates success. <br/>
	/// On failure, the virtual machine is not deleted from the database and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DeleteVmAsync(int id)
	{
		if (id < 1) 
			return ExitCode.InvalidParameter;

		int? rows = await ExecuteNonQueryAsync("DELETE FROM virtual_machines WHERE id = @id", new NpgsqlParameter("@id", id));

		if (rows == 1) 
			return ExitCode.Success;
		
		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Checks if a virtual machine with the given name exists under a user with the given username.
	/// </summary>
	/// <param name="userId">The ID of the user to search for the VM under. userId >= 1.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <returns>True if the virtual machine exists, false otherwise.</returns>
	/// <remarks>
	/// Precondition: Service connected to database, a user with the given username must exist. userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: Returns true if the virtual machine exists, false if the virtual machine does not exist or on failure.
	/// </remarks>
	public async Task<bool> IsVmExistsAsync(int userId, string name)
	{
		if (userId < 1) 
			return false;
		
		object? res = await ExecuteScalarAsync($"SELECT EXISTS (SELECT 1 FROM virtual_machines WHERE owner_id = @owner_id AND name = @name)",
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);
		
		return res != null && res is bool exists && exists;
	}

	/// <summary>
	/// Checks if a virtual machine with the given ID exists.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine to check for.</param>
	/// <returns>True if the virtual machine exists, false otherwise or on failure.</returns>
	/// <remarks>
	/// Precondition: vmId >= 1. <br/>
	/// Postcondition: On success, returns whether a virtual machine with the given ID exists. On failure, false is returned.
	/// </remarks>
	public async Task<bool> IsVmExistsAsync(int vmId)
	{
		if (vmId < 1) 
			return false;
		
		object? res = await ExecuteScalarAsync($"SELECT EXISTS (SELECT 1 FROM virtual_machines WHERE id = @id)",
			new NpgsqlParameter("@id", vmId)
		);
		
		return res != null && res is bool exists && exists;
	}

	/// <summary>
	/// Get the ID of a virtual machine registered under the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to search for the VM under. userId >= 1.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <returns>On success, the ID of the VM is returned. On failure, -1 is returned.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. The user has a VM called by the given name. userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: On success, the ID of the virtual machine is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<int> GetVmIdAsync(int userId, string name)
	{
		if (userId < 1) 
			return -1;
		
		object? res = await ExecuteScalarAsync("SELECT id FROM virtual_machines WHERE owner_id = @owner_id AND name = @name", 
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);
		
		if (res is int id)
			return id;

		return -1;
	}

	/// <summary>
	/// Get the ID of the user that owns the virtual machine.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine to get the owner of. vmId >= 1.</param>
	/// <returns>The ID of the owner user, or -1 on failure.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists. vmId >= 1. <br/>
	/// Postcondition: On success, the ID of the owner user of the given virtual machine is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<int> GetVmOwnerIdAsync(int vmId)
	{
		if (vmId < 1) 
			return -1;

		object? res = await ExecuteScalarAsync("SELECT owner_id FROM virtual_machines WHERE id = @id",
			new NpgsqlParameter("@id", vmId)
		);
		
		if (res is int id)
			return id;

		return -1;
	}

	/// <summary>
	/// Get the ID's of all users that are related to the given virtual machine.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine to get the related users of. vmId >= 1.</param>
	/// <returns>The ID's of the related users, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists. vmId >= 1. <br/>
	/// Postcondition: On success, a user ID array of related users to the VM is returned. On failure, null is returned.
	/// </remarks>
	public async Task<int[]?> GetUserIdsRelatedToVmAsync(int vmId)
	{
		if (vmId < 1) 
			return null;

		/* For now the owner is the only related user */
		int id = await GetVmOwnerIdAsync(vmId);
		
		if (id < 1) 
			return null;
		
		return [id];
	}

	/// <summary>
	/// Get an array of general virtual machine descriptors of all virtual machines of the user.
	/// </summary>
	/// <param name="userId">The ID of the user to get the VMs of. userId >= 1.</param>
	/// <returns>
	/// An array of general VM descriptors, describing the VMs of the user.
	/// </returns>
	/// <remarks>
	/// Precondition: userId >= 1. <br/>
	/// Postcondition: On success, an array of general VM descriptors is returned. (might be empty, but not null) <br/>
	/// On failure, null is returned.
	/// </remarks>
	public async Task<VmGeneralDescriptor[]?> GetVmGeneralDescriptorsOfUserAsync(int userId)
	{
		if (userId < 1) 
			return null;
		
		await using NpgsqlDataReader? reader = await ExecuteReaderAsync(
			"SELECT id, name, operating_system, cpu_architecture, state, ram_size, boot_mode FROM virtual_machines WHERE owner_id = @owner_id",
			new NpgsqlParameter("@owner_id", userId)
		);

		if (reader == null)
			return null;

		List<VmGeneralDescriptor> descriptors = new List<VmGeneralDescriptor>();
		while(await reader.ReadAsync())
		{
			VmGeneralDescriptor descriptor = new VmGeneralDescriptor(
				reader.GetInt32(0),
				reader.GetString(1),
				(OperatingSystem)reader.GetInt32(2),
				(CpuArchitecture)reader.GetInt32(3),
				(VmState)reader.GetInt32(4),
				reader.GetInt32(5),
				(BootMode)reader.GetInt32(6)
			);
			
			descriptors.Add(descriptor);
		}
		return descriptors.ToArray();
	}

	/// <summary>
	/// Get the ID's of all virtual machines of a user.
	/// </summary>
	/// <param name="userId">The user of which to get the virtual machines of. userId >= 1.</param>
	/// <returns>An array of virtual machine ID's, or null on failure.</returns>
	/// <remarks>
	/// Precondition: userId >= 1. <br/>
	/// Postcondition: On success, an array of virtual machine ID's is returned. On failure, null is returned.
	/// </remarks>
	public async Task<int[]?> GetVmIdsOfUserAsync(int userId)
	{
		if (userId < 1) 
			return null;
		
		await using NpgsqlDataReader? reader = await ExecuteReaderAsync(
			"SELECT id FROM virtual_machines WHERE owner_id = @owner_id",
			new NpgsqlParameter("@owner_id", userId)
		);
		
		if (reader == null)
			return null;

		List<int> ids = new List<int>();
		while (await reader.ReadAsync())
		{
			ids.Add(reader.GetInt32(0));
		}
		
		return ids.ToArray();
	}

	/// <summary>
	/// Get a general descriptor of the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to get the general descriptor of. id >= 1.</param>
	/// <returns>The general descriptor of the virtual machine, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists. id >= 1. <br/>
	/// Postcondition: On success, a general descriptor of the virtual machine is returned. On failure, null is returned.
	/// </remarks>
	public async Task<VmGeneralDescriptor?> GetVmGeneralDescriptorAsync(int id)
	{
		if (id < 1) 
			return null;
		
		await using NpgsqlDataReader? reader = await ExecuteReaderAsync("SELECT name, operating_system, cpu_architecture, state, ram_size, boot_mode FROM virtual_machines WHERE id = @id",
			new NpgsqlParameter("@id", id)
		);

		if (reader == null)
			return null;
		
		VmGeneralDescriptor? descriptor = null;

		if (await reader.ReadAsync())
		{
			descriptor = new VmGeneralDescriptor(
				id,
				reader.GetString(0),
				(OperatingSystem)reader.GetInt32(1),
				(CpuArchitecture)reader.GetInt32(2),
				(VmState)reader.GetInt32(3),
				reader.GetInt32(4),
				(BootMode)reader.GetInt32(5)
			);
		}

		return descriptor;
	}

	/// <summary>
	/// Get the state of a virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine. id >= 1.</param>
	/// <returns>The state of the virtual machine, or -1 on failure.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID. id >= 1.<br/>
	/// Postcondition: On success, the state of the virtual machine is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<VmState> GetVmStateAsync(int id)
	{
		if (id < 1)
			return (VmState)(-1);

		object? res = await ExecuteScalarAsync(
			"SELECT state FROM virtual_machines WHERE id = @id",
			new NpgsqlParameter("@id", id)
		);

		if (res is int state)
			return (VmState)state;

		return (VmState)(-1);
	}

	/// <summary>
	/// Updates the state of the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to update the state of. id >= 1.</param>
	/// <param name="state">The new state to set the virtual machine to.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID, and the given state is different than the old one. id >= 1. <br/>
	/// Postcondition: On success, the state is updated and the returned exit code indicates success. <br/>
	/// On failure, the state is not updated and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> SetVmStateAsync(int id, VmState state)
	{
		if (id < 1)
			return ExitCode.InvalidParameter;

		int? rows = await ExecuteNonQueryAsync("UPDATE virtual_machines SET state = @state WHERE id = @id",
			new NpgsqlParameter("@state", (int)state) { NpgsqlDbType = NpgsqlDbType.Integer },
			new NpgsqlParameter("@id", id)
		);
		
		if(rows == 1)
			return ExitCode.Success;

		return ExitCode.VmDoesntExist;
	}

	/// <summary>
	/// Get a descriptor of a virtual machines.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine to get the descriptor of. vmId >= 1.</param>
	/// <returns>The descriptor of the virtual machine, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists. vmId >= 1. <br/>
	/// Postcondition: On success, the descriptor of the virtual machine is returned. On failure, null is returned.
	/// </remarks>
	public async Task<VirtualMachineDescriptor?> GetVmDescriptorAsync(int vmId)
	{
		if (vmId < 1)
			return null;
		
		await using NpgsqlDataReader? reader = await ExecuteReaderAsync(
			"SELECT name, operating_system, cpu_architecture, ram_size, boot_mode, state FROM virtual_machines WHERE id = @id LIMIT 1",
			new NpgsqlParameter("@id", vmId)
		);

		if (reader == null || !await reader.ReadAsync())
			return null;
		
		return new VirtualMachineDescriptor(
			vmId,
			reader.GetString(0),
			(OperatingSystem)reader.GetInt32(1), 
			(CpuArchitecture)reader.GetInt32(2),
			reader.GetInt32(3),
			(BootMode)reader.GetInt32(4),
			(VmState)reader.GetInt32(5)
		);
	}
	
	/// <summary>
	/// Searches for virtual machines using the given query.
	/// </summary>
	/// <param name="query">The query to search for virtual machines with. query != null.</param>
	/// <returns>An array of general virtual machine descriptors is returned, describing the found virtual machines. Returns or null on failure.</returns>
	/// <remarks>
	/// Precondition: query != null. <br/>
	/// Postcondition: An array of users describing the found users is returned. Returns or null on failure.
	/// </remarks>
	public async Task<SearchedVirtualMachine[]?> SearchVirtualMachinesAsync(string query)
	{
		string q = query.Trim();
		NpgsqlDataReader? reader;
		if (int.TryParse(q, out int id))
		{
			reader = await ExecuteReaderAsync($@"SELECT vms.id, vms.owner_id, usrs.username, vms.name, vms.operating_system, 
       															vms.cpu_architecture, vms.ram_size, vms.boot_mode, vms.state
															FROM virtual_machines vms 
															JOIN users usrs ON usrs.id = vms.owner_id 
															WHERE vms.id = @id OR vms.owner_id = @id 
															OR STRPOS(LOWER(vms.name), LOWER(@query)) > 0 
															OR STRPOS(LOWER(usrs.username), LOWER(@query)) > 0",
				new NpgsqlParameter("@id", id),
				new NpgsqlParameter("@query", q)
			);
		}
		else
		{
			reader = await ExecuteReaderAsync($@"SELECT vms.id, vms.owner_id, usrs.username, vms.name, vms.operating_system, 
       															vms.cpu_architecture, vms.ram_size, vms.boot_mode, vms.state
															FROM virtual_machines vms 
															JOIN users usrs ON usrs.id = vms.owner_id 
															WHERE STRPOS(LOWER(vms.name), LOWER(@query)) > 0 
															OR STRPOS(LOWER(usrs.username), LOWER(@query)) > 0",
				new NpgsqlParameter("@query", q)
			);
		}

		if (reader == null)
			return null;

		List<SearchedVirtualMachine> descriptors = new List<SearchedVirtualMachine>();
		while (await reader.ReadAsync())
		{
			descriptors.Add(new SearchedVirtualMachine(
				reader.GetInt32(0),
				reader.GetInt32(1),
				reader.GetString(2),
				reader.GetString(3),
				(OperatingSystem)reader.GetInt32(4),
				(CpuArchitecture)reader.GetInt32(5),
				reader.GetInt32(6),
				(BootMode)reader.GetInt32(7),
				(VmState)reader.GetInt32(8)
			));
		}

		await reader.DisposeAsync();
		
		return descriptors.ToArray();
	}

	/// <summary>
	/// Registers a drive in the database.
	/// </summary>
	/// <param name="userId">The username of the user that will own the drive. userId >= 1.</param>
	/// <param name="name">The name of the drive. Must be unique per user. name != null.</param>
	/// <param name="size">The size of the drive, in MiB. size >= 1.</param>
	/// <param name="driveType">The type of drive. (NVMe, SSD, etc)</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user exists with the given username. The user does not have a drive named by the given name. <br/>
	/// userId >= 1 &amp;&amp; name != null &amp;&amp; size >= 1. <br/>
	/// Postcondition: On success, the drive is registered in the database, and the returned exit code indicates success. <br/>
	/// On failure, the drive is not registered in the database, and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> CreateDriveAsync(int userId, string name, int size, DriveType driveType)
	{
		if (size < 1 || userId < 1) 
			return ExitCode.InvalidParameter;
		
		if (await IsDriveExistsAsync(userId, name))
			return ExitCode.DriveAlreadyExists;
		
		int? rowCount = await ExecuteNonQueryAsync($"""
		                                           INSERT INTO drives (name, owner_id, size, type)
		                                           		VALUES (@name, @owner_id, @size, @type)
		                                           """,
			new NpgsqlParameter("@name", name),
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@size", size),
			new NpgsqlParameter("@type", (int)driveType) { NpgsqlDbType = NpgsqlDbType.Integer }
		);

		if (rowCount == 1)
			return ExitCode.Success;
		
		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Get a drive descriptor of the drive identified by the given userId and drive name.
	/// </summary>
	/// <param name="userId">The ID of the user that the drive was created under. userId >= 1.</param>
	/// <param name="driveName">The name of the drive to search for. driveName != null.</param>
	/// <returns>A drive descriptor of the drive, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service initialized, userId >= 1 &amp;&amp; driveName != null. <br/>
	/// Postcondition: On success, a drive descriptor of the drive is returned. On failure, null is returned.
	/// </remarks>
	public async Task<DriveDescriptor?> GetDriveDescriptorAsync(int userId, string driveName)
	{
		if (userId < 1)
			return null;

		await using NpgsqlDataReader? reader = await ExecuteReaderAsync("SELECT id, size, type FROM drives WHERE name = @name AND owner_id = @owner_id LIMIT 1",
			new NpgsqlParameter("@name", driveName),
			new NpgsqlParameter("@owner_id", userId)
		);

		if (reader == null || !await reader.ReadAsync())
			return null;

		return new DriveDescriptor(
			reader.GetInt32(0),
			driveName,
			reader.GetInt32(1),
			(DriveType)reader.GetInt32(2)
		);
	}

	/// <summary>
	/// Check if the given user has a drive called by name.
	/// </summary>
	/// <param name="userId">The ID of the user to search the drive on. userId >= 1.</param>
	/// <param name="name">The name of the drive to search for. name != null.</param>
	/// <returns>True if the drive exists, false if not or on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID must exist. userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: Returns whether a drive called by exists under the given user. Returns false on failure.
	/// </remarks>
	public async Task<bool> IsDriveExistsAsync(int userId, string name)
	{
		if (userId < 1 || string.IsNullOrEmpty(name)) 
			return false;
		
		object? res = await ExecuteScalarAsync($"SELECT EXISTS (SELECT 1 FROM drives WHERE owner_id = @owner_id AND name = @name)",
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);

		return res != null && res is bool exists && exists;
	}

	/// <summary>
	/// Checks if a drive with the given ID exists.
	/// </summary>
	/// <param name="driveId">The ID of the drive to check the existence of. driveId >= 1.</param>
	/// <returns>True if a drive with the given ID exists, false otherwise or on failure.</returns>
	/// <remarks>
	/// Precondition: driveId >= 1. <br/>
	/// Postcondition: On success, returns whether a drive with the given ID exists. On failure, returns false.
	/// </remarks>
	public async Task<bool> IsDriveExistsAsync(int driveId)
	{
		if (driveId < 1) 
			return false;
		
		object? res = await ExecuteScalarAsync($"SELECT EXISTS (SELECT 1 FROM drives WHERE id = @id)",
			new NpgsqlParameter("@id", driveId)
		);
		
		return res != null && res is bool exists && exists;
	}
	
	
	/// <summary>
	/// Get the ID of a drive called name, owned by the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to search the drive under. userId >= 1.</param>
	/// <param name="name">The name of the drive to search for. name != null.</param>
	/// <returns>The ID of the drive on success, -1 on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. The given user has a drive called by name. userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: On success, the ID of the drive is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<int> GetDriveIdAsync(int userId, string name)
	{
		object? res = await ExecuteScalarAsync("SELECT id FROM drives WHERE owner_id = @owner_id AND name = @name", 
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);
		
		if (res is int id)
			return id;

		return -1;
	}

	/// <summary>
	/// Get the ID of the owner user of the drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive of which to get the owner of. driveId >= 1.</param>
	/// <returns>The ID of the owner user, or -1 on failure.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. driveId >= 1. <br/>
	/// Postcondition: On success, the ID of the owner user of the given drive is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<int> GetDriveOwnerIdAsync(int driveId)
	{
		if (driveId < 1) 
			return -1;

		object? res = await ExecuteScalarAsync("SELECT owner_id FROM drives WHERE id = @drive_id",
			new NpgsqlParameter("@drive_id", driveId)
		);

		if (res is int id)
			return id;

		return -1;
	}

	/// <summary>
	/// Get the ID's of all users that are related to the given drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive to get the related users of. driveId >= 1.</param>
	/// <returns>The ID's of the related users, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. driveId >= 1. <br/>
	/// Postcondition: On success, a user ID array of related users to the drive is returned. On failure, null is returned.
	/// </remarks>
	public async Task<int[]?> GetUserIdsRelatedToDriveAsync(int driveId)
	{
		if (driveId < 1) return null;

		/* For now the only related user is the owner */
		int ownerId = await GetDriveOwnerIdAsync(driveId);
		if (ownerId == -1) return null;

		return [ownerId];
	}

	/// <summary>
	/// Deletes the given drive from the database. (Not the disk image from the actual filesystem)
	/// </summary>
	/// <param name="id">The ID of the drive to delete. id >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. id >= 1. <br/>
	/// Postcondition: On success, the drive is deleted from the database and the returned exit code indicates success. <br/>
	/// On failure, the drive is not deleted from the database and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DeleteDriveAsync(int id)
	{
		if (id < 1)
			return ExitCode.InvalidParameter;
		
		int? rows = await ExecuteNonQueryAsync("DELETE FROM drives WHERE id = @id", new NpgsqlParameter("@id", id));

		if (rows >= 1)
			return ExitCode.Success;
		
		return ExitCode.DatabaseOperationFailed;
	}
	
	/// <summary>
	/// Searches for drives using the given query.
	/// </summary>
	/// <param name="query">The query to search for drives with. query != null.</param>
	/// <returns>An array of drives describing the found drives, or null on failure.</returns>
	/// <remarks>
	/// Precondition: query != null. <br/>
	/// Postcondition: An array of users describing the found users is returned. Returns or null on failure.
	/// </remarks>
	public async Task<SearchedDrive[]?> SearchDrivesAsync(string query)
	{
		string q = query.Trim();
		NpgsqlDataReader? reader;
		if (int.TryParse(q, out int id))
		{
			reader = await ExecuteReaderAsync($@"SELECT d.id, d.owner_id, u.username, d.name, d.size, d.type
															FROM drives d 
															JOIN users u ON d.owner_id = u.id
															WHERE d.id = @id OR d.owner_id = @id 
															OR STRPOS(LOWER(d.name), LOWER(@query)) > 0 
															OR STRPOS(LOWER(u.username), LOWER(@query)) > 0",
				new NpgsqlParameter("@id", id),
				new NpgsqlParameter("@query", q)
			);
		}
		else
		{
			reader = await ExecuteReaderAsync($@"SELECT d.id, d.owner_id, u.username, d.name, d.size, d.type
															FROM drives d 
															JOIN users u ON d.owner_id = u.id
															WHERE STRPOS(LOWER(d.name), LOWER(@query)) > 0 
															OR STRPOS(LOWER(u.username), LOWER(@query)) > 0",
				new NpgsqlParameter("@query", q)
			);
		}

		if (reader == null)
			return null;

		List<SearchedDrive> drives = new List<SearchedDrive>();
		while (await reader.ReadAsync())
		{
			drives.Add(new SearchedDrive(
				reader.GetInt32(0),
				reader.GetInt32(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetInt32(4),
				(DriveType)reader.GetInt32(5)
			));
		}

		await reader.DisposeAsync();
		
		return drives.ToArray();
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
	public async Task<ExitCode> ConnectDriveAsync(int driveId, int vmId)
	{
		if (driveId < 1 || vmId < 1)
		{
			return ExitCode.InvalidParameter;
		}
		
		if (await IsDriveConnectionExistsAsync(driveId, vmId))
		{
			return ExitCode.DriveConnectionAlreadyExists;
		}

		int? rows = await ExecuteNonQueryAsync($"""
		                                       INSERT INTO drive_connections (drive_id, vm_id)
		                                       	VALUES (@drive_id, @vm_id)
		                                       """,
			new NpgsqlParameter("@drive_id", driveId),
			new NpgsqlParameter("@vm_id", vmId)
		);

		if (rows == 1)
			return ExitCode.Success;

		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Removed the given drive-VM connection. (Means that when the virtual machine starts, this drive will not be connected.)
	/// </summary>
	/// <param name="driveId">The ID of the drive that is connected to the virtual machine. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine that the drive is connected to. vmId >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists, and a virtual machine with the given ID exists.
	/// The given drive is connected to the given virtual machine. (There is a connection between the two) <br/>
	/// Postcondition: On success, the drive-VM connection is removed and the returned exit code indicates success. <br/>
	/// On failure, the drive-VM connection is not affected and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DisconnectDriveAsync(int driveId, int vmId)
	{
		if (driveId < 1 || vmId < 1)
			return ExitCode.InvalidParameter;

		if (!await IsDriveConnectionExistsAsync(driveId, vmId))
			return ExitCode.DriveConnectionDoesNotExist;
		
		int? rows = await ExecuteNonQueryAsync("""
		                                       DELETE FROM drive_connections 
		                                              WHERE drive_id = @drive_id
		                                              AND vm_id = @vm_id
		                                       """,
			new NpgsqlParameter("@drive_id", driveId),
			new NpgsqlParameter("@vm_id", vmId)
		);
		
		if (rows == 1)
			return ExitCode.Success;
		
		return ExitCode.DatabaseOperationFailed;
	}

	/// <summary>
	/// Checks if a drive connection between the given drive and the given VM exists.
	/// </summary>
	/// <param name="driveId">The ID of the drive to check. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine to check. vmId >= 1.</param>
	/// <returns>True if the connection exists, false otherwise or on failure.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. A VM with the given ID exists. <br/>
	/// Postcondition: On success, returned whether the drive connection exists. On failure, returns false.
	/// </remarks>
	public async Task<bool> IsDriveConnectionExistsAsync(int driveId, int vmId)
	{
		if (driveId < 1 || vmId < 1)
		{
			return false;
		}

		object? res = await ExecuteScalarAsync(
			$"SELECT EXISTS (SELECT 1 FROM drive_connections WHERE drive_id = @drive_id AND vm_id = @vm_id)",
			new NpgsqlParameter("@drive_id", driveId),
			new NpgsqlParameter("@vm_id", vmId)
		);

		return res != null && res is bool exists && exists;
	}
	
	
	/// <summary>
	/// Searches for drive connections using the given query.
	/// </summary>
	/// <param name="query">The query to search for drive connections with. query != null.</param>
	/// <returns>An array of drives describing the found drives, or null on failure.</returns>
	/// <remarks>
	/// Precondition: query != null. <br/>
	/// Postcondition: An array of drive connections is returned, describing the found drives. Returns or null on failure.
	/// </remarks>
	public async Task<SearchedDriveConnection[]?> SearchDriveConnectionsAsync(string query)
	{
		string q = query.Trim();
		NpgsqlDataReader? reader;
		if (int.TryParse(q, out int id))
		{
			reader = await ExecuteReaderAsync($@"SELECT d.owner_id, u.username, d.name, v.name, d.id, v.id, dc.connected_at
															FROM drive_connections dc 
    														JOIN drives d ON d.id = dc.drive_id
    														JOIN virtual_machines v ON v.id = dc.vm_id
    														JOIN users u ON u.id = d.owner_id
    														WHERE d.id = @id OR v.id = @id OR u.id = @id
    														OR STRPOS(LOWER(u.username), LOWER(@query)) > 0 
    														OR STRPOS(LOWER(v.name), LOWER(@query)) > 0
    														OR STRPOS(LOWER(d.name), LOWER(@query)) > 0",
				new NpgsqlParameter("@id", id),
				new NpgsqlParameter("@query", q)
			);
		}
		else
		{
			reader = await ExecuteReaderAsync($@"SELECT d.owner_id, u.username, d.name, v.name, d.id, v.id, dc.connected_at
															FROM drive_connections dc 
    														JOIN drives d ON d.id = dc.drive_id
    														JOIN virtual_machines v ON v.id = dc.vm_id
    														JOIN users u ON u.id = d.owner_id
    														WHERE STRPOS(LOWER(u.username), LOWER(@query)) > 0 
    														OR STRPOS(LOWER(v.name), LOWER(@query)) > 0
    														OR STRPOS(LOWER(d.name), LOWER(@query)) > 0",
				new NpgsqlParameter("@id", id),
				new NpgsqlParameter("@query", q)
			);
		}

		if (reader == null)
			return null;

		List<SearchedDriveConnection> drives = new List<SearchedDriveConnection>();
		while (await reader.ReadAsync())
		{
			drives.Add(new SearchedDriveConnection(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetInt32(4),
				reader.GetInt32(5),
				reader.GetDateTime(6)
			));
		}

		await reader.DisposeAsync();
		
		return drives.ToArray();
	}		

	/// <summary>
	/// Get all drives connected to the virtual machine.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that the drives are attached to. vmId >= 1.</param>
	/// <returns>An array of drive descriptors, describing each drive that is connected to the virtual machine. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists. vmId >= 1. <br/>
	/// Postcondition: On success, an array of drive descriptors is returned, describing each drive that is connected to the virtual machine.
	/// On failure, null is returned.
	/// </remarks>
	public async Task<DriveDescriptor[]?> GetVmDriveDescriptorsAsync(int vmId)
	{
		if (vmId < 1)
		{
			return null;
		}

		await using NpgsqlDataReader? reader = await ExecuteReaderAsync($"""
		                                                    SELECT d.id, d.name, d.size, d.type FROM drive_connections dc 
		                                                        JOIN drives d ON d.id = dc.drive_id 
		                                                    	WHERE dc.vm_id = @vm_id
		                                                    	ORDER BY dc.connected_at ASC
		                                                    """,
			new NpgsqlParameter("@vm_id", vmId)
		);

		if (reader == null)
			return null;
		
		List<DriveDescriptor> descriptors = new List<DriveDescriptor>();
		while (await reader.ReadAsync())
		{
			DriveDescriptor descriptor = new DriveDescriptor(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetInt32(2),
				(DriveType)reader.GetInt32(3)
			);
			
			descriptors.Add(descriptor);
		}
		
		return descriptors.ToArray();
	}

	/// <summary>
	/// Get descriptors of all drives of the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to get the drive descriptors of. userId >= 1.</param>
	/// <returns>An array of drive descriptors, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. userId >= 1. <br/>
	/// Postcondition: On success, an array of drive descriptors is returned. (can be empty of user doesnt have drives) <br/>
	/// On failure, null is returned.
	/// </remarks>
	public async Task<DriveDescriptor[]?> GetDriveDescriptorsOfUserAsync(int userId)
	{
		if (userId < 1) return null;

		await using NpgsqlDataReader? reader = await ExecuteReaderAsync($"""
		                                                    SELECT id, name, size, type FROM drives WHERE owner_id = @owner_id
		                                                    """,
			new NpgsqlParameter("@owner_id", userId)
		);

		if (reader == null)
			return null;

		List<DriveDescriptor> descriptors = new List<DriveDescriptor>();
		while (await reader.ReadAsync())
		{
			descriptors.Add(new DriveDescriptor(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetInt32(2),
				(DriveType)reader.GetInt32(3)
			));
		}
		
		return descriptors.ToArray();
	}

	/// <summary>
	/// Get the ID's of all drives of the given user.
	/// </summary>
	/// <param name="userId">The ID of the user of which to get the drives of. userId >= 1.</param>
	/// <returns>An array of drive ID's, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. userId >= 1. <br/>
	/// Postcondition: On success, an array of drive ID's is returned. On failure, null is returned.
	/// </remarks>
	public async Task<int[]?> GetDriveIdsOfUserAsync(int userId)
	{
		if (userId < 1) return null;

		await using NpgsqlDataReader? reader = await ExecuteReaderAsync($"""
		                                                                SELECT id FROM drives WHERE owner_id = @owner_id
		                                                                """,
			new NpgsqlParameter("@owner_id", userId)
		);

		if (reader == null)
			return null;

		List<int> ids = new List<int>();
		while (await reader.ReadAsync())
		{
			ids.Add(reader.GetInt32(0));
		}
		
		return ids.ToArray();
	}

	/// <summary>
	/// Get all drive connections of the given user.
	/// </summary>
	/// <param name="userId">The ID of the user of which to get the drive connections of. userId >= 1.</param>
	/// <returns>An array of drive connections, or null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID exists. userId >= 1. <br/>
	/// Postcondition: On success, an array of drive connections is returned. On failure, null is returned.
	/// </remarks>
	public async Task<DriveConnection[]?> GetDriveConnectionsOfUserAsync(int userId)
	{
		if (userId < 1) return null;

		await using NpgsqlDataReader? reader = await ExecuteReaderAsync($"""
		                                                    SELECT dc.drive_id, dc.vm_id FROM drive_connections dc
		                                                    JOIN drives d ON d.id = dc.drive_id
		                                                    JOIN virtual_machines vm ON vm.id = dc.vm_id
		                                                    WHERE d.owner_id = @owner_id AND vm.owner_id = @owner_id
		                                                    """,
			new NpgsqlParameter("@owner_id", userId)
		);
		
		if (reader == null)
			return null;

		List<DriveConnection> connections = new List<DriveConnection>();
		while (await reader.ReadAsync())
		{
			connections.Add(new DriveConnection(
				reader.GetInt32(0),
				reader.GetInt32(1)
			));
		}
		
		return connections.ToArray();
	}
	
	/// <summary>
	/// The asynchronous version of the ExecuteNonQuery command.
	/// Executes a Non-Query command (Something that doesnt search for stuff, like a DELETE or INSERT command)
	/// </summary>
	/// <param name="command">
	/// The command to execute. command != null.
	/// </param>
	/// <param name="parameters">
	/// Optional parameters for the command.
	/// </param>
	/// <returns>
	/// The number of rows affected by execution of the command.
	/// </returns>
	/// <remarks>
	/// Precondition: Service connected to the database. command != null. <br/>
	/// Postcondition: On success, the number of rows affected by the execution of the command is returned. <br/>
	/// On failure, an exception is raised.
	/// </remarks>
	private async Task<int?> ExecuteNonQueryAsync(string command, params NpgsqlParameter[] parameters)
	{
		try
		{
			await using NpgsqlConnection connection = new NpgsqlConnection(DatabaseConnection);
			await connection.OpenAsync();
			await using NpgsqlCommand cmd = connection.CreateCommand();
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return await cmd.ExecuteNonQueryAsync();
		}
		catch (Exception e)
		{
			_logger.Error($"ExecuteNonQueryAsync failed. Exception: {e}");
			return null;
		}
	}
	
	/// <summary>
	/// Executes a Non-Query command (Something that doesnt search for stuff, like a DELETE or INSERT command)
	/// </summary>
	/// <param name="command">
	/// The command to execute. command != null.
	/// </param>
	/// <param name="parameters">
	/// Optional parameters for the command.
	/// </param>
	/// <returns>
	/// The number of rows affected by execution of the command.
	/// </returns>
	/// <remarks>
	/// Precondition: Service connected to the database. command != null. <br/>
	/// Postcondition: On success, the number of rows affected by the execution of the command is returned. <br/>
	/// On failure, an exception is raised.
	/// </remarks>
	public int? ExecuteNonQuery(string command, params NpgsqlParameter[] parameters)
	{
		try
		{
			using NpgsqlConnection connection = new NpgsqlConnection(DatabaseConnection);
			connection.Open();
			using NpgsqlCommand cmd = connection.CreateCommand();
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return cmd.ExecuteNonQuery();
		}
		catch (Exception e)
		{
			_logger.Error($"ExecuteNonQuery failed. Exception: {e}");
			return null;
		}
	}

	/// <summary>
	/// Executes a scalar command asynchronously
	/// </summary>
	/// <param name="command">
	/// The command to execute. command != null.
	/// </param>
	/// <param name="parameters">
	/// Optional parameters for the command.
	/// </param>
	/// <returns>
	/// The return type of the command. (Based on which command is run, its return type will be different)
	/// </returns>
	/// <remarks>
	/// Precondition: Service connected to the database. command != null. <br/>
	/// Postcondition: Returns the returned data by the executed command. (Data varies by which command is executed)
	/// </remarks>
	private async Task<object?> ExecuteScalarAsync(string command, params NpgsqlParameter[] parameters)
	{
		try
		{
			await using NpgsqlConnection connection = new NpgsqlConnection(DatabaseConnection);
			await connection.OpenAsync();
			await using NpgsqlCommand cmd = connection.CreateCommand();
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return await cmd.ExecuteScalarAsync();
		}
		catch (Exception e)
		{
			_logger.Error($"ExecuteScalarAsync failed. Exception: {e}");
			return null;
		}
	}

	/// <summary>
	/// Executes a reader command asynchronously.
	/// </summary>
	/// <param name="command">
	/// The command to execute. command != null.
	/// </param>
	/// <param name="parameters">
	/// Optional parameters for the command.
	/// </param>
	/// <returns>
	/// A data reader for reading the returned data by the command.
	/// </returns>
	/// <remarks>
	/// Precondition: Service connected to the database. command != null. <br/>
	/// Postcondition: Returns a data reader for reading the returned data by the command.
	/// </remarks>
	private async Task<NpgsqlDataReader?> ExecuteReaderAsync(string command, params NpgsqlParameter[] parameters)
	{
		try
		{
			NpgsqlConnection connection = new NpgsqlConnection(DatabaseConnection);
			await connection.OpenAsync();
			await using NpgsqlCommand cmd = connection.CreateCommand();
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
		}
		catch (Exception e)
		{
			_logger.Error($"ExecuteReaderAsync failed. Exception: {e}");
			return null;
		}
	}

	/// <summary>
	/// Encrypts the given password using the given salt.
	/// </summary>
	/// <param name="password">
	/// The password to encrypt. password != null.
	/// </param>
	/// <param name="salt">
	/// The salt to use while encrypting the password. salt != null.
	/// </param>
	/// <returns>
	/// A byte array representing the encrypted password and salt.
	/// </returns>
	/// <remarks>
	/// Precondition: password != null &amp;&amp; salt != null. <br/>
	/// Postcondition: A byte array representing the encrypted password and salt is returned.
	/// </remarks>
	private async Task<byte[]> EncryptPasswordAsync(string password, byte[] salt)
	{
		using Argon2id argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
		argon2.Salt = salt;
		argon2.MemorySize = Argon2MemorySize;
		argon2.Iterations = Argon2Iterations;
		argon2.DegreeOfParallelism = Argon2Threads;
			
		return await argon2.GetBytesAsync(EncryptedPasswordSize);
	}

	/// <summary>
	/// Generates a unique salt which is then used for encrypting a password.
	/// </summary>
	/// <returns>
	/// A byte array representing the salt.
	/// </returns>
	/// <remarks>
	/// Precondition: No specific condition. <br/>
	/// Postcondition: A byte array representing the salt is returned.
	/// </remarks>
	private byte[] GenerateSalt()
	{
		byte[] salt = new byte[SaltSize];
		using RandomNumberGenerator rng = RandomNumberGenerator.Create();
		rng.GetBytes(salt);

		return salt;
	}
	
	public class SearchedVirtualMachine
	{
		public int Id { get; }
		public int OwnerId { get; }
		public string OwnerUsername { get; }
		public string Name { get; }
		public OperatingSystem OperatingSystem { get; }
		public CpuArchitecture CpuArchitecture { get; }
		public int RamSizeMiB { get; }
		public BootMode BootMode { get; }
		public VmState State { get; }

		public SearchedVirtualMachine(int id, int ownerId, string ownerUsername, string name, OperatingSystem operatingSystem, 
			CpuArchitecture cpuArchitecture, int ramSizeMiB, BootMode bootMode, VmState state)
		{
			Id = id;
			OwnerId = ownerId;
			OwnerUsername = ownerUsername;
			Name = name;
			OperatingSystem = operatingSystem;
			CpuArchitecture = cpuArchitecture;
			RamSizeMiB = ramSizeMiB;
			BootMode = bootMode;
			State = state;
		}
	}
	
	public class SearchedDrive
	{
		public int Id { get; }
		public int OwnerId { get; }
		public string OwnerUsername { get; }
		public string Name { get; }
		public int SizeMiB { get; }
		public DriveType DriveType { get; }

		public SearchedDrive(int id, int ownerId, string ownerUsername, string name, int sizeMiB, DriveType driveType)
		{
			Id = id;
			OwnerId = ownerId;
			OwnerUsername = ownerUsername;
			Name = name;
			SizeMiB = sizeMiB;
			DriveType = driveType;
		}
	}
	
	public class SearchedDriveConnection
	{
		public int OwnerId { get; }
		public string OwnerUsername { get; }
		public string DriveName { get; }
		public string VirtualMachineName { get; }
		public int DriveId { get; }
		public int VirtualMachineId { get; }
		public DateTime ConnectedAt { get; }
		
		public SearchedDriveConnection(int ownerId, string ownerUsername, string driveName, string virtualMachineName, 
			int driveId, int virtualMachineId, DateTime connectedAt)
		{
			OwnerId = ownerId;
			OwnerUsername = ownerUsername;
			DriveName = driveName;
			VirtualMachineName = virtualMachineName;
			DriveId = driveId;
			VirtualMachineId = virtualMachineId;
			ConnectedAt = connectedAt;
		}
	}
}