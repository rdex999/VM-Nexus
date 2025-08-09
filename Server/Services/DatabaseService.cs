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
	
	public DatabaseService()
	{
		_connection = new NpgsqlConnection(connectionString: "Server=localhost;Port=5432;User Id=postgres;Password=postgres;Database=VM_Nexus_DB;");
		_connection.Open();

		NpgsqlCommand command = _connection.CreateCommand();
		
		#if DEBUG
			ExecuteNonQuery("DROP TABLE IF EXISTS users;");
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

	public void Close()
	{
		_connection.Close();
	}

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

	public int ExecuteNonQuery(string command, params NpgsqlParameter[] parameters)
	{
		using (NpgsqlCommand cmd = _connection.CreateCommand())
		{
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return cmd.ExecuteNonQuery();	
		}
	}

	public async Task<object?> ExecuteScalarAsync(string command, params NpgsqlParameter[] parameters)
	{
		using (NpgsqlCommand cmd = _connection.CreateCommand())
		{
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return await cmd.ExecuteScalarAsync();	
		}	
	}

	public async Task<NpgsqlDataReader> ExecuteReaderAsync(string command, params NpgsqlParameter[] parameters)
	{
		using (NpgsqlCommand cmd = _connection.CreateCommand())
		{
			cmd.CommandText = command;
			cmd.Parameters.AddRange(parameters);
			return await cmd.ExecuteReaderAsync();	
		}		
	}

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