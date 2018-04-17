using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Encode
{

    class Bench32
    {
        private const string CHAR_SET = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
        private static UInt32 Bench32PolymodStep(UInt32 pre)
        {
            UInt32 ans;
            byte c0 = (byte)(pre >> 25);
            ans = (pre & 0x1ffffff) << 5;
            if ((c0 & 1) != 0) ans ^= 0x3b6a57b2;
            if ((c0 & 2) != 0) ans ^= 0x26508e6d;
            if ((c0 & 4) != 0) ans ^= 0x1ea119fa;
            if ((c0 & 8) != 0) ans ^= 0x3d4233dd;
            if ((c0 & 16) != 0) ans ^= 0x2a1462b3;
            return ans;
        }

        private static byte[] Bench32Encode(string hrp, byte[] data, int data_length)
        {
            byte[] output = new byte[data_length + 7 + hrp.Length];
            UInt32 chk = 1;
            int i = 0;
            while (i < hrp.Length)
            {
                UInt32 ch = hrp[i];
                chk = Bench32PolymodStep(chk) ^ (ch >> 5);
                i++;
            }
            chk = Bench32PolymodStep(chk);
            int index = 0;
            while (index < hrp.Length)
            {
                UInt32 temp;
                temp = (UInt32)(hrp[index] & 0x1f);
                chk = Bench32PolymodStep(chk) ^ temp;
                output[index] = (byte)hrp[index];
                index++;
            }
            output[index++] = (byte)'1';
            int data_index = 0;
            for (i = 0; i < data_length; i++)
            {
                chk = Bench32PolymodStep(chk) ^ (data[data_index]);
                output[index++] = (byte)CHAR_SET[data[data_index++]];
            }
            for (i = 0; i < 6; ++i)
            {
                chk = Bench32PolymodStep(chk);
            }
            chk ^= 1;
            for (i = 0; i < 6; ++i)
            {
                UInt32 temp;
                temp = chk >> ((5 - i) * 5) & 0x1f;
                output[index++] = (byte)CHAR_SET[(int)temp];
            }
            return output;
        }
        private static int ConvertBits(byte[] output, out int out_len, int out_bits, byte[] input, int input_len, int in_bits, int pad)
        {
            UInt32 val = 0;
            int bits = 0;
            int in_index = 0;
            int out_index = 0;
            out_len = 0;
            UInt32 maxv = (UInt32)((1 << out_bits) - 1);
            while ((input_len--) > 0)
            {
                val = (val << in_bits) | input[in_index++];
                bits += in_bits;
                while (bits >= out_bits)
                {
                    bits -= out_bits;
                    output[out_index++] = (byte)((val >> bits) & maxv);
                }
            }
            if (pad != 0)
            {
                if (bits != 0)
                {
                    output[out_index++] = (byte)((val << (out_bits - bits)) & maxv);
                }
            } else if (((val << (out_bits - bits)) & maxv) != 0 || bits >= in_bits)
            {
                return 0;
            }
            out_len = out_index;
            return 1;
        }


        public static byte[] SegwitAddrEncode(string hrp, int witver, byte[] witprog, int witprog_len){
            byte[] data = new byte[65];
            int datalen = 0;
            ConvertBits(data, out datalen, 5, witprog, witprog_len, 8, 1);
            int i;
            for(i = datalen-1; i>=0; i--)
            {
                data[i + 1] = data[i];
            }
            data[0] = (byte)witver;
            datalen++;
            return Bench32Encode(hrp, data, datalen);

        }


    }          
}
