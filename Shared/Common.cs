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
		if (localEndIdx > 64 || localEndIdx <= 0)
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

	/// <summary>
	/// Check if the given username is in correct syntax.
	/// </summary>
	/// <param name="username">The username to check the syntax of. username != null.</param>
	/// <returns>True if the username is valid, fale otherwise.</returns>
	/// <remarks>
	/// Precondition: username != null. <br/>
	/// Postcondition: Returns whether the given username is valid or not.
	/// </remarks>
	public static bool IsValidUsername(string username)
	{
		foreach (char c in username)
		{
			foreach (char invalidChar in SharedDefinitions.InvalidUsernameCharacters)
			{
				if (c == invalidChar)
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Checks the strength of the given password.
	/// </summary>
	/// <param name="password">The password to check. password != null.</param>
	/// <returns>The strength of the password, where the weakest is 0, and the strongest is 5.</returns>
	/// <remarks>
	/// Precondition: password != null. <br/>
	/// Postcondition: The strength of the password is returned, where the weakest is 0, and the strongest is 5.
	/// </remarks>
	public static int PasswordStrength(string password)
	{
		if (password.Length < SharedDefinitions.PasswordMinLength)
			return 0;
		
		int symbol = 0;
		int number = 0;
		int upper = 0;
		int lower = 0;
		foreach (char ch in password)
		{
			if (char.IsDigit(ch))
				number = 1;
			
			else if (char.IsUpper(ch))
				upper = 1;
			
			else if (char.IsLower(ch))
				lower = 1;
			
			else
				symbol = 1;
		}

		int strength = 1 + symbol + number + upper + lower;
		
		return strength;
	}

	/// <summary>
	/// Separates words in a string by spaces. Words are indicated by their first letter being capital. (pascal/camel case)
	/// </summary>
	/// <param name="str">The string to separate the words in. str != null.</param>
	/// <returns>A new space-seperated string.</returns>
	/// <remarks>
	/// Precondition: str != null. <br/>
	/// Postcondition: A new space-seperated string is returned.
	/// </remarks>
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
	
	/// <summary>
	/// Checks whether all bytes in the given array are 0.
	/// </summary>
	/// <param name="arr">The byte array to check the bytes of. arr != null.</param>
	/// <returns>True if all the bytes in the given array are 0, false otherwise.</returns>
	/// <remarks>
	/// Precondition: arr != null. <br/>
	/// Postcondition: Returns true if all the bytes in the given array are 0, false otherwise.
	/// </remarks>
	public static bool IsAllBytesZeros(byte[] arr)
	{
		var span = arr.AsSpan();
		int i = 0;

		/* check 8 bytes at a time */
		while (i + 8 <= span.Length)
		{
			if (BitConverter.ToUInt64(span.Slice(i, 8)) != 0UL) return false;
			i += 8;
		}
		
		/* Checks the remaining bytes after the last multiple of 8 */
		while (i < span.Length)		
		{
			if (span[i++] != 0) return false;
		}
		return true;
	}

	/// <summary>
	/// Checks if the given drive size (MiB) is valid for the given operating system.
	/// </summary>
	/// <param name="operatingSystem">The operating system to check on.</param>
	/// <param name="size">The drive size in MiB. size >= 1.</param>
	/// <returns>True if the size is valid, false otherwise.</returns>
	/// <remarks>
	/// Precondition: size >= 1. size in MiB units. <br/>
	/// Postcondition: Returns whether the given drive size is valid for the given operating system.
	/// </remarks>
	public static bool IsOperatingSystemDriveSizeValid(VirtualMachines.OperatingSystem operatingSystem, int size)
	{
		if (size < 1)
			return false;

		return operatingSystem switch
		{
			VirtualMachines.OperatingSystem.MiniCoffeeOS	=> size >= 9 && size <= 10,
			VirtualMachines.OperatingSystem.Ubuntu			=> size >= 25 * 1024,
			VirtualMachines.OperatingSystem.KaliLinux		=> size >= 20 * 1024,
			VirtualMachines.OperatingSystem.ManjaroLinux	=> size >= 30 * 1024,
			_ => true
		};
	}

	/// <summary>
	/// Cleans the given path. Removes leading and trailing white-space and directory separators.
	/// </summary>
	/// <param name="path">The path to clean. path != null.</param>
	/// <returns>A clean version of the given path.</returns>
	/// <remarks>
	/// Precondition: path != null. <br/>
	/// Postcondition: A clean version of the given path is returned.
	/// </remarks>
	public static string CleanPath(string path) => path.Trim().Trim(SharedDefinitions.DirectorySeparators);
	
	/// <summary>
	/// Does the given path point to the drive itself?
	/// </summary>
	/// <param name="path">The path to check. path != null.</param>
	/// <returns>True if the path points to the drive itself, false otherwise.</returns>
	/// <remarks>
	/// Precondition: path != null. <br/>
	/// Postcondition: If the path points to the drive, true is returned. False is returned otherwise.
	/// </remarks>
	public static bool IsPathToDrive(string path)
	{
		string trimmedPath = CleanPath(path);
		string[] pathParts = trimmedPath.Split(SharedDefinitions.DirectorySeparators);

		return pathParts.Length == 0 || (pathParts.Length == 1 && string.IsNullOrEmpty(pathParts[0]));
	}
}