using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace iiMenu.Managers
{
    public static class SmtcReader
    {
        private const string ClassName = "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager";
        private static readonly Guid StaticsIid = new Guid("2050C4EE-11A0-57DE-AED7-C97C70338245");

        private const int SlotRequestAsync = 6;
        private const int SlotManagerGetCurrentSession = 6;
        private const int SlotSessionMediaProperties = 7;
        private const int SlotSessionTimeline = 8;
        private const int SlotSessionPlaybackInfo = 9;
        private const int SlotPropTitle = 6;
        private const int SlotPropArtist = 9;
        private const int SlotPropThumbnail = 15;
        private const int SlotReferenceOpenRead = 6;
        private const int SlotStreamSize = 6;
        private const int SlotStreamGetInput = 8;
        private const int SlotInputRead = 6;
        private const int SlotBufferCreate = 6;
        private const int SlotBufferLength = 7;
        private const int SlotByteAccess = 3;
        private const int SlotPlaybackStatus = 7;
        private const int SlotTimelineEnd = 7;
        private const int SlotTimelinePosition = 10;
        private const int SlotAsyncStatus = 7;
        private const int SlotAsyncCancel = 9;
        private const int SlotAsyncResults = 13;
        private const int SlotOpWithProgressResults = 15;

        private const string BufferClassName = "Windows.Storage.Streams.Buffer";
        private static readonly Guid BufferFactoryIid = new Guid("71AF914D-C10F-484B-BC50-14BC623B3A27");
        private static readonly Guid BufferByteAccessIid = new Guid("905A0FEF-BC53-11DF-8C49-001E4FC68AD1");
        private const int ArtMaxBytes = 8388608;

        private const int StatusStarted = 0;
        private const int PlaybackPlaying = 4;

        private const int PollMs = 2000;
        private const int AcquireTimeoutMs = 3000;
        private const int FetchTimeoutMs = 2000;

        [DllImport("combase.dll")]
        private static extern int RoInitialize(int model);

        [DllImport("combase.dll")]
        private static extern void RoUninitialize();

        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(IntPtr className, ref Guid iid, out IntPtr factory);

        [DllImport("combase.dll", CharSet = CharSet.Unicode)]
        private static extern int WindowsCreateString(string source, int length, out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out int length);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetString(IntPtr self, out IntPtr value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetOp(IntPtr self, out IntPtr value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetInt32(IntPtr self, out int value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetInt64(IntPtr self, out long value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetSize(IntPtr self, out long value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetStreamAt(IntPtr self, ulong position, out IntPtr stream);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ReadInto(IntPtr self, IntPtr buffer, uint count, int options, out IntPtr operation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateBuffer(IntPtr self, uint capacity, out IntPtr buffer);

        private static T Slot<T>(IntPtr obj, int index) where T : class
        {
            IntPtr vtbl = Marshal.ReadIntPtr(obj);
            IntPtr fn = Marshal.ReadIntPtr(vtbl, index * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<T>(fn);
        }

        private static string ReadHString(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return string.Empty;
            IntPtr buffer = WindowsGetStringRawBuffer(handle, out int length);
            string value = buffer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(buffer, length);
            WindowsDeleteString(handle);
            return value;
        }

        private static int WaitOp(IntPtr op, int timeoutMs)
        {
            GetInt32 status = Slot<GetInt32>(op, SlotAsyncStatus);
            long deadline = Environment.TickCount + timeoutMs;
            while (Environment.TickCount < deadline)
            {
                int hr = status(op, out int current);
                if (hr != 0)
                    return hr;
                if (current != StatusStarted)
                    return 0;
                Thread.Sleep(10);
            }
            Slot<GetInt32>(op, SlotAsyncCancel)(op, out _);
            return -1;
        }

        public sealed class Snapshot
        {
            public bool HasData;
            public string Title;
            public string Artist;
            public bool Paused;
            public float Start;
            public float End;
            public float Position;
            public byte[] Art;
        }

        private static readonly object gate = new object();
        private static Snapshot latest = new Snapshot { HasData = false, Title = "Unknown", Artist = "Unknown", Paused = true };
        private static Thread worker;
        private static bool running;

        public static bool Running
        {
            get
            {
                lock (gate)
                    return worker != null && worker.IsAlive && running;
            }
        }

        public static void Begin()
        {
            lock (gate)
            {
                if (worker != null && worker.IsAlive)
                    return;
                running = true;
                worker = new Thread(Loop) { IsBackground = true, Name = "iiMenu.Smtc" };
                worker.Start();
            }
        }

        public static void End()
        {
            lock (gate)
                running = false;
        }

        public static Snapshot Current()
        {
            lock (gate)
                return latest;
        }

        private static void Publish(Snapshot value)
        {
            lock (gate)
                latest = value;
        }

        private static void Loop()
        {
            int hr = RoInitialize(1);
            if (hr != 0 && hr != 1)
            {
                LogManager.Log($"smtc roinitialize failed 0x{hr:X8}");
                return;
            }

            IntPtr manager = IntPtr.Zero;
            byte[] art = null;
            string artTitle = null;

            while (true)
            {
                lock (gate)
                    if (!running)
                        break;

                if (manager == IntPtr.Zero)
                {
                    manager = AcquireManager(out bool permanent);
                    if (permanent)
                        break;
                    if (manager == IntPtr.Zero)
                    {
                        Thread.Sleep(PollMs);
                        continue;
                    }
                }

                Snapshot snapshot = Read(manager, artTitle);
                if (snapshot == null)
                {
                    Marshal.Release(manager);
                    manager = IntPtr.Zero;
                    Thread.Sleep(PollMs);
                    continue;
                }

                if (snapshot.Art != null)
                {
                    art = snapshot.Art;
                    artTitle = snapshot.Title;
                }
                else if (snapshot.Title == artTitle)
                {
                    snapshot.Art = art;
                }
                else
                {
                    art = null;
                    artTitle = null;
                }

                Publish(snapshot);
                Thread.Sleep(PollMs);
            }

            if (manager != IntPtr.Zero)
                Marshal.Release(manager);
            RoUninitialize();
        }

        private static IntPtr AcquireManager(out bool permanent)
        {
            permanent = false;

            int hr = WindowsCreateString(ClassName, ClassName.Length, out IntPtr classId);
            if (hr != 0)
                return IntPtr.Zero;

            IntPtr factory = IntPtr.Zero;
            try
            {
                Guid iid = StaticsIid;
                hr = RoGetActivationFactory(classId, ref iid, out factory);
                if (hr != 0)
                {
                    if (hr == unchecked((int)0x80040154))
                    {
                        permanent = true;
                        LogManager.Log("smtc unavailable on this os");
                    }
                    return IntPtr.Zero;
                }

                hr = Slot<GetOp>(factory, SlotRequestAsync)(factory, out IntPtr op);
                if (hr != 0 || op == IntPtr.Zero)
                    return IntPtr.Zero;

                try
                {
                    if (WaitOp(op, AcquireTimeoutMs) != 0)
                        return IntPtr.Zero;
                    hr = Slot<GetOp>(op, SlotAsyncResults)(op, out IntPtr result);
                    if (hr != 0 || result == IntPtr.Zero)
                        return IntPtr.Zero;
                    return result;
                }
                finally
                {
                    Marshal.Release(op);
                }
            }
            finally
            {
                if (factory != IntPtr.Zero)
                    Marshal.Release(factory);
                WindowsDeleteString(classId);
            }
        }

        private static Snapshot Read(IntPtr manager, string artTitle)
        {
            int hr = Slot<GetOp>(manager, SlotManagerGetCurrentSession)(manager, out IntPtr session);
            if (hr != 0)
                return null;

            if (session == IntPtr.Zero)
                return new Snapshot { HasData = false, Title = "Unknown", Artist = "Unknown", Paused = true };

            try
            {
                Snapshot snapshot = new Snapshot { HasData = false, Title = "Unknown", Artist = "Unknown", Paused = true };

                hr = Slot<GetOp>(session, SlotSessionMediaProperties)(session, out IntPtr propOp);
                if (hr == 0 && propOp != IntPtr.Zero)
                {
                    try
                    {
                        if (WaitOp(propOp, FetchTimeoutMs) == 0 && Slot<GetOp>(propOp, SlotAsyncResults)(propOp, out IntPtr props) == 0 && props != IntPtr.Zero)
                        {
                            try
                            {
                                if (Slot<GetString>(props, SlotPropTitle)(props, out IntPtr titleHandle) == 0)
                                    snapshot.Title = ReadHString(titleHandle);
                                if (Slot<GetString>(props, SlotPropArtist)(props, out IntPtr artistHandle) == 0)
                                    snapshot.Artist = ReadHString(artistHandle);
                                snapshot.Art = snapshot.Title != artTitle ? ReadThumbnail(props) : null;
                            }
                            finally
                            {
                                Marshal.Release(props);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.Release(propOp);
                    }
                }

                hr = Slot<GetOp>(session, SlotSessionPlaybackInfo)(session, out IntPtr playback);
                if (hr == 0 && playback != IntPtr.Zero)
                {
                    try
                    {
                        if (Slot<GetInt32>(playback, SlotPlaybackStatus)(playback, out int status) == 0)
                            snapshot.Paused = status != PlaybackPlaying;
                    }
                    finally
                    {
                        Marshal.Release(playback);
                    }
                }

                hr = Slot<GetOp>(session, SlotSessionTimeline)(session, out IntPtr timeline);
                if (hr == 0 && timeline != IntPtr.Zero)
                {
                    try
                    {
                        if (Slot<GetInt64>(timeline, SlotTimelinePosition)(timeline, out long position) == 0)
                            snapshot.Position = (float)(position / 1e7);
                        if (Slot<GetInt64>(timeline, SlotTimelineEnd)(timeline, out long end) == 0)
                            snapshot.End = (float)(end / 1e7);
                        snapshot.Start = 0f;
                    }
                    finally
                    {
                        Marshal.Release(timeline);
                    }
                }

                snapshot.HasData = true;
                return snapshot;
            }
            finally
            {
                Marshal.Release(session);
            }
        }
        private static byte[] ReadThumbnail(IntPtr props)
        {
            if (Slot<GetOp>(props, SlotPropThumbnail)(props, out IntPtr reference) != 0 || reference == IntPtr.Zero)
                return null;

            try
            {
                if (Slot<GetOp>(reference, SlotReferenceOpenRead)(reference, out IntPtr openOp) != 0 || openOp == IntPtr.Zero)
                    return null;

                try
                {
                    if (WaitOp(openOp, FetchTimeoutMs) != 0)
                        return null;
                    if (Slot<GetOp>(openOp, SlotOpWithProgressResults)(openOp, out IntPtr stream) != 0 || stream == IntPtr.Zero)
                        return null;

                    try
                    {
                        if (Slot<GetSize>(stream, SlotStreamSize)(stream, out long size) != 0 || size <= 0 || size > ArtMaxBytes)
                            return null;

                        if (Slot<GetStreamAt>(stream, SlotStreamGetInput)(stream, 0, out IntPtr input) != 0 || input == IntPtr.Zero)
                            return null;

                        try
                        {
                            IntPtr buffer = CreateBuffer((uint)size);
                            if (buffer == IntPtr.Zero)
                                return null;

                            try
                            {
                                if (Slot<ReadInto>(input, SlotInputRead)(input, buffer, (uint)size, 0, out IntPtr readOp) != 0 || readOp == IntPtr.Zero)
                                    return null;

                                IntPtr resultBuffer = IntPtr.Zero;
                                try
                                {
                                    if (WaitOp(readOp, FetchTimeoutMs) != 0)
                                        return null;
                                    if (Slot<GetOp>(readOp, SlotOpWithProgressResults)(readOp, out resultBuffer) != 0 || resultBuffer == IntPtr.Zero)
                                        return null;
                                    return CopyBuffer(resultBuffer);
                                }
                                finally
                                {
                                    if (resultBuffer != IntPtr.Zero && resultBuffer != buffer)
                                        Marshal.Release(resultBuffer);
                                    Marshal.Release(readOp);
                                }
                            }
                            finally
                            {
                                Marshal.Release(buffer);
                            }
                        }
                        finally
                        {
                            Marshal.Release(input);
                        }
                    }
                    finally
                    {
                        Marshal.Release(stream);
                    }
                }
                finally
                {
                    Marshal.Release(openOp);
                }
            }
            finally
            {
                Marshal.Release(reference);
            }
        }

        private static byte[] CopyBuffer(IntPtr buffer)
        {
            if (Slot<GetInt32>(buffer, SlotBufferLength)(buffer, out int length) != 0 || length <= 0 || length > ArtMaxBytes)
                return null;

            Guid iid = BufferByteAccessIid;
            if (Marshal.QueryInterface(buffer, ref iid, out IntPtr byteAccess) != 0 || byteAccess == IntPtr.Zero)
                return null;

            try
            {
                if (Slot<GetOp>(byteAccess, SlotByteAccess)(byteAccess, out IntPtr data) != 0 || data == IntPtr.Zero)
                    return null;

                byte[] art = new byte[length];
                Marshal.Copy(data, art, 0, length);
                return art;
            }
            finally
            {
                Marshal.Release(byteAccess);
            }
        }

        private static IntPtr CreateBuffer(uint capacity)
        {
            int hr = WindowsCreateString(BufferClassName, BufferClassName.Length, out IntPtr classId);
            if (hr != 0)
                return IntPtr.Zero;

            try
            {
                Guid iid = BufferFactoryIid;
                hr = RoGetActivationFactory(classId, ref iid, out IntPtr factory);
                if (hr != 0 || factory == IntPtr.Zero)
                    return IntPtr.Zero;

                try
                {
                    return Slot<CreateBuffer>(factory, SlotBufferCreate)(factory, capacity, out IntPtr buffer) == 0 && buffer != IntPtr.Zero ? buffer : IntPtr.Zero;
                }
                finally
                {
                    Marshal.Release(factory);
                }
            }
            finally
            {
                WindowsDeleteString(classId);
            }
        }
    }
}
