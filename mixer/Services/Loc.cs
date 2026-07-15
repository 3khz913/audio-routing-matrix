using System;
using System.Windows;

namespace mixer.Services
{
    public static class Loc
    {
        public static string Get(string key)
        {
            try
            {
                return (string)System.Windows.Application.Current.FindResource(key);
            }
            catch
            {
                return $"[{key}]";
            }
        }

        public static string Get(string key, params object[] args)
        {
            try
            {
                return string.Format(Get(key), args);
            }
            catch
            {
                return $"[{key}]";
            }
        }
    }
}
