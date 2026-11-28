#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Lets BASS callbacks be static methods, which IL2CPP (iOS) requires for any delegate handed
    ///     to native code. The owning object is registered here and its id is passed as BASS's
    ///     <c>user</c> pointer; the static callback looks the object back up. Ids are never reused,
    ///     so a late callback after <see cref="Unregister"/> simply finds nothing.
    /// </summary>
    internal static class BassCallbackTargets
    {
        private static readonly ConcurrentDictionary<long, object> _targets = new();
        private static long _nextId;

        public static IntPtr Register(object target)
        {
            long id = Interlocked.Increment(ref _nextId);
            _targets[id] = target;
            return new IntPtr(id);
        }

        public static T? Get<T>(IntPtr id) where T : class
        {
            return _targets.TryGetValue(id.ToInt64(), out var target) ? target as T : null;
        }

        public static void Unregister(IntPtr id)
        {
            if (id != IntPtr.Zero)
            {
                _targets.TryRemove(id.ToInt64(), out _);
            }
        }
    }
}
