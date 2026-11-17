namespace Agency.Huddle.App.Helpers;

/// <summary>An extra section a Helper brings to its card on Settings › Helpers, drawn under the description with <c>DynamicComponent</c>. Registered in DI like the Helpers themselves.</summary>
internal interface IHelperCardSection
{
    /// <summary>The Helper whose card shows the section.</summary>
    HelperId Helper { get; }

    /// <summary>The component to draw. It takes one string parameter, <c>HelperId</c>.</summary>
    Type Component { get; }
}
