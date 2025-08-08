using System.Diagnostics;
using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Shared;

public static class Common
{
	public static byte[] ToByteArray(object? obj)
	{
		return JsonSerializer.SerializeToUtf8Bytes(obj);
	}
	
	public static T? FromByteArray<T>(byte[]? bytes)
	{
		return JsonSerializer.Deserialize<T>(bytes);
	}
	
	
	public static byte[] ToByteArrayWithType(object? obj)
	{
		var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
		var json = JsonConvert.SerializeObject(obj, settings);
		return System.Text.Encoding.UTF8.GetBytes(json);
	}

	public static object? FromByteArrayWithType(byte[]? bytes)
	{
		if (bytes == null) 
			return null;
		
		var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
		var json = System.Text.Encoding.UTF8.GetString(bytes);
		return JsonConvert.DeserializeObject(json, settings);
	}

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