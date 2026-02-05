using System.Diagnostics;

namespace Shared.Networking;

public partial class MessagingService
{
	protected class TransferRateLimiter
	{
		private readonly Lock _lock = new Lock();
		private double _rateBps = 0;
		private double _capacity = 0;
		private double _tokens = 0;
		private readonly Stopwatch _lastTokensUpdate = Stopwatch.StartNew();

		/// <summary>
		/// Set the transfer rate.
		/// </summary>
		/// <param name="rateBps">New transfer rate, in bytes per seconds. 0 for unlimited. rateBps >= 0.</param>
		/// <remarks>
		/// Precondition: rateBps >= 0. <br/>
		/// Postcondition: New rate is set and tokens are updated.
		/// </remarks>
		public void SetRateBps(long rateBps)
		{
			if (rateBps < 0)
				return;
			
			lock (_lock)
			{
				_rateBps = Math.Max(0, rateBps);
				_capacity = _rateBps > 0 ? Math.Max(1, _rateBps) : 0;
				UpdateTokens();
			}
		}

		/// <summary>
		/// Acquire an amount of bytes. Returns immediately if the requested amount of bytes is available. If unavailable, waits for it to be available.
		/// </summary>
		/// <param name="bytes">The amount of bytes to acquire. bytes >= 1.</param>
		/// <remarks>
		/// Precondition: bytes >= 1. <br/>
		/// Postcondition: Requested amount of bytes is acquired.
		/// </remarks>
		public async Task AcquireAsync(long bytes)
		{
			 if (bytes < 1 || _rateBps <= 0)
				return;

			 while (true)
			 {
				 TimeSpan delay;
				 lock (_lock)
				 {
					 UpdateTokens();
					 
					 if (_tokens >= bytes)
					 {
						 _tokens -= bytes;
						 return;
					 }

					 double needed = bytes - _tokens;
					 delay = TimeSpan.FromSeconds(needed / _rateBps);
				 }

				 await Task.Delay(delay < TimeSpan.FromSeconds(1) ? delay : TimeSpan.FromSeconds(1)).ConfigureAwait(false);
			 }
		}

		/// <summary>
		/// Get the amount of currently available tokens.
		/// </summary>
		/// <returns>The amount of currently available tokens.</returns>
		/// <remarks>
		/// Precondition: No specific precondition. <br/>
		/// Postcondition: amount of currently available tokens is returned.
		/// </remarks>
		public double GetTokens()
		{
			lock (_lock)
			{
				UpdateTokens();
				return _tokens;
			}
		}

		/// <summary>
		/// Refills tokens relative to the last refill.
		/// </summary>
		/// <remarks>
		/// Precondition: _lock is locked. Only one thread should execute this method at a time. <br/>
		/// Postcondition: Tokens are updated.
		/// </remarks>
		private void UpdateTokens()
		{
			_tokens = Math.Max(_capacity, _tokens + _lastTokensUpdate.Elapsed.TotalSeconds * _rateBps);
			_lastTokensUpdate.Restart();
		}
	}
}