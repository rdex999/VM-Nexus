using System.Text.Json;

namespace Shared;

public static class Common
{
	public static byte[] ToByteArray(object? obj)
	{
		return JsonSerializer.SerializeToUtf8Bytes(obj);
	}

	public static T? FromByteArray<T>(byte[] bytes)
	{
		return JsonSerializer.Deserialize<T>(bytes);
	}
}