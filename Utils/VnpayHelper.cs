using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
namespace VnpayPymentQR.Utils
{
    
    public class VnpayHelper
    {
        private readonly string _hashSecret;

        public VnpayHelper(string hashSecret)
        {
            _hashSecret = hashSecret;
        }

        public string CreateSignature(SortedDictionary<string, string> paramsDict)
        {
            var signData = string.Join("&", paramsDict.Select(kvp =>$"{kvp.Key}={System.Net.WebUtility.UrlEncode(kvp.Value)}"));
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_hashSecret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signData));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        public bool ValidateSignature(IDictionary<string, string?> inputData, string inputHash)
        {
            var paramsDict = new SortedDictionary<string, string>();
            foreach (var kvp in inputData.Where(kvp => kvp.Key.StartsWith("vnp_") && kvp.Key != "vnp_SecureHash"))
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                    paramsDict[kvp.Key] = kvp.Value;
            }
            var signData = string.Join("&", paramsDict.Select(kvp => $"{kvp.Key}={System.Net.WebUtility.UrlEncode(kvp.Value)}"));
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_hashSecret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signData));
            var calculatedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            return calculatedHash.Equals(inputHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string HmacSHA512(string key, string inputData)
        {
            var hash = new StringBuilder();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
            using (var hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashValue = hmac.ComputeHash(inputBytes);
                foreach (var b in hashValue)
                    hash.Append(b.ToString("x2"));
            }
            return hash.ToString();
        }
    }
}
