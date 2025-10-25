using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace PasswordVault.Services;

public static class CanonicalJson
{
    public static byte[] SerializeStable(object obj)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffK",
            Culture = System.Globalization.CultureInfo.InvariantCulture
        };
        var json = JsonConvert.SerializeObject(obj, settings);
        return Encoding.UTF8.GetBytes(json);
    }
}
