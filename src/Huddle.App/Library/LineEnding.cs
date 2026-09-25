namespace Agency.Huddle.App.Library;

/// <summary>The line ending style used in a text file.</summary>
public enum LineEnding
{
    /// <summary>Windows/DOS line ending: carriage return + line feed (CRLF, \r\n).</summary>
    CrLf,

    /// <summary>Unix/Linux line ending: line feed only (LF, \n).</summary>
    Lf,
}
