using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;
using Npgsql;
using Shared;

namespace Server.Services;

public class DatabaseService
{
	private NpgsqlConnection _connection;

	private const int EncryptedPasswordSize = 64;
	private const int SaltSize = 32;
	private const int Argon2MemorySize = 1024 * 512;	/* 512 MiB */
	private const int Argon2Iterations = 4;
	private const int Argon2Threads = 2;
	
	/// <summary>
	/// Creates the database service object and initializes a connection.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: On success, the service is connected to the database and fully initialized. <br/>
	/// On failure, an exception is raised. and the service should be considered as not initialized.
	/// </remarks>
	public DatabaseService()
	{
		_connection = new NpgsqlConnection(connectionString: "Server=localhost;Port=5432;User Id=postgres;Password=postgres;Database=VM_Nexus_DB;");
		_connection.Open();
		
		#if DEBUG
			// ExecuteNonQuery("DROP TABLE IF EXISTS users;");
		#endif

		/* TODO: Generate salts and fill in the length of a salt here */
		ExecuteNonQuery($"""
		                CREATE TABLE IF NOT EXISTS users (
		                    		username VARCHAR({SharedDefinitions.CredentialsMaxLength}), 
		                    		password_hashed BYTEA, 
		                    		password_salt BYTEA
		                    	)
		                """);
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
		_connection.Close();
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
		object? result = await ExecuteScalarAsync("SELECT COUNT(*) FROM users WHERE username = @username",  new NpgsqlParameter("@username", username));
		if (result == null)
		{
			return false;
		}
		
		long userCount = (long)result;
		return userCount > 0;
	}

	/// <summary>
	/// Creates an account with the given username and password.
	/// </summary>
	/// <param name="username">
	/// The username for the new account. username != null.
	/// </param>
	/// <param name="password">
	/// The password for the new account. password != null.
	/// </param>
	/// <returns>
	/// An exit code indicating the result of the operation.
	/// </returns>
	/// <remarks>
	/// Precondition: Service is connected to the database. username != null &amp;&amp; password != null. <br/>
	/// Postcondition: On success, the returned exit code will indicate success, and a new account is created with the given username and password. <br/>
	/// On failure, the returned exit code indicates the error, and no account is created.
	/// </remarks>
	public async Task<ExitCode> RegisterUserAsync(string username, string password)
	{
		byte[] salt = GenerateSalt();
		byte[] passwordHash = await EncryptPasswordAsync(password, salt);
		
		int rowCount = await ExecuteNonQueryAsync($"""
		                                           INSERT INTO users (username, password_hashed, password_salt)
		                                           		VALUES (@username, @password_hashed, @password_salt)
		                                           """,
			new NpgsqlParameter("@username", username), new NpgsqlParameter("@password_hashed", passwordHash), new NpgsqlParameter("@password_salt",  salt)
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
		using NpgsqlDataReader reader = await ExecuteReaderAsync(
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
		/* TODO: Add SQL injection handling and checks */
		using (NpgsqlCommand cmd = _connection.CreateCommand())
		{
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return await cmd.ExecuteNonQueryAsync();	
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
		using (NpgsqlCommand cmd = _connection.CreateCommand())
		{
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return cmd.ExecuteNonQuery();	
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
		using (NpgsqlCommand cmd = _connection.CreateCommand())
		{
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return await cmd.ExecuteScalarAsync();	
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
		NpgsqlCommand cmd = _connection.CreateCommand();
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