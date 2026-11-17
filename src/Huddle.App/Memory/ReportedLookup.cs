namespace Agency.Huddle.App.Memory;

/// <summary>What <see cref="MemoryPaths.ResolveReportedOf"/> found, so the <c>memory</c> tool can answer each case in its own words.</summary>
internal enum ReportedLookup
{
    /// <summary>The caller is not the Chief of Staff, so the <c>agent:</c> value is not one it may use.</summary>
    NotChiefOfStaff,

    /// <summary>The Name is no Team's manager today.</summary>
    NotManager,

    /// <summary>The Name is a manager's, but no safe path to its reported file exists: no folder under the Agents root, or a junction or link on the way.</summary>
    NoFile,

    /// <summary>The Name is a manager's and the path is the one place its reported file may be (the file itself may not exist yet).</summary>
    Found,
}
