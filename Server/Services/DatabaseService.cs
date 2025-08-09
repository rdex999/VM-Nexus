using System.Threading.Tasks;
using Npgsql;
using Shared;

namespace Server.Services;

public class DatabaseService
{
	private NpgsqlConnection _connection;

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
		                    		password_hashed VARCHAR(255), 
		                    		password_salt VARCHAR(255)
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
}