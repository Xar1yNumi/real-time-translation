using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Credentials;

public sealed class WindowsCredentialStore : ICredentialStore
{
    public const string DefaultTargetName = "RealTimeTranslation/AzureSpeech";

    private const uint GenericCredentialType = 1;
    private const uint LocalMachinePersistence = 2;
    private const int ErrorNotFound = 1168;
    private readonly string targetName;

    public WindowsCredentialStore(string targetName = DefaultTargetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        this.targetName = targetName;
    }

    public async ValueTask WriteApiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API Key cannot be empty.", nameof(apiKey));
        }

        await Task.Run(() => WriteCore(apiKey), cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<string?> ReadApiKeyAsync(CancellationToken cancellationToken) =>
        new(Task.Run(ReadCore, cancellationToken));

    public ValueTask DeleteApiKeyAsync(CancellationToken cancellationToken) =>
        new(Task.Run(DeleteCore, cancellationToken));

    private void WriteCore(string apiKey)
    {
        var secretBytes = Encoding.Unicode.GetBytes(apiKey);
        var secretPointer = Marshal.AllocHGlobal(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, secretPointer, secretBytes.Length);
            var credential = new NativeCredential
            {
                Type = GenericCredentialType,
                TargetName = targetName,
                CredentialBlobSize = checked((uint)secretBytes.Length),
                CredentialBlob = secretPointer,
                Persist = LocalMachinePersistence,
                UserName = Environment.UserName
            };

            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
        finally
        {
            ZeroUnmanagedMemory(secretPointer, secretBytes.Length);
            Marshal.FreeHGlobal(secretPointer);
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }

    private string? ReadCore()
    {
        if (!NativeMethods.CredRead(targetName, GenericCredentialType, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlobSize == 0)
            {
                return string.Empty;
            }

            var secretBytes = new byte[credential.CredentialBlobSize];
            try
            {
                Marshal.Copy(credential.CredentialBlob, secretBytes, 0, secretBytes.Length);
                return Encoding.Unicode.GetString(secretBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secretBytes);
            }
        }
        finally
        {
            NativeMethods.CredFree(credentialPointer);
        }
    }

    private void DeleteCore()
    {
        if (NativeMethods.CredDelete(targetName, GenericCredentialType, 0))
        {
            return;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error != ErrorNotFound)
        {
            throw new Win32Exception(error);
        }
    }

    private static void ZeroUnmanagedMemory(IntPtr pointer, int byteCount)
    {
        for (var index = 0; index < byteCount; index++)
        {
            Marshal.WriteByte(pointer, index, 0);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Comment;

        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? TargetAlias;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string UserName;
    }

    private static class NativeMethods
    {
        [DllImport(
            "advapi32.dll",
            EntryPoint = "CredWriteW",
            ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredWrite(ref NativeCredential credential, uint flags);

        [DllImport(
            "advapi32.dll",
            EntryPoint = "CredReadW",
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredRead(
            string target,
            uint type,
            uint flags,
            out IntPtr credential);

        [DllImport(
            "advapi32.dll",
            EntryPoint = "CredDeleteW",
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll", ExactSpelling = true)]
        public static extern void CredFree(IntPtr credential);
    }
}
