using System.Diagnostics;
using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Shared;

public static class Common
{
	/// <summary>
	/// Converts an object to a byte array.
	/// </summary>
	/// <param name="obj">
	/// The object to convert to the byte array.
	/// </param>
	/// <returns>
	/// A byte array representing the given objects' bytes.
	/// </returns>
	/// <remarks>
	/// Precondition: obj != null. <br/>
	/// Postcondition: Returns a byte array representation of the object.
	/// </remarks>
	public static byte[] ToByteArray(object obj)
	{
		return JsonSerializer.SerializeToUtf8Bytes(obj);
	}

	/// <summary>
	/// Converts a byte array to an object of type T.
	/// </summary>
	/// <param name="bytes">
	/// The byte array to convert to an object. bytes != null.
	/// </param>
	/// <typeparam name="T">
	/// The type of the object to convert into.
	/// </typeparam>
	/// <returns>
	/// On success, an object of type T is returned.
	/// On failure, null is returned.
	/// </returns>
	/// <remarks>
	/// Precondition: bytes != null. <br/>
	/// Postcondition: On success, an object of type T is returned. On failure, null is returned.
	/// </remarks>
	public static T? FromByteArray<T>(byte[] bytes)
	{
		return JsonSerializer.Deserialize<T>(bytes);
	}

	/// <summary>
	/// Converts the given object to a byte array with type information.
	/// </summary>
	/// <param name="obj">
	/// The object ot convert into the byte array. obj != null.
	/// </param>
	/// <returns>
	/// A byte array with type information representing the object.
	/// </returns>
	/// <remarks>
	/// Precondition: obj != null. <br/>
	/// Postcondition: A byte array with type information representing the object is returned.
	/// </remarks>
	public static byte[] ToByteArrayWithType(object obj)
	{
		var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
		var json = JsonConvert.SerializeObject(obj, settings);
		return System.Text.Encoding.UTF8.GetBytes(json);
	}

	/// <summary>
	/// Converts a byte array with type information into an object.
	/// </summary>
	/// <param name="bytes">
	/// The bytes array to convert into as obejct. bytes != null.
	/// </param>
	/// <returns>
	/// On success, an object representation of the given byte array is returned. <br/>
	/// On failure, null is returned.
	/// </returns>
	/// <remarks>
	/// Precondition: bytes != null. <br/>
	/// Postcondition: On success, an object representation of the given byte array is returned. <br/>
	/// On failure, null is returned.
	/// </remarks>
	public static object? FromByteArrayWithType(byte[] bytes)
	{
		var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
		var json = System.Text.Encoding.UTF8.GetString(bytes);
		try
		{
			return JsonConvert.DeserializeObject(json, settings);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// Runs the given bash command in a bash shell.
	/// </summary>
	/// <param name="command">
	/// The bash command to run. command != null.
	/// </param>
	/// <returns>
	/// The exit code of the command.
	/// </returns>
	/// <remarks>
	/// Precondition: command contains a valid bash command. command != null. <br/>
	/// Postcondition: The exit code of the command is returned.
	/// </remarks>
	public static async Task<int> RunBashCommandAsync(string command)
	{
		ProcessStartInfo psi = new ProcessStartInfo();
		psi.FileName = "/bin/bash";
		psi.Arguments = command;
		
		Process process = Process.Start(psi);
		if (process == null)
			return -1;
		
		await process.WaitForExitAsync();
		return process.ExitCode;
	}
}