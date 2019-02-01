// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class PeriodicTask : IDisposable
    {
        readonly Func<CancellationToken, Task> work;
        readonly TimeSpan frequency;
        readonly TimeSpan startAfter;
        readonly object stateLock = new object();
        readonly ILogger logger;
        readonly string operationName;
        readonly Timer checkTimer;
        readonly CancellationTokenSource cts = new CancellationTokenSource();

        Task currentRuner;

        public PeriodicTask(
            Func<CancellationToken, Task> work,
            TimeSpan frequency,
            TimeSpan startAfter,
            ILogger logger,
            string operationName)
        {
            Preconditions.CheckArgument(frequency > TimeSpan.Zero, "Frequency should be > 0");
            Preconditions.CheckArgument(startAfter >= TimeSpan.Zero, "startAfter should be >= 0");

            this.work = Preconditions.CheckNotNull(work);
            this.frequency = frequency;
            this.startAfter = startAfter;
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.operationName = Preconditions.CheckNonWhiteSpace(operationName, nameof(operationName));
            this.currentRuner = this.DoWork();
            this.checkTimer = new Timer(this.EnsureWork, null, frequency, startAfter);
            this.logger.LogInformation($"Started operation {this.operationName}");
        }

        public PeriodicTask(
            Func<Task> work,
            TimeSpan frequency,
            TimeSpan startAfter,
            ILogger logger,
            string operationName)
            : this(_ => work(), frequency, startAfter, logger, operationName)
        {
        }

        public void Dispose()
        {
            this.checkTimer?.Dispose();
            this.cts?.Cancel();
            this.cts?.Dispose();
            this.currentRuner?.Dispose();
        }

        void EnsureWork(object state)
        {
            lock (this.stateLock)
            {
                if (this.currentRuner == null || this.currentRuner.IsCompleted)
                {
                    this.logger.LogInformation($"Periodic operation {this.operationName}, is not running. Attempting to start again...");
                    this.currentRuner = this.DoWork();
                    this.logger.LogInformation($"Started operation {this.operationName}");
                }
            }
        }

        async Task DoWork()
        {
            try
            {
                CancellationToken cancellationToken = this.cts.Token;
                await Task.Delay(this.startAfter, cancellationToken);
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        this.logger.LogInformation($"Starting periodic operation {this.operationName}...");
                        await this.work(cancellationToken);
                        this.logger.LogInformation($"Successfully completed periodic operation {this.operationName}");
                    }
                    catch (Exception e)
                    {
                        this.logger.LogWarning(e, $"Error in periodic operation {this.operationName}");
                    }

                    await Task.Delay(this.frequency, cancellationToken);
                }

                this.logger.LogDebug($"Periodic operation {this.operationName} cancelled");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Unexpected error in periodic operation {this.operationName}");
            }
        }
    }
}
