using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuickFix45
{
    public class TaskWorker : IDisposable, ITaskWorker
    {
        private int _running = 0;
        private Task _task;
        private CancellationTokenSource _taskCancellation;
        private readonly object _taskSyncLocker = new object();
        private readonly Action _taskAction;

        public TaskWorker(Action taskAction)
        {
            _taskAction = taskAction;
        }

        public void Start()
        {
            lock (_taskSyncLocker)
            {
                if (Interlocked.CompareExchange(ref _running, 1, 0) == 0)
                {
                    _taskCancellation = new CancellationTokenSource();
                    var task = Task.Factory.StartNew(_taskAction, _taskCancellation.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).
                    ContinueWith(t => { Interlocked.Exchange(ref _running, 0); });
                    Interlocked.Exchange(ref _task, task);
                }
            }
        }

        public void Stop(int timeout = -1)
        {
            lock (_taskSyncLocker)
            {
                if (Interlocked.CompareExchange(ref _running, 0, 1) == 1
                    && _task != null)
                {
                    _taskCancellation.Cancel();
                    _task.Wait(timeout);
                }
            }
        }

        public void Dispose()
        {
            try
            {
                Stop();
            }
            catch { }
        }
    }
}
