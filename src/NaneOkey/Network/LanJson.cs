using System.Web.Script.Serialization;

namespace NaneOkey.Network
{
    public static class LanJson
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static string Serialize<T>(T value)
        {
            return Serializer.Serialize(value);
        }

        public static T Deserialize<T>(string value)
        {
            return Serializer.Deserialize<T>(value);
        }
    }
}
