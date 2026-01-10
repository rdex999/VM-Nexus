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

	public ulong MaxSize
	{
		get
		{
			if (_drive == null)
				return (ulong)Stream.Length;

			if (_fileSystem == null)
				return (ulong)_drive.Capacity;

			return (ulong)_fileSystem.AvailableSpace;
		}
	}
	
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

	/// <summary>
	/// Disposes used objects and this object.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: This object, and objects used by this object are disposed.
	/// </remarks>
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