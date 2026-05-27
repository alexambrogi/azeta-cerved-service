namespace CERVED_Service
{
    internal class Common
    {
        internal static IConfiguration Configuration { get; set; } = null!;
        internal static int DefaultCommandTimeout { get; set; }
        internal static ConnectionData ConnectionData { get; set; } = null!;
        internal static int TimeZoneAdd { get; set; }
        internal static string API { get; set; } = string.Empty;
        internal static string FromEmail { get; set; } = string.Empty;
    }
}
