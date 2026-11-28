using System;
using System.IO;
using AOT;
using ManagedBass;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Feeds a managed <see cref="Stream"/> to BASS. The callbacks are static (with the instance
    ///     found through <see cref="UserData"/>) because IL2CPP cannot pass instance-method delegates
    ///     to native code; lambdas capturing <c>this</c> made every song fail to load on iOS.
    /// </summary>
    public sealed class BassStreamProcedures : FileProcedures
    {
        private static readonly FileCloseProcedure  CLOSE  = OnClose;
        private static readonly FileLengthProcedure LENGTH = OnLength;
        private static readonly FileReadProcedure   READ   = OnRead;
        private static readonly FileSeekProcedure   SEEK   = OnSeek;

        private readonly Stream _stream;
        private readonly long _start;
        private readonly long _length;

        /// <summary>Pass this as the <c>user</c> argument of the BASS call that uses these procedures.</summary>
        public IntPtr UserData { get; }

        public BassStreamProcedures(Stream stream)
        {
            _stream = stream;
            _start = stream.Position;
            _length = stream.Length - _start;

            Close = CLOSE;
            Length = LENGTH;
            Read = READ;
            Seek = SEEK;

            UserData = BassCallbackTargets.Register(this);
        }

        /// <summary>Drops the registration without closing the stream (used when stream creation fails).</summary>
        public void Release() => BassCallbackTargets.Unregister(UserData);

        [MonoPInvokeCallback(typeof(FileCloseProcedure))]
        private static void OnClose(IntPtr user)
        {
            var self = BassCallbackTargets.Get<BassStreamProcedures>(user);
            if (self == null)
            {
                return;
            }

            self.Release();
            self._stream.Close();
        }

        [MonoPInvokeCallback(typeof(FileLengthProcedure))]
        private static long OnLength(IntPtr user)
        {
            return BassCallbackTargets.Get<BassStreamProcedures>(user)?._length ?? 0;
        }

        [MonoPInvokeCallback(typeof(FileReadProcedure))]
        private static int OnRead(IntPtr buffer, int length, IntPtr user)
        {
            var self = BassCallbackTargets.Get<BassStreamProcedures>(user);
            if (self == null)
            {
                return 0;
            }

            try
            {
                unsafe
                {
                    return self._stream.Read(new Span<byte>((byte*) buffer, length));
                }
            }
            catch
            {
                return 0;
            }
        }

        [MonoPInvokeCallback(typeof(FileSeekProcedure))]
        private static bool OnSeek(long offset, IntPtr user)
        {
            var self = BassCallbackTargets.Get<BassStreamProcedures>(user);
            if (self == null)
            {
                return false;
            }

            try
            {
                self._stream.Seek(offset + self._start, SeekOrigin.Begin);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
