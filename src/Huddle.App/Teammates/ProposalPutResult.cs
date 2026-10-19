namespace Agency.Huddle.App.Teammates;

/// <summary>The outcome of <see cref="ProposalStore.TryPut"/> against Spec §8.4's replacement table.</summary>
internal enum ProposalPutResult
{
    /// <summary>No Proposal was pending in the Room; the new one is now stored.</summary>
    Stored,

    /// <summary>The same Agent's earlier Proposal in this Room was replaced by the new one.</summary>
    Replaced,

    /// <summary>Another Agent's Proposal is already pending in this Room; the new one was refused.</summary>
    Refused,
}
