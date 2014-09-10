using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using System.Collections.Generic;

namespace McNNTP.Data
{
    using System.Globalization;

    using McNNTP.Common;

    public class User : IIdentity
    {
        public virtual int Id { get; set; }

        string IIdentity.Id
        {
            get
            {
                return Id.ToString(CultureInfo.InvariantCulture);
            }

            set
            {
                int id;
                if (int.TryParse(value, out id))
                    Id = id;
                Id = -1;
            }
        }

        public virtual string Username { get; set; }

        [CanBeNull]
        public virtual string Mailbox { get; set; }

        public virtual string PasswordHash { get; set; }

        public virtual string PasswordSalt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user can Approve moderated messages in any group
        /// </summary>
        public virtual bool CanApproveAny { get; set; }

        public virtual bool CanCancel { get; set; }

        public virtual bool CanCreateCatalogs { get; set; }

        public virtual bool CanDeleteCatalogs { get; set; }

        public virtual bool CanCheckCatalogs { get; set; }

        /// <summary>
        /// Indicates the credential can operate as a server, such as usiing the IHAVE command
        /// </summary>
        public virtual bool CanInject { get; set; }
       
        /// <summary>
        /// Whether or not the user can only authenticate from localhost
        /// </summary>
        public virtual bool LocalAuthenticationOnly { get; set; }

        public virtual IList<Newsgroup> Moderates { get; set; }

        IEnumerable<ICatalog> IIdentity.Moderates
        {
            get
            {
                return this.Moderates;
            }
        }

        public virtual DateTime? LastLogin { get; set; }


        public virtual void SetPassword(SecureString password)
        {
            var saltBytes = new byte[64];
            var rng = RandomNumberGenerator.Create();
            rng.GetNonZeroBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);
            var bstr = Marshal.SecureStringToBSTR(password);
            try
            {
                PasswordHash = Convert.ToBase64String(new SHA512CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(string.Concat(salt, Marshal.PtrToStringBSTR(bstr)))));
                PasswordSalt = salt;
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }

        public override bool Equals(object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Point return false.
            var p = obj as User;
            if ((object)p == null)
                return false;

            // Return true if the fields match:
            return Id == p.Id;
        }

        public override int GetHashCode()
        {
            return Id;
        }
    }
}
