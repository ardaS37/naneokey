using System.Web.Script.Serialization;

namespace NaneOkey.Network
{
    public static class LanJson
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static readonly object Sync = new object();

        public static string Serialize<T>(T value)
        {
            lock (Sync)
            {
                return Serializer.Serialize(value);
            }
        }

        public static T Deserialize<T>(string value)
        {
            lock (Sync)
            {
                return Serializer.Deserialize<T>(value);
            }
        }
    }
}
