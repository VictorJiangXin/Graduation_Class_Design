using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Encode
{
    class Hash160
    {
        private Hash160() { }
        public static byte[] hash160(byte[] data)
        {
            byte[] hash1;
            byte[] hash2;
            using (SHA256Managed sha256 = new SHA256Managed())
            {
                hash1 = sha256.ComputeHash(data);
            }
            using (RIPEMD160 myRIPEMD160 = RIPEMD160Managed.Create())
            {
                hash2 = myRIPEMD160.ComputeHash(hash1);
            }
            return hash2;
        }
    }
}
