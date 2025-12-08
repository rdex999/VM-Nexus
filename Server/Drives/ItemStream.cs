using System;
using System.IO;
using DiscUtils;
using DiscUtils.Raw;

namespace Server.Drives;

public class ItemStream : IDisposable
{
	public Stream Stream { get; }
	private Disk? _drive = null;
	private DiscFileSystem? _fileSystem = null;

	public ItemStream(Stream stream)
	{
		Stream = stream;
	}

	public ItemStream(Stream stream, Disk drive)
	{
		Stream = stream;
		_drive = drive;
	}
	
	public ItemStream(Stream stream, Disk drive, DiscFileSystem fileSystem)
	{
		Stream = stream;
		_drive = drive;
		_fileSystem = fileSystem;
	}
	
	public void Dispose()
	{
		try
		{
			Stream.Dispose();
		}
		catch (ObjectDisposedException)
		{
		}
		
		if  (_drive != null)
		{
			_drive.Dispose();
			_drive = null;
		}
		
		if (_fileSystem != null)
		{ 
			_fileSystem.Dispose();
			_fileSystem = null;
		}
	}
}