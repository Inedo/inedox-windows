using System;
using System.ComponentModel;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.Windows.Functions
{
    [ScriptAlias("PSCredential")]
    [Description("Returns a PSCredential object that can be passed to PowerShell scripts.")]
    [Tag("PowerShell")]
    [Example(@"
# convert user and password to a PSCredential object
PSCall MyPowerShellScript
(
    Credentials: $PSCredential(user, password)
);
")]
    [Category("PowerShell")]
    public sealed class PsCredentialVariableFunction : ScalarVariableFunction
    {
        internal static string Prefix = "{229C065F-9E8F-4679-AD52-0DD5B7334FA3}";

        [DisplayName("userName")]
        [VariableFunctionParameter(0)]
        [Description("The user name of the PSCredential object.")]
        public string UserName { get; set; }
        [DisplayName("password")]
        [VariableFunctionParameter(1)]
        [Description("The password of the PSCredential object.")]
        public SecureString Password { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => this.Serialize();

        internal static PSCredential Deserialize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return PSCredential.Empty;

            var buffer = Convert.FromBase64String(s);
            try
            {
                int userNameLength = BitConverter.ToInt32(buffer, 0);
                var userName = InedoLib.UTF8Encoding.GetString(buffer, sizeof(int), userNameLength);

                int passwordLength = BitConverter.ToInt32(buffer, userNameLength + sizeof(int));
                var passwordBuffer = InedoLib.UTF8Encoding.GetChars(buffer, userNameLength + (sizeof(int) * 2), passwordLength);
                try
                {
                    SecureString password;
                    unsafe
                    {
                        fixed (char* passwordPtr = passwordBuffer)
                        {
                            password = new SecureString(passwordPtr, passwordBuffer.Length);
                            password.MakeReadOnly();
                        }
                    }

                    return new PSCredential(userName, password);
                }
                finally
                {
                    Array.Clear(passwordBuffer, 0, passwordBuffer.Length);
                }
            }
            finally
            {
                Array.Clear(buffer, 0, buffer.Length);
            }
        }

        private string Serialize()
        {
            var userName = InedoLib.UTF8Encoding.GetBytes(this.UserName ?? string.Empty);
            var password = default(IntPtr);
            try
            {
                password = Marshal.SecureStringToBSTR(this.Password ?? new SecureString());
                unsafe
                {
                    int passwordByteCount = InedoLib.UTF8Encoding.GetByteCount((char*)password, this.Password.Length);
                    var buffer = new byte[userName.Length + passwordByteCount + (sizeof(int) * 2)];
                    try
                    {
                        fixed (byte* bufferPtr = buffer)
                        {
                            *(int*)&bufferPtr[0] = userName.Length;
                            Marshal.Copy(userName, 0, new IntPtr(&bufferPtr[sizeof(int)]), userName.Length);

                            *(int*)&bufferPtr[userName.Length + sizeof(int)] = passwordByteCount;
                            InedoLib.UTF8Encoding.GetBytes((char*)password.ToPointer(), this.Password.Length, &bufferPtr[userName.Length + (sizeof(int) * 2)], passwordByteCount);
                        }

                        return Prefix + Convert.ToBase64String(buffer);
                    }
                    finally
                    {
                        Array.Clear(buffer, 0, buffer.Length);
                    }
                }
            }
            finally
            {
                if (password != default)
                    Marshal.ZeroFreeBSTR(password);
            }
        }
    }
}
