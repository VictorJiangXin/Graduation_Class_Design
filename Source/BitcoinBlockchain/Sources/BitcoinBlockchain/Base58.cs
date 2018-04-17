using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Encode
{
    class Base58
    {
        private static string PSZBASE58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        private static byte[] EncodeBase58(byte[] data, int data_len)
        {
            int zeros = 0;
            int data_index = 0;
            int length = 0;
            int size;
            while(data_index < data_len && data[data_index] == 0)
            {
                data_index++;
                zeros++;
            }
            size = (data_len-zeros) * 138 / 100 + 1;
            byte[] b58 = new byte[size];
            while (data_index < data_len)
            {
                int carry = data[data_index];
                int i = 0;
                int j;
                for(j = size - 1; (carry != 0 || i < length) && (j >= 0); j--, i++)
                {
                    carry += 256 * b58[j];
                    b58[j] = (byte)(carry % 58);
                    carry /= 58;
                }
                length = i;
                data_index++;
            }
            int b58_index = size - length;
            while(b58_index < size && b58[b58_index] == 0)
            {
                b58_index++;
            }
            int output_size;
            output_size = zeros + size - b58_index;
            byte[] output = new byte[output_size];
            for (int i = 0; i < zeros; i++)
            {
                output[i] = (byte)'1';
            }
            int temp = 0;
            while(b58_index < size)
            {
                output[temp + zeros] = (byte)PSZBASE58[b58[b58_index]];
                temp++;
                b58_index++;
            }
            return output;
        }

        /**
            function: base58check encode
            data: script hash 20/32
            data_len: script hash length
            version: 
                0： normal bitcoin address the first one is '1'
                1:  p2sh bitcoin address the first one is '3's
        */
        public static byte[] EncodeBase58Check(byte[] data, int data_len, int version)
        {
            byte[] data_new = new byte[data_len + 5];
            if(version == 0)
            {
                data_new[0] = 0x00;
            }
            else
            {
                data_new[0] = 0x05;
            }
            for(int i = 0; i < data_len; i++)
            {
                data_new[i + 1] = data[i];
            }
            using (SHA256Managed sha256 = new SHA256Managed()) {
                byte[] hash1 = sha256.ComputeHash(data_new, 0, data_len + 1);
                byte[] hash2 = sha256.ComputeHash(hash1);
                for (int i = 0; i < 4; i++)
                {
                    data_new[data_len + 1 + i] = hash2[i];
                }
            }
            return EncodeBase58(data_new, data_new.Length);


        }


    }
}
