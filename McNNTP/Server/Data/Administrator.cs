using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using System.Collections.Generic;

namespace McNNTP.Server.Data
{
    public class Administrator
    {
        public virtual int Id { get; set; }
        [NotNull]
        public virtual string Username { get; set; }
        [CanBeNull]
        public virtual string Mailbox { get; set; }
        [NotNull]
        public virtual string PasswordHash { get; set; }
        [NotNull]
        public virtual string PasswordSalt { get; set; }

        /// <summary>
        /// Whether or not the administrator can Approve moderated messages
        /// in any group
        /// </summary>
        public virtual bool CanApproveAny { get; set; }
        public virtual bool CanCancel { get; set; }
        public virtual bool CanCreateGroup { get; set; }
        public virtual bool CanDeleteGroup { get; set; }
        public virtual bool CanCheckGroups { get; set; }
        /// <summary>
        /// Indicates the credential can operate as a server, such as usiing the IHAVE command
        /// </summary>
        public virtual bool CanInject { get; set; }
        /// <summary>
        /// Whether or not the administrator can only authenticate from localhost
        /// </summary>
        public virtual bool LocalAuthenticationOnly { get; set; }

        public virtual IList<Newsgroup> Moderates { get; set; }


        public virtual void SetPassword(SecureString password)
        {
            var saltBytes = new byte[64];
            var rng = RandomNumberGenerator.Create();
            rng.GetNonZeroBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);
            var bstr = Marshal.SecureStringToBSTR(password);
            try
            {
                PasswordHash =
                    Convert.ToBase64String(
                        new SHA512CryptoServiceProvider().ComputeHash(
                            Encoding.UTF8.GetBytes(string.Concat(salt, Marshal.PtrToStringBSTR(bstr)))));
                PasswordSalt = salt;
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }
    }
}
