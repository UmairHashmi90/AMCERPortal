using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace ERPaperless.Services
{
    public static class Data
    {

        public static String key = "key@hmt.com.pk";

        public static string Encrypt(string toEncrypt, bool useHashing = true)
        {
            byte[] keyArray;

            byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(toEncrypt);

            //string key = "SoftSolPakistan";

            //System.Windows.Forms.MessageBox.Show(key);

            //If hashing use get hashcode regards to your key

            if (useHashing)
            {

                MD5CryptoServiceProvider hashmd5 = new MD5CryptoServiceProvider();

                keyArray = hashmd5.ComputeHash(UTF8Encoding.UTF8.GetBytes(key));

                //Always release the resources and flush data

                // of the Cryptographic service provide. Best Practice

                hashmd5.Clear();

            }
            else
                keyArray = UTF8Encoding.UTF8.GetBytes(key);

            TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider();

            //set the secret key for the tripleDES algorithm

            tdes.Key = keyArray;

            //mode of operation. there are other 4 modes.

            //We choose ECB(Electronic code Book)

            tdes.Mode = CipherMode.ECB;

            //padding mode(if any extra byte added)

            tdes.Padding = PaddingMode.PKCS7;

            ICryptoTransform cTransform = tdes.CreateEncryptor();

            //transform the specified region of bytes array to resultArray

            byte[] resultArray =

              cTransform.TransformFinalBlock(toEncryptArray, 0,

              toEncryptArray.Length);

            //Release resources held by TripleDes Encryptor

            tdes.Clear();

            //Return the encrypted data into unreadable string format
            //return Convert.ToBase64String(resultArray, 0, resultArray.Length);
            string hex = BitConverter.ToString(resultArray);
            hex = hex.Replace("-", "");
            return hex;

        }

        public static byte[] FromHexToByte(string hex)
        {
            // hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }


        public static string Decrypt(string cipherString, bool useHashing = true)
        {
            byte[] bytes = FromHexToByte(cipherString);
            string sp = Convert.ToBase64String(bytes);
            byte[] keyArray;

            byte[] toEncryptArray = Convert.FromBase64String(sp);

            //Get your key from config file to open the lock!

            //string key = "SoftSolPakistan";

            if (useHashing)

            {
                //if hashing was used get the hash code with regards to your key

                MD5CryptoServiceProvider hashmd5 = new MD5CryptoServiceProvider();

                keyArray = hashmd5.ComputeHash(UTF8Encoding.UTF8.GetBytes(key));

                //release any resource held by the MD5CryptoServiceProvider

                hashmd5.Clear();

            }

            else

            {

                //if hashing was not implemented get the byte code of the key

                keyArray = UTF8Encoding.UTF8.GetBytes(key);

            }

            TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider();

            //set the secret key for the tripleDES algorithm

            tdes.Key = keyArray;

            //mode of operation. there are other 4 modes.

            //We choose ECB(Electronic code Book)



            tdes.Mode = CipherMode.ECB;

            //padding mode(if any extra byte added)

            tdes.Padding = PaddingMode.PKCS7;



            ICryptoTransform cTransform = tdes.CreateDecryptor();

            byte[] resultArray = cTransform.TransformFinalBlock(

                                 toEncryptArray, 0, toEncryptArray.Length);

            //Release resources held by TripleDes Encryptor               

            tdes.Clear();

            //return the Clear decrypted TEXT

            return UTF8Encoding.UTF8.GetString(resultArray);

        }

    }
}