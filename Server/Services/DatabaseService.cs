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
			command.CommandText = "DROP TABLE IF EXISTS users;";
			command.ExecuteNonQuery();
		#endif
		
		/* TODO: Generate salts and fill in the length of a salt here */
		command.CommandText = $"""
		                       CREATE TABLE IF NOT EXISTS users (
		                           		username VARCHAR({SharedDefinitions.CredentialsMaxLength}), 
		                           		password_hashed VARCHAR(255), 
		                           		password_salt VARCHAR(255)
		                           	)
		                       """;
		command.ExecuteNonQuery();
	}

	public void Close()
	{
		_connection.Close();
	}
}