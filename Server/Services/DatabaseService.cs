using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using Server.Drives;
using Server.VirtualMachines;
using Shared;

namespace Server.Services;

public class DatabaseService
{
	private const string DatabaseConnection = "Server=localhost;Port=5432;User Id=postgres;Password=postgres;Database=VM_Nexus_DB;";
	private const int EncryptedPasswordSize = 64;
	private const int SaltSize = 32;
	private const int Argon2MemorySize = 1024 * 512;	/* 512 MiB */
	private const int Argon2Iterations = 4;
	private const int Argon2Threads = 2;
	
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
		try
		{
			/* Because these tables depend upon each other, I can not create them at the same time. */
			await ExecuteNonQueryAsync($"""
			                                      CREATE TABLE IF NOT EXISTS users (
			                                          id SERIAL PRIMARY KEY,
			                                          username VARCHAR({SharedDefinitions.CredentialsMaxLength}) NOT NULL, 
			                                          email VARCHAR(254) NOT NULL,
			                                          password_hashed BYTEA NOT NULL, 
			                                          password_salt BYTEA NOT NULL
			                                      )
			                                      """);

			await ExecuteNonQueryAsync($"""
			                                                CREATE TABLE IF NOT EXISTS virtual_machines (
			                                                    id SERIAL PRIMARY KEY,
			                                                    name VARCHAR({SharedDefinitions.CredentialsMaxLength}) NOT NULL,
			                                                    owner_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
			                                                    operating_system INT NOT NULL,
			                                                    cpu_architecture INT NOT NULL,
			                                                    boot_mode INT NOT NULL,
			                                                    state INT NOT NULL
			                                                )
			                                                """);

			await ExecuteNonQueryAsync($"""
			                                       CREATE TABLE IF NOT EXISTS drives (
			                                           id SERIAL PRIMARY KEY,
			                                           name VARCHAR({SharedDefinitions.CredentialsMaxLength}) NOT NULL,
			                                           owner_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
			                                           size INT NOT NULL,
			                                           type INT NOT NULL
			                                       )
			                                       """);

			await ExecuteNonQueryAsync($"""
			                                                 CREATE TABLE IF NOT EXISTS drive_connections (
			                                                     drive_id INT NOT NULL REFERENCES drives(id) ON DELETE CASCADE,
			                                                     vm_id INT NOT NULL REFERENCES virtual_machines(id) ON DELETE CASCADE,
			                                                     PRIMARY KEY (drive_id, vm_id)
			                                                 ) 
			                                                 """);

			return ExitCode.Success;
		}
		catch (Exception)
		{
			return ExitCode.DatabaseStartupFailed;
		}
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
		object? exists = await ExecuteScalarAsync($"SELECT EXISTS (SELECT 1 FROM users WHERE username = @username)",
			new NpgsqlParameter("@username", username)
		);
		
		return exists != null && (bool)exists;
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
	public async Task<ExitCode> RegisterUserAsync(string username, string email, string password)
	{
		byte[] salt = GenerateSalt();
		byte[] passwordHash = await EncryptPasswordAsync(password, salt);
		
		int rowCount = await ExecuteNonQueryAsync($"""
		                                           INSERT INTO users (username, email, password_hashed, password_salt)
		                                           		VALUES (@username, @email, @password_hashed, @password_salt)
		                                           """,
			
			new NpgsqlParameter("@username", username), 
			new NpgsqlParameter("@email", email),
			new NpgsqlParameter("@password_hashed", passwordHash), 
			new NpgsqlParameter("@password_salt",  salt)
		);

		if (rowCount == 1)
		{
			return ExitCode.Success;
		}
		
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
		{
			return false;
		}

		await using NpgsqlDataReader reader = await ExecuteReaderAsync(
				"SELECT password_hashed, password_salt FROM users WHERE username = @username",
				new NpgsqlParameter("@username", username)
			);
		
		if (!reader.Read() || reader.IsDBNull(0) || reader.IsDBNull(1))
		{
			return false;
		}
		
		byte[] dbPasswordHash = (byte[])reader.GetValue(0);
		byte[] passwordSalt = (byte[])reader.GetValue(1);
		
		byte[] passwordHash = await EncryptPasswordAsync(password, passwordSalt);

		return dbPasswordHash.SequenceEqual(passwordHash);
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
		object? id = await ExecuteScalarAsync("SELECT id FROM users WHERE username = @username", new NpgsqlParameter("@username", username));
		if (id == null)
		{
			return -1;
		}
		
		return (int)id;
	}

	/// <summary>
	/// Creates a virtual machine in the database.
	/// </summary>
	/// <param name="userId">The ID of the owner user of the virtual machine. userId >= 1.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <param name="operatingSystem">The operating system of the virtual machine.</param>
	/// <param name="cpuArchitecture">The CPU architecture (x86, x86-64, etc..) of the virtual machine.</param>
	/// <param name="bootMode">The boot mode for the virtual machine. (UEFI or BIOS)</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service connected to database, a user with the given username must exist,
	/// there should not be a virtual machine with the given name under this user. (name is unique).
	/// username != null &amp;&amp; name != null. <br/>
	/// Postcondition: On success, a virtual machine with the given parameters is created. On failure, the returned exit code will indicate the error.
	/// </remarks>
	public async Task<ExitCode> CreateVmAsync(int userId, string name, SharedDefinitions.OperatingSystem operatingSystem, 
		SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode)
	{
		if (userId < 1)
		{
			return ExitCode.InvalidParameter;
		}
		
		bool vmExists = await IsVmExistsAsync(userId, name);
		
		if (vmExists)
		{
			return ExitCode.VmAlreadyExists;
		}

		int state = (int)SharedDefinitions.VmState.ShutDown;
		int rows = await ExecuteNonQueryAsync($"""
		                                      INSERT INTO virtual_machines (name, owner_id, operating_system, cpu_architecture, boot_mode, state) 
		                                      	VALUES (@name, @owner_id, @operating_system, @cpu_architecture,  @boot_mode, @state)
		                                      """,
			new NpgsqlParameter("@name", name),
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@operating_system", (int)operatingSystem) { NpgsqlDbType = NpgsqlDbType.Integer },
			new NpgsqlParameter("@cpu_architecture", (int)cpuArchitecture) { NpgsqlDbType = NpgsqlDbType.Integer },
			new NpgsqlParameter("@boot_mode", (int)bootMode) { NpgsqlDbType = NpgsqlDbType.Integer },
			new NpgsqlParameter("@state", state) { NpgsqlDbType = NpgsqlDbType.Integer }
		);
		
		if (rows == 1)
		{
			return ExitCode.Success;
		}
		
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
		{
			return false;
		}
		
		object? exists = await ExecuteScalarAsync($"SELECT EXISTS (SELECT 1 FROM virtual_machines WHERE owner_id = @owner_id AND name = @name)",
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);
		
		return exists != null && (bool)exists;
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
		{
			return -1;
		}
		
		object? id = await ExecuteScalarAsync("SELECT id FROM virtual_machines WHERE owner_id = @owner_id AND name = @name", 
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);
		
		if (id == null)
		{
			return -1;
		}
		
		return (int)id;
	}

	/// <summary>
	/// Get an array of general virtual machine descriptors of all virtual machines of the user.
	/// </summary>
	/// <param name="userId">The ID of the user to get the VMs of. userId >= 1.</param>
	/// <returns>
	/// An array of general VM descriptors, describing the VMs of the user.
	/// </returns>
	/// <remarks>
	/// Precondition: Service connected to the database, a user with the given username exists. userId >= 1. <br/>
	/// Postcondition: On success, an array of general VM descriptors is returned. (might be empty, but not null) <br/>
	/// On failure, null is returned.
	/// </remarks>
	public async Task<SharedDefinitions.VmGeneralDescriptor[]?> GetVmGeneralDescriptorsAsync(int userId)
	{
		await using NpgsqlDataReader reader = await ExecuteReaderAsync(
			"SELECT id, name, operating_system, state FROM virtual_machines WHERE owner_id = @owner_id",
			new NpgsqlParameter("@owner_id", userId)
		);

		List<SharedDefinitions.VmGeneralDescriptor> descriptors = new List<SharedDefinitions.VmGeneralDescriptor>();
		while(await reader.ReadAsync())
		{
			SharedDefinitions.VmGeneralDescriptor descriptor = new SharedDefinitions.VmGeneralDescriptor(
				reader.GetInt32(0),
				reader.GetString(1),
				(SharedDefinitions.OperatingSystem)reader.GetInt32(2),
				(SharedDefinitions.VmState)reader.GetInt32(3)
			);
			
			descriptors.Add(descriptor);
		}
		return descriptors.ToArray();
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
	public async Task<SharedDefinitions.VmState> GetVmStateAsync(int id)
	{
		if (id < 1)
		{
			return (SharedDefinitions.VmState)(-1);
		}

		object? state = await ExecuteScalarAsync(
			"SELECT state FROM virtual_machines WHERE id = @id",
			new NpgsqlParameter("@id", id)
		);

		if (state == null)
		{
			return (SharedDefinitions.VmState)(-1);
		}
		
		return (SharedDefinitions.VmState)state;
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
		{
			return null;
		}
		
		NpgsqlDataReader reader = await ExecuteReaderAsync(
			"SELECT name, operating_system, cpu_architecture, boot_mode, state FROM virtual_machines WHERE id = @id LIMIT 1",
			new NpgsqlParameter("@id", vmId)
		);

		if (!await reader.ReadAsync())
		{
			return null;
		}
		
		return new VirtualMachineDescriptor(
			vmId,
			reader.GetString(0),
			(SharedDefinitions.OperatingSystem)reader.GetInt32(1), 
			(SharedDefinitions.CpuArchitecture)reader.GetInt32(2),
			(SharedDefinitions.BootMode)reader.GetInt32(3),
			(SharedDefinitions.VmState)reader.GetInt32(4)
		);
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
	public async Task<ExitCode> CreateDriveAsync(int userId, string name, int size, SharedDefinitions.DriveType driveType)
	{
		if (size < 1 || userId < 1)
		{
			return ExitCode.InvalidParameter;
		}
	
		if (await IsDriveExistsAsync(userId, name))
		{
			return ExitCode.DriveAlreadyExists;
		}
		
		int rowCount = await ExecuteNonQueryAsync($"""
		                                           INSERT INTO drives (name, owner_id, size, type)
		                                           		VALUES (@name, @owner_id, @size, @type)
		                                           """,
			new NpgsqlParameter("@name", name),
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@size", size),
			new NpgsqlParameter("@type", (int)driveType) { NpgsqlDbType = NpgsqlDbType.Integer }
		);

		if (rowCount == 1)
		{
			return ExitCode.Success;
		}
		
		return ExitCode.DatabaseOperationFailed;
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
		object? exists = await ExecuteScalarAsync($"SELECT EXISTS (SELECT 1 FROM drives WHERE owner_id = @owner_id AND name = @name)",
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);
		
		return exists != null && (bool)exists;
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
		object? id = await ExecuteScalarAsync("SELECT id FROM drives WHERE owner_id = @owner_id AND name = @name", 
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);
		
		if (id == null)
		{
			return -1;
		}
		
		return (int)id;
	}

	/// <summary>
	/// Deletes the given drive from the database. (Not the disk image from the actual filesystem)
	/// </summary>
	/// <param name="userId">The ID of the user who owns the drive. userId >= 1.</param>
	/// <param name="name">The name of the drive. name != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user whos ID is the given ID - exists. The user has a drive which name is the given name.
	/// userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: On success, the drive is deleted from the database and the returned exit code indicates success. <br/>
	/// On failure, the drive is not deleted from the database and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DeleteDriveAsync(int userId, string name)
	{
		if (userId < 1)
		{
			return ExitCode.InvalidParameter;
		}
		
		int rows = await ExecuteNonQueryAsync("DELETE FROM drives WHERE owner_id = @owner_id AND name = @name",
			new NpgsqlParameter("@owner_id", userId),
			new NpgsqlParameter("@name", name)
		);

		if (rows >= 1)
		{
			return ExitCode.Success;
		}
		
		return ExitCode.DatabaseOperationFailed;
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
			return ExitCode.DriveAlreadyExists;
		}

		int rows = await ExecuteNonQueryAsync($"""
		                                       INSERT INTO drive_connections (drive_id, vm_id)
		                                       	VALUES (@drive_id, @vm_id)
		                                       """,
			new NpgsqlParameter("@drive_id", driveId),
			new NpgsqlParameter("@vm_id", vmId)
		);

		if (rows == 1)
		{
			return ExitCode.Success;
		}

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

		object? exists = await ExecuteScalarAsync(
			$"SELECT EXISTS (SELECT 1 FROM drive_connections WHERE drive_id = @drive_id AND vm_id = @vm_id)",
			new NpgsqlParameter("@drive_id", driveId),
			new NpgsqlParameter("@vm_id", vmId)
		);
		
		return exists != null && (bool)exists;
	}

	/// <summary>
	/// Get all drives associated with a virtual machine.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that the drives are attached to. vmId >= 1.</param>
	/// <returns>An array of drive descriptors, describing each drive that is connected to the virtual machine. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: A user with the given username exists. The user has a virtual machine with the given name. vmId >= 1. <br/>
	/// Postcondition: On success, an array of drive descriptors is returned, describing each drive that is connected to the virtual machine.
	/// On failure, null is returned.
	/// </remarks>
	public async Task<DriveDescriptor[]?> GetVmDriveDescriptorsAsync(int vmId)
	{
		if (vmId < 1)
		{
			return null;
		}

		NpgsqlDataReader reader = await ExecuteReaderAsync($"""
		                                                    SELECT d.id, d.name, d.size, d.type FROM drive_connections dc 
		                                                        JOIN drives d ON d.id = dc.drive_id 
		                                                    	WHERE dc.vm_id = @vm_id
		                                                    """,
			new NpgsqlParameter("@vm_id", vmId)
		);
		
		List<DriveDescriptor> descriptors = new List<DriveDescriptor>();
		while (await reader.ReadAsync())
		{
			DriveDescriptor descriptor = new DriveDescriptor(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetInt32(2),
				(SharedDefinitions.DriveType)reader.GetInt32(3)
			);
			
			descriptors.Add(descriptor);
		}
		
		return descriptors.ToArray();
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
	public async Task<int> ExecuteNonQueryAsync(string command, params NpgsqlParameter[] parameters)
	{
		using (NpgsqlConnection connection = new NpgsqlConnection(DatabaseConnection))
		{
			await connection.OpenAsync();
			using (NpgsqlCommand cmd = connection.CreateCommand())
			{
				cmd.CommandText = command;
				cmd.Parameters.AddRange(parameters);
				return await cmd.ExecuteNonQueryAsync();
			}
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
	public int ExecuteNonQuery(string command, params NpgsqlParameter[] parameters)
	{
		using (NpgsqlConnection connection = new NpgsqlConnection(DatabaseConnection))
		{
			connection.Open();
			using (NpgsqlCommand cmd = connection.CreateCommand())
			{
				cmd.CommandText = command;
				cmd.Parameters.AddRange(parameters);
				return cmd.ExecuteNonQuery();
			}
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
	public async Task<object?> ExecuteScalarAsync(string command, params NpgsqlParameter[] parameters)
	{
		using (NpgsqlConnection connection = new NpgsqlConnection(DatabaseConnection))
		{
			await connection.OpenAsync();
			using (NpgsqlCommand cmd = connection.CreateCommand())
			{
				cmd.CommandText = command;
				cmd.Parameters.AddRange(parameters);
				return await cmd.ExecuteScalarAsync();
			}	
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
	public async Task<NpgsqlDataReader> ExecuteReaderAsync(string command, params NpgsqlParameter[] parameters)
	{
		NpgsqlConnection connection = new NpgsqlConnection(DatabaseConnection);
		await connection.OpenAsync();
		NpgsqlCommand cmd = connection.CreateCommand();
		cmd.CommandText = command;
		cmd.Parameters.AddRange(parameters);
		return await cmd.ExecuteReaderAsync();
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
		using (Argon2id argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
		{
			argon2.Salt = salt;
			argon2.MemorySize = Argon2MemorySize;
			argon2.Iterations = Argon2Iterations;
			argon2.DegreeOfParallelism = Argon2Threads;
			
			return await argon2.GetBytesAsync(EncryptedPasswordSize);
		}
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
		using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
		{
			rng.GetBytes(salt);
		}

		return salt;
	}
}