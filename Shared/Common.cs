using System.Diagnostics;
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
		
		Process? process = Process.Start(psi);
		if (process == null)
			return -1;
		
		await process.WaitForExitAsync();
		return process.ExitCode;
	}

	/// <summary>
	/// Checks if the given email is in valid email syntax.
	/// </summary>
	/// <param name="email">
	/// The email address to check. email != null.
	/// </param>
	/// <returns>
	/// True if the given email is valid, false otherwise.
	/// </returns>
	/// <remarks>
	/// Precondition: email != null. <br/>
	/// Postcondition: Returns true if the given email is valid, false otherwise.
	/// </remarks>
	public static bool IsValidEmail(string email)
	{
		/*
		 * Check the local part of the email
		 */
		
		int localEndIdx = email.IndexOf('@');
		if (localEndIdx > 64 || localEndIdx == -1)
		{
			return false;
		}

		if (email.Count(ch => ch == '@') > 1)
		{
			return false;
		}

		if (email[0] == '.' || email[localEndIdx - 1] == '.')
		{
			return false;
		}

		if (email.Contains(' '))
		{
			return false;
		}
		
		char[] acceptedSpecialInLocal = { '!', '#', '$', '%', '&', '\'', '*', '+', '-', '/', '=', '?', '^', '_', '`', '.', '{', '|', '}', '~' };
		for (int i = 0; i < localEndIdx - 1; ++i)
		{
			if (email[i] == '.' && email[i + 1] == '.')		/* i+1 will not cause index out of range exception because of the '@' indexing */
			{
				return false;
			}
			
			if(!(acceptedSpecialInLocal.Contains(email[i]) || char.IsAsciiLetterOrDigit(email[i])))
			{
				return false;
			}
		}
		
		/*
		 * Check the domain part of the email
		 */

		int domainLength = email.Length - localEndIdx - 1;
		if (domainLength <= 0)	/* Means there is no domain */
		{
			return false;
		}
		
		int domainStartIdx = localEndIdx + 1;
		if (domainLength > 255)
		{
			return false;
		}

		if (email[domainStartIdx] == '-' || email[email.Length - 1] == '-' || email[domainStartIdx] == '.' || email[email.Length - 1] == '.')
		{
			return false;
		}

		int lastDomainStartIdx = domainStartIdx;
		for (int i = domainStartIdx; i < email.Length; ++i)
		{
			if (i - lastDomainStartIdx + 1 > 63)
			{
				return false;
			}
		
			if (email[i] == '.')
			{
				if (i + 1 < email.Length && email[i + 1] == '.')
				{
					return false;
				}
				
				lastDomainStartIdx = i + 1;
			} 
			else if (!(char.IsAsciiLetterOrDigit(email[i]) || email[i] == '-'))
			{
				return false;
			}
		}
		
		return lastDomainStartIdx > domainStartIdx;		/* Means there is at least one dot - separating the domain name from the top-level domain (TLD) */
	}

	public static string SeparateStringWords(string str)
	{
		string result = str;
		for (int i = 1; i < result.Length; i++)
		{
			if (result[i] >= 'A' && result[i] <= 'Z' && !(result[i - 1] >= 'A' && result[i - 1] <= 'Z'))	/* If the current character is capital and the previous isnt */
			{
				result = result.Insert(i, " ");
				++i;
			}
		}
		return result;
	}
}