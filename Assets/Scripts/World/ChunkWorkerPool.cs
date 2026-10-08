using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DivergentGenesis.World
{
    public struct GenJob
    {
        public int Cx, Cz, Level, Seed;
        public bool Voxels;
        /// <summary>Which world the job belongs to. Stale epochs are discarded.</summary>
        public int Gen;
    }

    public struct GenResult
    {
        public ChunkData Data;
        public int Cx, Cz, Level;
        public int Gen;
    }

    /// <summary>
    /// A tiny worker pool. Terrain generation is pure maths, so it is perfectly
    /// safe off the main thread - which is the only way a phone can keep 60 fps
    /// while streaming a 2 km view.
    /// </summary>
    public sealed class ChunkWorkerPool
    {
        private readonly ConcurrentQueue<GenJob> _pending = new ConcurrentQueue<GenJob>();
        private readonly ConcurrentQueue<GenResult> _ready = new ConcurrentQueue<GenResult>();
        private readonly Thread[] _threads;
        private volatile bool _running = true;
        private int _busy;

        public ChunkWorkerPool(int threadCount)
        {
            threadCount = Math.Max(1, Math.Min(threadCount, 8));
            _threads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                var t = new Thread(Worker);
                t.IsBackground = true;
                t.Name = "DG-ChunkGen-" + i;
                t.Priority = ThreadPriority.BelowNormal;
                _threads[i] = t;
                t.Start();
            }
        }

        public int BusyWorkers { get { return Volatile.Read(ref _busy); } }
        public int PendingCount { get { return _pending.Count; } }

        public void Enqueue(in GenJob job) { _pending.Enqueue(job); }

        public bool TryDequeue(out GenResult result) { return _ready.TryDequeue(out result); }

        private void Worker()
        {
            while (_running)
            {
                GenJob job;
                if (!_pending.TryDequeue(out job))
                {
                    Thread.Sleep(4);
                    continue;
                }

                Interlocked.Increment(ref _busy);
                try
                {
                    var data = ChunkGenerator.Generate(job.Cx, job.Cz, job.Level, job.Seed, job.Voxels);
                    _ready.Enqueue(new GenResult { Data = data, Cx = job.Cx, Cz = job.Cz, Level = job.Level, Gen = job.Gen });
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[DivergentGenesis] chunk generation failed: " + e);
                }
                finally
                {
                    Interlocked.Decrement(ref _busy);
                }
            }
        }

        public void Dispose()
        {
            _running = false;
            for (int i = 0; i < _threads.Length; i++)
            {
                try { _threads[i].Join(200); } catch { /* shutting down */ }
            }
        }
    }
}
