using System.Runtime.InteropServices;

namespace VartFanSkaViLuncha.OpeningHoursParser;

public sealed partial class OpeningHoursParser
{
    [LibraryImport("opening_hours_parser", EntryPoint = "is_open_at_lunch", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsOpenAtLunch(string pattern);
}
