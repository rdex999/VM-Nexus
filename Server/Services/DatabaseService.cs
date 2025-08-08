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
			ExecuteCommand("DROP TABLE IF EXISTS users;");
		#endif

		/* TODO: Generate salts and fill in the length of a salt here */
		ExecuteCommand($"""
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

	public async Task<int> ExecuteCommandAsync(string command, params NpgsqlParameter[] parameters)
	{
		/* TODO: Add  */
		NpgsqlCommand cmd = _connection.CreateCommand();
		cmd.CommandText = command;
		cmd.Parameters.AddRange(parameters);
		return await cmd.ExecuteNonQueryAsync();
	}

	public int ExecuteCommand(string command, params NpgsqlParameter[] parameters)
	{
		NpgsqlCommand cmd = _connection.CreateCommand();
		cmd.CommandText = command;
		cmd.Parameters.AddRange(parameters);
		return cmd.ExecuteNonQuery();	
	}
}