using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using AESFactory;

namespace OCRTester.Model.Settings
{
    public class Config
    {
        private const string ENCRYPTED = "encrypted";
        private const char NAME_VALUE_DIVIDER = '=';
        private const char VALUE_ENCRYPT_DIVIDER = ':';
        private const char AES_DIVIDER = '.';

        private readonly AesMaze am;

        public void SaveConfigData(params ConfigData[] configs)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection cfgc = config.AppSettings.Settings;

            // 라이브러리에 이거 추가?
            foreach (ConfigData c in configs)
            {
                string value = (c.Encrypt) ? EncryptData(c.Value) + ":encrypted" : c.Value;
                if (cfgc[c.Name] == null)
                {
                    cfgc.Add(c.Name, value);
                }
                else
                {
                    cfgc[c.Name].Value = value;
                }
            }

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
        }

        public ConfigData[] LoadConfigData()
        {
            List<ConfigData> configDataList = new List<ConfigData>();
            NameValueCollection appSettings = ConfigurationManager.AppSettings;

            for (int idx = 0; idx < appSettings.Count; idx++)
            {
                string key = appSettings.GetKey(idx);
                string[] splittedValue = appSettings[key].Split(VALUE_ENCRYPT_DIVIDER);
                if (splittedValue.Length > 1 && splittedValue[1].Equals(ENCRYPTED))
                {
                    configDataList.Add(new ConfigData(key, DecryptData(splittedValue[0]), true));
                }
                else
                {
                    configDataList.Add(new ConfigData(key, appSettings[key], false));
                }
            }

            return configDataList.ToArray();
        }

        public string GetValue(string name)
        {
            return ConfigurationManager.AppSettings[name];
        }

        private string EncryptData(string data)
        {
            if (data == null)
            {
                return null;
            }

            string encryptedData = string.Empty;
            byte[] bData = am.Encrypt(data);

            foreach (byte b in bData)
            {
                encryptedData += b + ".";
            }

            return encryptedData.Remove(encryptedData.Length - 1);
        }

        private string DecryptData(string data)
        {
            try
            {
                string[] splittedData = data.Split(AES_DIVIDER);
                List<byte> byteList = new List<byte>();

                foreach (string s in splittedData)
                {
                    byteList.Add(byte.Parse(s));
                }
                byteList.ToArray();

                return am.Decrypt(byteList.ToArray());
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public Config(byte[] AesKey, byte[] AesIv)
        {
            am = new AesMaze(AesKey, AesIv);
        }
    }
}
