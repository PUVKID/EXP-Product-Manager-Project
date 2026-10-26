using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BunjangAuto
{
    /// <summary>
    /// 로컬에 저장하는 설정·상품 데이터를 이 PC에서만 열어 볼 수 있게 보호한다.
    /// AES-256(CBC) 으로 암호화하고 HMAC-SHA256 으로 위변조 여부를 함께 확인한다(Encrypt-then-MAC).
    /// 키는 비밀번호 없이, 이 PC 고유번호(License.GetMachineId)에서 PBKDF2(SHA-256)로 만들어 내므로
    /// 파일만 복사해 다른 PC로 가져가거나 손으로 편집하면 열리지 않는다.
    /// </summary>
    public static class Vault
    {
        private const string Magic = "BJAV1";
        private const int Iterations = 50000;     // 처음 한 번만 치르는 비용(이후 같은 라벨은 메모리에 캐시)
        private const int KeyBytes = 32;          // AES-256
        private const int MacBytes = 32;          // HMAC-SHA256
        private const int IvBytes = 16;

        private static readonly Dictionary<string, byte[][]> _cache = new Dictionary<string, byte[][]>();
        private static readonly object _gate = new object();

        /// <summary>용도(label)별로 서로 다른 암호키/서명키를 한 번만 유도하고 캐시해 둔다.</summary>
        private static byte[][] Keys(string label)
        {
            lock (_gate)
            {
                byte[][] k;
                if (_cache.TryGetValue(label, out k)) return k;

                string material = "BJA-VAULT-v1|" + label + "|" + License.GetMachineId();
                byte[] salt = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("BunjangAuto-Vault-Salt|" + label));
                byte[] both;
                using (var kdf = new Rfc2898DeriveBytes(material, salt, Iterations, HashAlgorithmName.SHA256))
                    both = kdf.GetBytes(KeyBytes * 2);

                var encKey = new byte[KeyBytes];
                var macKey = new byte[KeyBytes];
                Buffer.BlockCopy(both, 0, encKey, 0, KeyBytes);
                Buffer.BlockCopy(both, KeyBytes, macKey, 0, KeyBytes);
                k = new byte[][] { encKey, macKey };
                _cache[label] = k;
                return k;
            }
        }

        /// <summary>평문 문자열을 이 PC에서만 풀 수 있는 암호문(한 줄 텍스트)으로 바꾼다.</summary>
        public static string Protect(string plainText, string label)
        {
            var keys = Keys(label);
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                aes.Key = keys[0];
                aes.GenerateIV();
                byte[] pt = Encoding.UTF8.GetBytes(plainText ?? "");
                byte[] ct;
                using (var enc = aes.CreateEncryptor()) ct = enc.TransformFinalBlock(pt, 0, pt.Length);

                byte[] payload = new byte[IvBytes + ct.Length];
                Buffer.BlockCopy(aes.IV, 0, payload, 0, IvBytes);
                Buffer.BlockCopy(ct, 0, payload, IvBytes, ct.Length);

                byte[] mac;
                using (var h = new HMACSHA256(keys[1])) mac = h.ComputeHash(payload);

                byte[] all = new byte[payload.Length + MacBytes];
                Buffer.BlockCopy(payload, 0, all, 0, payload.Length);
                Buffer.BlockCopy(mac, 0, all, payload.Length, MacBytes);
                return Magic + ":" + Convert.ToBase64String(all);
            }
        }

        /// <summary>Protect() 로 만든 값을 되돌린다. 형식이 다르거나, 위조되었거나, 다른 PC 것이면 예외를 던진다.</summary>
        public static string Unprotect(string stored, string label)
        {
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Magic + ":"))
                throw new CryptographicException("암호화된 형식이 아닙니다.");
            byte[] all = Convert.FromBase64String(stored.Substring(Magic.Length + 1));
            if (all.Length < IvBytes + MacBytes + 1)
                throw new CryptographicException("손상된 데이터입니다.");

            var keys = Keys(label);
            int payloadLen = all.Length - MacBytes;
            byte[] payload = new byte[payloadLen];
            byte[] mac = new byte[MacBytes];
            Buffer.BlockCopy(all, 0, payload, 0, payloadLen);
            Buffer.BlockCopy(all, payloadLen, mac, 0, MacBytes);

            byte[] expect;
            using (var h = new HMACSHA256(keys[1])) expect = h.ComputeHash(payload);
            if (!FixedTimeEquals(mac, expect))
                throw new CryptographicException("데이터가 손상되었거나 다른 PC에서 만든 파일입니다.");

            byte[] iv = new byte[IvBytes];
            byte[] ct = new byte[payloadLen - IvBytes];
            Buffer.BlockCopy(payload, 0, iv, 0, IvBytes);
            Buffer.BlockCopy(payload, IvBytes, ct, 0, ct.Length);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                aes.Key = keys[0]; aes.IV = iv;
                using (var dec = aes.CreateDecryptor())
                    return Encoding.UTF8.GetString(dec.TransformFinalBlock(ct, 0, ct.Length));
            }
        }

        public static bool IsProtected(string stored)
        {
            return !string.IsNullOrEmpty(stored) && stored.StartsWith(Magic + ":");
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
