using Agency.Huddle.SkillPacks.Resolving;
using MudBlazor;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>How one state looks: the chip's words, its colour family, its leading icon and the one sentence that explains it.</summary>
/// <param name="Text">The chip text; <c>In use</c> for <see cref="ItemState.InUse"/>, which <see cref="SkillStateText.ChipText"/> counts.</param>
/// <param name="Color">The colour the chip tints with.</param>
/// <param name="Icon">The leading icon, so colour is never the only signal.</param>
/// <param name="Sentence">The explanation, with <c>{count}</c>, <c>{holders}</c> and <c>{reason}</c> where they apply.</param>
internal sealed record StateLook(string Text, Color Color, string Icon, string Sentence);
